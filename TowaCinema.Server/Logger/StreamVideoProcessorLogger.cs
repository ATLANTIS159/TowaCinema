using TowaCinema.Server.Logger.Interface;
using ILogger = Serilog.ILogger;

namespace TowaCinema.Server.Logger;

public class StreamVideoProcessorLogger : IStreamVideoProcessorLogger, IDisposable
{
    private readonly Serilog.Core.Logger _logger;

    public StreamVideoProcessorLogger(Serilog.Core.Logger logger)
    {
        _logger = logger;
    }

    public void Dispose()
    {
        _logger.Dispose();
    }

    public ILogger Log => _logger;
}