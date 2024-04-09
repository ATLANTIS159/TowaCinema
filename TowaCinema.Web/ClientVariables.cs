namespace TowaCinema.Web;

public static class ClientVariables
{
    public const string PageTitleEnding = "etaCarinae Archive";

    public static readonly string? AdminIds =
        Environment.GetEnvironmentVariable("ADMIN_TWITCH_IDS");

    public static readonly string? InternalServerUrl =
        Environment.GetEnvironmentVariable("INTERNAL_SERVER_URL");

    public static readonly string? ExternalServerUrl =
        Environment.GetEnvironmentVariable("EXTERNAL_SERVER_URL");

    public static readonly string? TwitchAppClientId =
        Environment.GetEnvironmentVariable("TWITCH_APP_CLIENT_ID");

    public static readonly string? TwitchAppClientSecret =
        Environment.GetEnvironmentVariable("TWITCH_APP_CLIENT_SECRET");
}