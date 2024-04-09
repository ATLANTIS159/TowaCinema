namespace TowaCinema.Server;

public static class ServerVariables
{
    // public const string CacheStreamsKey = "streamsInfo";
    // public const string CacheStreamInfoKey = "streamInfo";
    public const string CacheStreamVideoKey = "streamVideo";
    // public const string CacheGamesKey = "gamesInfo";
    // public const string CacheGameInfoKey = "gameInfo";
    // public const string CacheGameStreamsKey = "gameStreams";

    public const string ThumbnailFolder = "Thumbnails";
    public const string SmallThumbnailFile = "thumbnail-sm.jpg";
    public const string MediumThumbnailFile = "thumbnail-md.jpg";
    public const string LargeThumbnailFile = "thumbnail-lg.jpg";

    private static readonly string DataFolder = Path.Combine(AppContext.BaseDirectory, "ServerData");
    public static readonly string DatabaseFolder = Path.Combine(DataFolder, "Database");
    public static readonly string FfmpegFolder = Path.Combine(DataFolder, "FFmpeg");
    public static readonly string TempFolder = Path.Combine(DataFolder, "Temp");

    public static readonly string LogsFolder = Path.Combine(DataFolder, "Logs");

    public static readonly string SourceStreamsFolder = Path.Combine(AppContext.BaseDirectory, "Streams");

    public static readonly string AssetsFolder = Path.Combine(DataFolder, "Assets");
    public static readonly string ProcessedStreamsFolder = Path.Combine(AssetsFolder, "ProcessedStreams");
    public static readonly string GamesAssetsFolder = Path.Combine(AssetsFolder, "GamesAssets");

    public static readonly string FfMpegFile =
        Path.Combine(FfmpegFolder, OperatingSystem.IsLinux() ? "ffmpeg" : "ffmpeg.exe");

    public static readonly string FfProbeFile =
        Path.Combine(FfmpegFolder, OperatingSystem.IsLinux() ? "ffprobe" : "ffprobe.exe");
}