using ILogger = Serilog.ILogger;

namespace TowaCinema.Server.Logger.Interface;

public interface IFfmpegProcessorLogger
{
    public ILogger Log { get; }
}