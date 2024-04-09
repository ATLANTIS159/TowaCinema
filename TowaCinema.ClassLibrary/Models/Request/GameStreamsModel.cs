namespace TowaCinema.ClassLibrary.Models.Request;

public class GameStreamsModel
{
    public Guid Id { get; set; }
    public string Title { get; init; } = null!;
    public List<StreamInfoModel> Streams { get; set; } = [];
}