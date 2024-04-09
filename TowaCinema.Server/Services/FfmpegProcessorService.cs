using TowaCinema.ClassLibrary.Models.Request;
using TowaCinema.Server.Db.DbModels;
using TowaCinema.Server.Logger.Interface;
using TowaCinema.Server.Services.Interfaces;
using Xabe.FFmpeg;

namespace TowaCinema.Server.Services;

public class FfmpegProcessorService : IFfmpegProcessorService
{
    private readonly IFfmpegProcessorLogger _logger;

    public FfmpegProcessorService(IFfmpegProcessorLogger logger)
    {
        _logger = logger;
    }

    public async Task GenerateStreamThumbnails(StreamVideo streamInfo, string processedStreamFolder)
    {
        var thumbnailFolder = Path.Combine(processedStreamFolder, ServerVariables.ThumbnailFolder);

        if (!Directory.Exists(thumbnailFolder)) Directory.CreateDirectory(thumbnailFolder);

        var smallThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.SmallThumbnailFile);
        var mediumThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.MediumThumbnailFile);
        var largeThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.LargeThumbnailFile);

        var isSmallExists = File.Exists(smallThumbnailFile);
        var isMediumExists = File.Exists(mediumThumbnailFile);
        var isLargeExists = File.Exists(largeThumbnailFile);

        if (isSmallExists && isMediumExists && isLargeExists) return;

        _logger.Log.Information("Запущено создание превью для файла {FileName}", streamInfo.SourceFileName);

        var duration = TimeSpan.Parse(streamInfo.Duration).TotalSeconds;
        var thumbnailTime = TimeSpan.FromSeconds(duration * 0.25).ToString(@"hh\:mm\:ss");
        var filePath = Path.Combine(ServerVariables.SourceStreamsFolder, streamInfo.SourceFileName);

        if (!isSmallExists)
        {
            var argument = $"""
                            -ss {thumbnailTime} -i "{filePath}" -vf scale=-1:360 -frames:v 1 "{smallThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        if (!isMediumExists)
        {
            var argument = $"""
                            -ss {thumbnailTime} -i "{filePath}" -vf scale=-1:720 -frames:v 1 "{mediumThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        if (!isLargeExists)
        {
            var argument = $"""
                            -ss {thumbnailTime} -i "{filePath}" -frames:v 1 "{largeThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        _logger.Log.Information("Создание превью для файла {FileName} завершено", streamInfo.SourceFileName);
    }

    public async Task GenerateGameThumbnails(GameInfoModel gameInfoModel, string tempFilePath, bool isRecreate = false)
    {
        var thumbnailFolder = Path.Combine(ServerVariables.GamesAssetsFolder, gameInfoModel.Id.ToString(),
            ServerVariables.ThumbnailFolder);

        if (!Directory.Exists(thumbnailFolder)) Directory.CreateDirectory(thumbnailFolder);

        var smallThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.SmallThumbnailFile);
        var mediumThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.MediumThumbnailFile);
        var largeThumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.LargeThumbnailFile);

        var isSmallExists = !isRecreate && File.Exists(smallThumbnailFile);
        var isMediumExists = !isRecreate && File.Exists(mediumThumbnailFile);
        var isLargeExists = !isRecreate && File.Exists(largeThumbnailFile);

        _logger.Log.Information("Запущено создание превью для игры {GameTitle}", gameInfoModel.Title);

        if (!isSmallExists)
        {
            var argument = $"""
                            -y -i "{tempFilePath}" -vf scale=-1:360 "{smallThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        if (!isMediumExists)
        {
            var argument = $"""
                            -y -i "{tempFilePath}" -vf scale=-1:720 "{mediumThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        if (!isLargeExists)
        {
            var argument = $"""
                            -y -i "{tempFilePath}" "{largeThumbnailFile}"
                            """;

            await FFmpeg.Conversions.New().Start(argument);
        }

        _logger.Log.Information("Создание превью для игры {GameTitle} завершено", gameInfoModel.Title);
    }

    public async Task GenerateStreamQuality(StreamVideo streamInfo, string processedStreamFolder)
    {
        _logger.Log.Information("Запущено создание сегментов и плейлиста для файла {FileName}",
            streamInfo.SourceFileName);

        var qualityFolder = Path.Combine(processedStreamFolder, "1080p-segments");

        if (!Directory.Exists(qualityFolder))
            Directory.CreateDirectory(qualityFolder);

        var arguments =
            $"""-i "{Path.Combine(ServerVariables.SourceStreamsFolder, streamInfo.SourceFileName)}" -c:v copy -c:a copy -f hls -hls_time 10 -hls_playlist_type vod -hls_flags independent_segments -hls_segment_type mpegts -hls_segment_filename "{qualityFolder}/segment-1080-%010d.ts" "{Path.Combine(processedStreamFolder, "playlist-1080.m3u8")}" """;


        await FFmpeg.Conversions.New().Start(arguments);

        var titleFile = Path.Combine(processedStreamFolder, streamInfo.SourceFileName.Replace(".mp4", ""));

        if (!File.Exists(titleFile)) await File.WriteAllTextAsync(titleFile, streamInfo.SourceFileName);

        _logger.Log.Information("Создание сегментов и плейлиста для файла {FileName} завершено",
            streamInfo.SourceFileName);
    }
}