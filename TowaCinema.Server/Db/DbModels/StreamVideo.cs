namespace TowaCinema.Server.Db.DbModels;

public class StreamVideo
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime StreamDate { get; set; }
    public string Duration { get; set; } = null!;
    public string SourceFileName { get; set; } = null!;
    public bool IsPublished { get; set; }
    public List<Game> Games { get; set; } = new();
    public List<User> UsersInProgress { get; set; } = new();
    public List<User> UsersCompleted { get; set; } = new();
    public List<StreamGame> StreamGames { get; set; } = new();
    public List<InProgress> InProgresses { get; set; } = new();
    public List<Completed> Completeds { get; set; } = new();
}

public class Game
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public List<StreamVideo> StreamVideos { get; set; } = new();
    public List<StreamGame> StreamGames { get; set; } = new();
}

public class StreamGame
{
    public Guid StreamVideoId { get; set; }
    public StreamVideo? StreamVideo { get; set; }
    public Guid GameId { get; set; }
    public Game? Game { get; set; }
    public TimeOnly GameTimestamp { get; set; }
}

public class User
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public List<StreamVideo> StreamVideosInProgress { get; set; } = new();
    public List<StreamVideo> StreamVideosCompleted { get; set; } = new();
    public List<InProgress> InProgresses { get; set; } = new();
    public List<Completed> Completeds { get; set; } = new();
}

public class InProgress
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid StreamVideoId { get; set; }
    public StreamVideo? StreamVideo { get; set; }
    public int StopTimeInSeconds { get; set; }
}

public class Completed
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid StreamVideoId { get; set; }
    public StreamVideo? StreamVideo { get; set; }
}