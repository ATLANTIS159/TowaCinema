namespace TowaCinema.ClassLibrary.Models.Request;

public class StreamInfoModel
{
    public Guid Id { get; init; }
    public string Title { get; set; } = null!;
    public DateTime StreamDate { get; set; }
    public string Duration { get; init; } = null!;
    public List<GameInfoModel> Games { get; set; } = [];
    public ProgressStateModel ProgressState { get; set; } = new();
    public bool IsPublished { get; set; }
}