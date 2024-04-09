using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TowaCinema.Server.Db.Context;
using TowaCinema.Server.Db.DbModels;
using TowaCinema.Server.Logger.Interface;
using TowaCinema.Server.Services.Interfaces;
using Xabe.FFmpeg;

namespace TowaCinema.Server.Services;

public class CoreProcessorService : ICoreProcessorService
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<CinemaDbContext> _database;
    private readonly IFfmpegProcessorService _ffmpegService;
    private readonly IStreamVideoProcessorLogger _logger;

    private readonly Queue<string> _processQueue = new();
    private string _filesInProcess = "";

    public CoreProcessorService(IDbContextFactory<CinemaDbContext> database,
        IStreamVideoProcessorLogger logger, IMemoryCache cache, IFfmpegProcessorService ffmpegService)
    {
        _database = database;
        _logger = logger;
        _cache = cache;
        _ffmpegService = ffmpegService;
    }

    public bool IsProcessing { get; private set; }

    public async Task CheckStreamFolder()
    {
        _logger.Log.Information("Запущен проверка стримов в папке и базе данных");

        var streamFiles = Directory.GetFiles(ServerVariables.SourceStreamsFolder, "*.mp4").Select(Path.GetFileName)
            .ToList();
        var processedFolders = Directory.GetDirectories(ServerVariables.ProcessedStreamsFolder)
            .Select(Path.GetFileName).ToList();

        await using var db = await _database.CreateDbContextAsync();
        var dbVideos = db.StreamVideos.ToList();

        if (!IsProcessing)
        {
            var missingElements = dbVideos.Where(w =>
                    streamFiles.All(a => a != w.SourceFileName) || processedFolders.All(a => a != w.Id.ToString()))
                .ToList();

            if (missingElements.Count > 0)
            {
                foreach (var missingElement in missingElements)
                {
                    var element = Path.Combine(ServerVariables.ProcessedStreamsFolder, missingElement.Id.ToString());

                    if (Directory.Exists(element))
                    {
                        Directory.Delete(element, true);
                        db.StreamVideos.Remove(missingElement);

                        _logger.Log.Information(
                            "Файл | {FileName} | не найден в папке с видео. Папка с идентификатором | {Id} | удалена и объект удалён из базы данных",
                            missingElement.SourceFileName, missingElement.Id);
                    }
                    else
                    {
                        db.StreamVideos.Remove(missingElement);

                        _logger.Log.Warning(
                            "Папка с идентификатором | {Id} | не найдена. Объект удалён из базы данных",
                            missingElement.Id);
                    }
                }

                await db.SaveChangesAsync();
            }

            dbVideos = db.StreamVideos.ToList();

            processedFolders = Directory.GetDirectories(ServerVariables.ProcessedStreamsFolder)
                .Select(Path.GetFileName).ToList();

            var wrongFolders = processedFolders.Where(w => dbVideos.All(a => a.Id.ToString() != w)).ToList();

            if (wrongFolders.Count > 0)
                foreach (var wrongFolder in wrongFolders)
                {
                    if (string.IsNullOrWhiteSpace(wrongFolder)) continue;

                    var folder = Path.Combine(ServerVariables.ProcessedStreamsFolder, wrongFolder);

                    if (!Directory.Exists(folder)) continue;

                    Directory.Delete(folder, true);
                    processedFolders.Remove(wrongFolder);

                    _logger.Log.Information(
                        "Папка с идентификатором | {Folder} | не найдена в базе данных и удалена",
                        wrongFolder);
                }

            var missingThumbnails = processedFolders.Where(w =>
                w is not null && (!Directory.Exists(Path.Combine(ServerVariables.ProcessedStreamsFolder, w)) ||
                                  !File.Exists(Path.Combine(ServerVariables.ProcessedStreamsFolder, w,
                                      ServerVariables.ThumbnailFolder, ServerVariables.SmallThumbnailFile)) ||
                                  !File.Exists(Path.Combine(ServerVariables.ProcessedStreamsFolder, w,
                                      ServerVariables.ThumbnailFolder, ServerVariables.MediumThumbnailFile)) ||
                                  !File.Exists(Path.Combine(ServerVariables.ProcessedStreamsFolder, w,
                                      ServerVariables.ThumbnailFolder, ServerVariables.LargeThumbnailFile)))).ToList();

            if (missingThumbnails.Count > 0)
                foreach (var missingThumbnail in missingThumbnails.Where(missingThumbnail =>
                             !string.IsNullOrWhiteSpace(missingThumbnail)))
                    await _ffmpegService.GenerateStreamThumbnails(
                        dbVideos.First(w => w.Id.ToString() == missingThumbnail),
                        Path.Combine(ServerVariables.ProcessedStreamsFolder, missingThumbnail!));
        }

        var newFiles = streamFiles.Where(w => dbVideos.All(a => a.SourceFileName != w)).ToList();

        if (newFiles.Count > 0)
        {
            _logger.Log.Information("Найдено | {Count} | новых файлов, которых нет в базе данных", newFiles.Count);

            foreach (var newFile in newFiles)
            {
                if (string.IsNullOrWhiteSpace(newFile)) continue;
                if (_filesInProcess == newFile || _processQueue.Contains(newFile)) continue;

                _processQueue.Enqueue(newFile);

                _logger.Log.Information("Файл | {FileName} | добавлен в очередь на обработку",
                    Path.GetFileName(newFile));
            }

            if (!IsProcessing) _ = ProcessNextVideo();
        }

        _logger.Log.Information("Процесс сканирования папки завершён");
    }

    private async Task ProcessNextVideo()
    {
        while (_processQueue.Count > 0)
        {
            IsProcessing = true;
            var fileName = _processQueue.Dequeue();
            _filesInProcess = fileName;

            _logger.Log.Information("Запущена обработка файла {FileName}", fileName);

            var streamInfoItem = await CreateStreamInfoItem(fileName);

            await using var db = await _database.CreateDbContextAsync();

            var processedStreamFolder =
                Path.Combine(ServerVariables.ProcessedStreamsFolder, streamInfoItem.Id.ToString());

            if (!Directory.Exists(processedStreamFolder)) Directory.CreateDirectory(processedStreamFolder);

            await _ffmpegService.GenerateStreamThumbnails(streamInfoItem, processedStreamFolder);

            await _ffmpegService.GenerateStreamQuality(streamInfoItem, processedStreamFolder);

            db.StreamVideos.Add(streamInfoItem);

            await db.SaveChangesAsync();

            _filesInProcess = "";

            _logger.Log.Information("Добавление файла {FileName} в базу данных успешно завершено",
                Path.GetFileName(streamInfoItem.SourceFileName));

            ((MemoryCache)_cache).Clear();
        }

        IsProcessing = false;
    }

    private static async Task<StreamVideo> CreateStreamInfoItem(string fileName)
    {
        var id = Guid.NewGuid();
        var filePath = Path.Combine(ServerVariables.SourceStreamsFolder, fileName);
        var title = GetTitle(fileName).Replace(".mp4", "");
        var match = Regex.Match(fileName, "([0-9]+(\\.[0-9]+)+)");
        var streamDate = match.Success ? DateTime.ParseExact(match.Value, "dd.MM.yyyy", null) : DateTime.MinValue;
        var mediaInfo = await FFmpeg.GetMediaInfo(filePath);
        var duration = mediaInfo.Duration.TotalSeconds;

        return new StreamVideo
        {
            Id = id,
            Title = title,
            StreamDate = streamDate,
            Duration = TimeSpan.FromSeconds(duration).ToString(@"hh\:mm\:ss"),
            SourceFileName = fileName,
            Games = [],
            IsPublished = false
        };
    }

    private static string GetTitle(string fileName)
    {
        return fileName.Contains(". ") ? fileName.Split(". ")[1].Remove(0, 11) : fileName;
    }
}