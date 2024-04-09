using System.Diagnostics;
using System.Text;
using Serilog;
using Serilog.Events;
using TowaCinema.Server.Db.Context;
using TowaCinema.Server.Extensions;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace TowaCinema.Server;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(Program))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(ServerVariables.LogsFolder, "Server", "Server_.etaLogs"),
                LogEventLevel.Information, template,
                rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateBootstrapLogger();

        Log.Information("Инициализация сервера");

        if (!Directory.Exists(ServerVariables.SourceStreamsFolder))
        {
            Directory.CreateDirectory(ServerVariables.SourceStreamsFolder);
            Log.Information("Создана папка для исходников стримов");
        }

        if (!Directory.Exists(ServerVariables.AssetsFolder))
        {
            Directory.CreateDirectory(ServerVariables.AssetsFolder);
            Log.Information("Создана папка для ассетов");
        }

        if (!Directory.Exists(ServerVariables.ProcessedStreamsFolder))
        {
            Directory.CreateDirectory(ServerVariables.ProcessedStreamsFolder);
            Log.Information("Создана папка для обработанных стримов");
        }

        if (!Directory.Exists(ServerVariables.GamesAssetsFolder))
        {
            Directory.CreateDirectory(ServerVariables.GamesAssetsFolder);
            Log.Information("Создана папка для ассетов игр");
        }

        if (!Directory.Exists(ServerVariables.DatabaseFolder))
        {
            Directory.CreateDirectory(ServerVariables.DatabaseFolder);
            Log.Information("Создана папка базы данных");
        }

        if (!Directory.Exists(ServerVariables.FfmpegFolder))
        {
            Directory.CreateDirectory(ServerVariables.FfmpegFolder);
            Log.Information("Создана папка для файлов FFmpeg");
        }

        FFmpeg.SetExecutablesPath(ServerVariables.FfmpegFolder);

        if (!File.Exists(ServerVariables.FfMpegFile) || !File.Exists(ServerVariables.FfProbeFile))
        {
            Log.Warning(
                "Не найден один или несколько необходимых файлов FFmpeg для работы сервера. Запущена загрузка недостающих файлов");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ServerVariables.FfmpegFolder);
            Log.Information(
                "Все недостающие файлы FFmpeg успешно загружены");

            try
            {
                Process.Start("chmod", $"777 {ServerVariables.FfMpegFile}");
                Process.Start("chmod", $"777 {ServerVariables.FfProbeFile}");
            }
            catch (Exception)
            {
                //ignore
            }

            Log.Information(
                "Все права на файлы FFmpeg исправлены");
        }

        Log.Information("Инициализация основных систем");

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddMemoryCache();
        builder.Services.AddDbContextFactory<CinemaDbContext>();

        builder.Services.AddArchiveServices(Log.Logger);

        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddCors(o =>
            o.AddPolicy("cinema", build => { build.WithOrigins("*").AllowAnyMethod().AllowAnyHeader(); }));

        var app = builder.Build();

        app.UseCors("cinema");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        await app.RunAsync();
    }
}