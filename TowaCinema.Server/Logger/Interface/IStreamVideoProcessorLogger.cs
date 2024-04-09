using ILogger = Serilog.ILogger;

namespace TowaCinema.Server.Logger.Interface;

public interface IStreamVideoProcessorLogger
{
    public ILogger Log { get; }
}