using System.Text;
using Serilog;
using Serilog.Events;
using TowaCinema.Server.Logger;
using TowaCinema.Server.Logger.Interface;
using TowaCinema.Server.Services;
using TowaCinema.Server.Services.Interfaces;
using ILogger = Serilog.ILogger;

namespace TowaCinema.Server.Extensions;

public static class ArchiveExtention
{
    public static void AddArchiveServices(this IServiceCollection services, ILogger logger)
    {
        services.AddSingleton(InitFfmpegProcessorLogger());
        services.AddSingleton(InitStreamVideoProcessorLogger());
        services.AddSingleton<IFfmpegProcessorService, FfmpegProcessorService>();
        services.AddSingleton<ICoreProcessorService, CoreProcessorService>();
    }

    private static IFfmpegProcessorLogger InitFfmpegProcessorLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new FfmpegProcessorLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(FfmpegProcessorLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(ServerVariables.LogsFolder, "StreamVideoProcessor", "svp_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }

    private static IStreamVideoProcessorLogger InitStreamVideoProcessorLogger()
    {
        const string template = "[{Timestamp:HH:mm:ss} {Level:u3} {Source}] {Message:lj}{NewLine}{Exception}";
        return new StreamVideoProcessorLogger(new LoggerConfiguration()
            .Enrich.WithProperty("Source", nameof(StreamVideoProcessorLogger))
            .WriteTo.Console(outputTemplate: template)
            .WriteTo.File(Path.Combine(ServerVariables.LogsFolder, "StreamVideoProcessor", "svp_.etaLogs"),
                LogEventLevel.Information, template, rollingInterval: RollingInterval.Day, encoding: Encoding.UTF8)
            .CreateLogger());
    }
}