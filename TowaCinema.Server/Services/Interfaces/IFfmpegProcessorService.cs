using TowaCinema.ClassLibrary.Models.Request;
using TowaCinema.Server.Db.DbModels;

namespace TowaCinema.Server.Services.Interfaces;

public interface IFfmpegProcessorService
{
    public Task GenerateStreamThumbnails(StreamVideo streamInfo, string processedStreamFolder);
    public Task GenerateGameThumbnails(GameInfoModel gameInfoModel, string tempFilePath, bool isRecreate = false);
    public Task GenerateStreamQuality(StreamVideo streamInfo, string processedStreamFolder);
}