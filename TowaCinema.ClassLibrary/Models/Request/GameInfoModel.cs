namespace TowaCinema.ClassLibrary.Models.Request;

public class GameInfoModel
{
    public Guid Id { get; init; }
    public string Title { get; set; } = null!;
    public TimeOnly GameTimestamp { get; set; }
    public int StreamsCount { get; init; }
    public bool IsActive { get; set; }
}