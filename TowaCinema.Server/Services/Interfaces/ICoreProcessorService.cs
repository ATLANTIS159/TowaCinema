namespace TowaCinema.Server.Services.Interfaces;

public interface ICoreProcessorService
{
    bool IsProcessing { get; }
    Task CheckStreamFolder();
}