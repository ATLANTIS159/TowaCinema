using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using TowaCinema.ClassLibrary.Enums;
using TowaCinema.ClassLibrary.Models.Request;
using TowaCinema.Server.Db.Context;
using TowaCinema.Server.Db.DbModels;
using TowaCinema.Server.Services.Interfaces;

namespace TowaCinema.Server.Controllers;

[ApiController]
[Route("archive")]
public class ArchiveController : ControllerBase
{
    private readonly IMemoryCache _cache;

    private readonly IDbContextFactory<CinemaDbContext> _database;
    private readonly IFfmpegProcessorService _ffmpeg;
    private readonly ICoreProcessorService _scanService;
    private readonly TimeSpan _storeTime = TimeSpan.FromHours(3);

    public ArchiveController(IDbContextFactory<CinemaDbContext> database, IMemoryCache cache,
        ICoreProcessorService scanService,
        IFfmpegProcessorService ffmpeg)
    {
        _database = database;
        _cache = cache;
        _scanService = scanService;
        _ffmpeg = ffmpeg;
    }

    [HttpGet]
    [Route("streams")]
    public async Task<IActionResult> GetStreams([FromQuery] bool admin, [FromQuery] string? userId)
    {
        await using var db = await _database.CreateDbContextAsync();
        var dbStreams = db.StreamVideos.Include(streamVideo => streamVideo.StreamGames)
            .ThenInclude(streamGame => streamGame.Game!).Include(streamVideo => streamVideo.InProgresses)
            .ThenInclude(inProgress => inProgress.User).Include(streamVideo => streamVideo.Completeds)
            .ThenInclude(completed => completed.User).ToList();
        var streams = dbStreams.Select(s => new StreamInfoModel
        {
            Id = s.Id,
            Title = s.Title,
            StreamDate = s.StreamDate,
            Duration = s.Duration,
            Games = s.StreamGames.Where(w => w.StreamVideoId == s.Id).Select(l => new GameInfoModel
            {
                Id = l.GameId,
                Title = l.Game!.Title,
                GameTimestamp = l.GameTimestamp
            }).ToList(),
            ProgressState = new ProgressStateModel
            {
                InProgressTimestamp = null,
                IsCompleted = null
            },
            IsPublished = s.IsPublished
        }).ToList();

        if (!string.IsNullOrWhiteSpace(userId))
            foreach (var stream in streams)
            {
                var streamTimestamp = dbStreams.FirstOrDefault(f => f.Id == stream.Id)
                    ?.InProgresses
                    .FirstOrDefault(f => f.StreamVideoId == stream.Id && f.User?.UserId == userId)
                    ?.StopTimeInSeconds.ToString();
                var isCompleted = dbStreams.FirstOrDefault(f => f.Id == stream.Id)?.Completeds
                    .Any(a => a.StreamVideoId == stream.Id && a.User?.UserId == userId);

                if (streamTimestamp is not null || isCompleted is not null)
                    stream.ProgressState = new ProgressStateModel
                    {
                        InProgressTimestamp = streamTimestamp,
                        IsCompleted = isCompleted
                    };
            }

        if (!admin) streams = streams.Where(w => w.IsPublished).ToList();

        return Ok(JsonConvert.SerializeObject(streams, Formatting.Indented));
    }

    [HttpGet]
    [Route("streams/{streamId}")]
    public async Task<IActionResult> GetStreamInfo(Guid streamId)
    {
        await using var db = await _database.CreateDbContextAsync();
        var stream = db.StreamVideos.Include(i => i.StreamGames).ThenInclude(t => t.Game)
            .FirstOrDefault(f => f.Id == streamId);

        if (stream is null) return NotFound("Стрим не найден в базе данных");

        var streamInfo = new StreamInfoModel
        {
            Id = stream.Id,
            Title = stream.Title,
            StreamDate = stream.StreamDate,
            Duration = stream.Duration,
            Games = stream.StreamGames.Where(w => w.StreamVideoId == stream.Id).Select(l => new GameInfoModel
            {
                Id = l.GameId,
                Title = l.Game!.Title,
                GameTimestamp = l.GameTimestamp
            }).ToList(),
            IsPublished = stream.IsPublished
        };

        return Ok(JsonConvert.SerializeObject(streamInfo, Formatting.Indented));
    }

    [HttpPut]
    [Route("streams")]
    public async Task<IActionResult> UpdateStreamInfo()
    {
        var form = await Request.ReadFormAsync();

        var streamPair = form.FirstOrDefault(f => f.Key == "StreamInfo");

        if (streamPair.Key is null) return BadRequest("Ошибка при обработке данных на сервере");

        var streamInfo = JsonConvert.DeserializeObject<StreamInfoModel>(streamPair.Value.ToString());

        if (streamInfo is null) return StatusCode(502, "Ошибка при получении данных о стриме");

        await using var db = await _database.CreateDbContextAsync();

        var stream = db.StreamVideos
            .Include(streamVideo => streamVideo.StreamGames)
            .Include(streamVideo => streamVideo.Games)
            .FirstOrDefault(streamVideo => streamVideo.Id == streamInfo.Id);

        if (stream is null) return BadRequest($"Стрим с таким ID: {streamInfo.Id} не найден");

        var titleChanged = stream.Title != streamInfo.Title;
        var streamDateChanged = stream.StreamDate != streamInfo.StreamDate;
        var publishChanged = stream.IsPublished != streamInfo.IsPublished;
        var newGames = streamInfo.Games.Where(w => stream.Games.All(a => a.Id != w.Id)).ToList();
        var missingGames = stream.Games.Where(w => streamInfo.Games.All(a => a.Id != w.Id)).ToList();
        var timestampsChanged =
            streamInfo.Games.Where(w => stream.StreamGames.All(a => a.GameTimestamp != w.GameTimestamp))
                .ToList();

        if (missingGames.Count > 0 || newGames.Count > 0)
        {
            var games = db.Games.Include(i => i.StreamGames).Include(i => i.StreamVideos).ToList();

            if (missingGames.Count > 0)
            {
                var deletedGames = games.Where(w => missingGames.Any(a => a.Id == w.Id)).ToList();

                stream.Games.RemoveAll(r => deletedGames.Contains(r));
            }

            if (newGames.Count > 0)
            {
                var gamesToStream = games.Where(w => newGames.Any(a => a.Id == w.Id)).ToList();

                stream.Games.AddRange(gamesToStream);
            }

            await db.SaveChangesAsync();
        }

        if (titleChanged) stream.Title = streamInfo.Title;
        if (streamDateChanged) stream.StreamDate = streamInfo.StreamDate;
        if (publishChanged) stream.IsPublished = streamInfo.IsPublished;

        if (timestampsChanged.Count > 0)
            foreach (var gameInfo in timestampsChanged)
                stream.StreamGames.FirstOrDefault(f => f.StreamVideoId == stream.Id && f.GameId == gameInfo.Id)!
                    .GameTimestamp = gameInfo.GameTimestamp;

        if (!titleChanged && !streamDateChanged && !publishChanged && newGames.Count <= 0 && missingGames.Count <= 0 &&
            timestampsChanged.Count <= 0) return Ok(streamInfo);

        await db.SaveChangesAsync();

        ((MemoryCache)_cache).Clear();

        return Ok();
    }

    [HttpGet]
    [Route("thumbnails/{type}/{streamId}/{size}")]
    [Produces("image/jpg")]
    public IActionResult GetThumbnail(string type, Guid streamId, string size)
    {
        string thumbnailFile;
        string thumbnailFolder;

        switch (type)
        {
            case "stream":
                thumbnailFolder = Path.Combine(ServerVariables.ProcessedStreamsFolder, streamId.ToString(),
                    ServerVariables.ThumbnailFolder);
                break;
            case "game":
                thumbnailFolder = Path.Combine(ServerVariables.GamesAssetsFolder, streamId.ToString(),
                    ServerVariables.ThumbnailFolder);
                break;
            default:
                return NotFound("Превью такого типа не найдено");
        }

        if (!Directory.Exists(thumbnailFolder)) return Empty;

        switch (size)
        {
            case "lg":
                thumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.LargeThumbnailFile);
                break;
            case "md":
                thumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.MediumThumbnailFile);
                break;
            case "sm":
                thumbnailFile = Path.Combine(thumbnailFolder, ServerVariables.SmallThumbnailFile);
                break;
            default:
                return NotFound("Файл такого размера не найден");
        }

        return Directory.Exists(thumbnailFolder) && System.IO.File.Exists(thumbnailFile)
            ? Ok(System.IO.File.OpenRead(thumbnailFile))
            : NotFound("Ошибка при получении файла превью");
    }

    [HttpGet]
    [Route("videos/{streamId}/{file}")]
    public async Task<IActionResult> GetStreamVideo(Guid streamId, string file)
    {
        var key = $"{ServerVariables.CacheStreamVideoKey}-{streamId.ToString()}";
        var isInCache = _cache.TryGetValue(key, out StreamVideo? stream);

        if (isInCache && stream is not null)
        {
            GetRequestedFile(file, streamId, out var cacheFilePath, out var cacheContentType);

            if (cacheFilePath is not null && cacheContentType is not null)
                return File(System.IO.File.OpenRead(cacheFilePath), cacheContentType);

            _cache.Remove(key);
        }

        await using var db = await _database.CreateDbContextAsync();
        stream = await db.StreamVideos.FindAsync(streamId);

        GetRequestedFile(file, streamId, out var dbFilePath, out var dbContentType);

        if (stream is null && dbFilePath is null && dbContentType is null)
            return NotFound("Файлы стрима не найдены в базе данных");

        _cache.Set(key, stream, _storeTime);

        return File(System.IO.File.OpenRead(dbFilePath!), dbContentType!);
    }

    private static void GetRequestedFile(string file, Guid id, out string? filePath, out string? contentType)
    {
        filePath = null;
        contentType = null;

        var extension = Path.GetExtension(file);
        var folder = Path.Combine(ServerVariables.ProcessedStreamsFolder, id.ToString());

        if (!Directory.Exists(folder)) return;

        switch (extension)
        {
            case ".m3u8":
                var playlist = Path.Combine(folder, file);
                if (!System.IO.File.Exists(playlist)) return;
                filePath = playlist;
                contentType = "application/vnd.apple.mpegurl";
                break;
            case ".ts":
                var segment = Path.Combine(folder, "1080p-segments", file);
                if (!System.IO.File.Exists(segment)) return;
                filePath = segment;
                contentType = "video/mp2t";
                break;
        }
    }

    [HttpGet]
    [Route("games")]
    public async Task<IActionResult> GetGames([FromQuery] bool admin)
    {
        await using var db = await _database.CreateDbContextAsync();
        var dbGamesList = db.Games.Include(game => game.StreamVideos).ToList();

        if (!admin) dbGamesList = dbGamesList.Where(w => w.StreamVideos.Count > 0).ToList();

        var games = dbGamesList.Select(s => new GameInfoModel
        {
            Id = s.Id,
            Title = s.Title,
            StreamsCount = s.StreamVideos.Count
        }).ToList();

        return Ok(JsonConvert.SerializeObject(games, Formatting.Indented));
    }

    [HttpGet]
    [Route("games/{id}")]
    public async Task<IActionResult> GetGame(Guid id)
    {
        await using var db = await _database.CreateDbContextAsync();
        var game = db.Games.FirstOrDefault(f => f.Id == id);

        if (game is null) return NotFound("Игра не найдена в базе данных");

        var gameInfo = new GameInfoModel
        {
            Id = game.Id,
            Title = game.Title
        };

        return Ok(JsonConvert.SerializeObject(gameInfo, Formatting.Indented));
    }

    [HttpGet]
    [Route("games/{gameId}/streams")]
    public async Task<IActionResult> GetGameStreams(Guid gameId, [FromQuery] bool admin, [FromQuery] string? userId)
    {
        await using var db = await _database.CreateDbContextAsync();
        var gamesList = db.Games.Include(game => game.StreamVideos)
            .ThenInclude(streamVideo => streamVideo.StreamGames).ToList();

        var game = gamesList.FirstOrDefault(f => f.Id == gameId);

        if (game is null) return BadRequest("Игра с таким ID не найдена");

        var gameStreams = new GameStreamsModel
        {
            Id = game.Id,
            Title = game.Title,
            Streams = game.StreamVideos.Select(s => new StreamInfoModel
            {
                Id = s.Id,
                Title = s.Title,
                StreamDate = s.StreamDate,
                Duration = s.Duration,
                Games = s.StreamGames.Where(w => w.StreamVideoId == s.Id).Select(l => new GameInfoModel
                {
                    Id = l.GameId,
                    Title = l.Game!.Title,
                    GameTimestamp = l.GameTimestamp
                }).ToList(),
                IsPublished = s.IsPublished
            }).ToList()
        };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var dbStreams = db.StreamVideos.Include(streamVideo => streamVideo.InProgresses)
                .ThenInclude(inProgress => inProgress.User).Include(streamVideo => streamVideo.Completeds)
                .ThenInclude(completed => completed.User).ToList();

            foreach (var stream in gameStreams.Streams)
            {
                var streamTimestamp = dbStreams.FirstOrDefault(f => f.Id == stream.Id)
                    ?.InProgresses
                    .FirstOrDefault(f => f.StreamVideoId == stream.Id && f.User?.UserId == userId)
                    ?.StopTimeInSeconds.ToString();
                var isCompleted = dbStreams.FirstOrDefault(f => f.Id == stream.Id)?.Completeds
                    .Any(a => a.StreamVideoId == stream.Id && a.User?.UserId == userId);

                if (streamTimestamp is not null || isCompleted is not null)
                    stream.ProgressState = new ProgressStateModel
                    {
                        InProgressTimestamp = streamTimestamp,
                        IsCompleted = isCompleted
                    };
            }
        }

        if (!admin) gameStreams.Streams = gameStreams.Streams.Where(w => w.IsPublished).ToList();

        return Ok(JsonConvert.SerializeObject(gameStreams, Formatting.Indented));
    }

    [HttpPost]
    [Route("games")]
    public async Task<IActionResult> CreateGame()
    {
        var form = await Request.ReadFormAsync();
        var files = form.Files;

        var gameTitle = form.FirstOrDefault(f => f.Key == "GameTitle");

        if (gameTitle.Key is null) return BadRequest("Ошибка при обработке данных на сервере");

        await using var db = await _database.CreateDbContextAsync();
        var games = db.Games.ToList();

        if (games.Any(
                a => string.Equals(a.Title, gameTitle.Value.ToString(), StringComparison.CurrentCultureIgnoreCase)))
            return BadRequest("Игра с таким названием уже существует");

        var newGame = new Game
        {
            Id = Guid.NewGuid(),
            Title = gameTitle.Value.ToString()
        };

        var thumbnailFile = files.FirstOrDefault(f => f.Name == "ThumbnailFile");

        if (thumbnailFile is not null)
        {
            var extension = Path.GetExtension(thumbnailFile.FileName);

            if (!Directory.Exists(ServerVariables.TempFolder)) Directory.CreateDirectory(ServerVariables.TempFolder);

            var tempFile = Path.Combine(ServerVariables.TempFolder, $"{newGame.Id.ToString()}{extension}");

            await using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await thumbnailFile.CopyToAsync(fs);
            }

            try
            {
                await _ffmpeg.GenerateGameThumbnails(new GameInfoModel
                {
                    Id = newGame.Id,
                    Title = newGame.Title
                }, tempFile);
            }
            catch (Exception)
            {
                return StatusCode(502, "Ошибка при конвертации файла превью игры");
            }

            System.IO.File.Delete(tempFile);
        }

        db.Games.Add(newGame);
        await db.SaveChangesAsync();

        ((MemoryCache)_cache).Clear();

        return Ok();
    }

    [HttpPut]
    [Route("games")]
    public async Task<IActionResult> UpdateGame()
    {
        var form = await Request.ReadFormAsync();
        var files = form.Files;

        var gamePair = form.FirstOrDefault(f => f.Key == "GameInfo");

        if (gamePair.Key is null) return BadRequest("Ошибка при обработке данных на сервере");

        var gameInfo = JsonConvert.DeserializeObject<GameInfoModel>(gamePair.Value.ToString());

        if (gameInfo is null) return StatusCode(502, "Ошибка при получении данных об игре");

        await using var db = await _database.CreateDbContextAsync();
        var game = db.Games.FirstOrDefault(f => f.Id == gameInfo.Id);

        if (game is null) return BadRequest("Игра с таким ID не найдена");

        game.Title = gameInfo.Title;

        var thumbnailFile = files.FirstOrDefault(f => f.Name == "ThumbnailFile");

        if (thumbnailFile is not null)
        {
            var extension = Path.GetExtension(thumbnailFile.FileName);

            if (!Directory.Exists(ServerVariables.TempFolder)) Directory.CreateDirectory(ServerVariables.TempFolder);

            var tempFile = Path.Combine(ServerVariables.TempFolder, $"{game.Id.ToString()}{extension}");

            await using (var fs = new FileStream(tempFile, FileMode.Create))
            {
                await thumbnailFile.CopyToAsync(fs);
            }

            try
            {
                await _ffmpeg.GenerateGameThumbnails(new GameInfoModel
                {
                    Id = game.Id,
                    Title = game.Title
                }, tempFile, true);
            }
            catch (Exception)
            {
                return StatusCode(502, "Ошибка при конвертации файла превью игры");
            }

            System.IO.File.Delete(tempFile);
        }

        await db.SaveChangesAsync();

        ((MemoryCache)_cache).Clear();

        return Ok();
    }

    [HttpDelete]
    [Route("games/{gameId}")]
    public async Task<IActionResult> DeleteGame(Guid gameId)
    {
        await using var db = await _database.CreateDbContextAsync();
        var game = db.Games.Include(i => i.StreamGames).FirstOrDefault(f => f.Id == gameId);

        if (game is null) return BadRequest("Игра с таким ID не найдена");

        db.Games.Remove(game);
        await db.SaveChangesAsync();

        ((MemoryCache)_cache).Clear();

        return Ok($"Игра с ID: {gameId} успешно удалена");
    }

    [HttpGet]
    [Route("users/{userId}/streams")]
    public async Task<IActionResult> GetUserStreams(string userId, [FromQuery] UserStreamType type,
        [FromQuery] bool admin)
    {
        await using var db = await _database.CreateDbContextAsync();

        User? user;
        List<StreamInfoModel> streams;

        switch (type)
        {
            case UserStreamType.NotViewed:
                user = await db.Users.FirstOrDefaultAsync(f => f.UserId == userId);
                var dbStreams = db.StreamVideos.Include(streamVideo => streamVideo.Completeds)
                    .ThenInclude(completed => completed.User).Include(streamVideo => streamVideo.InProgresses)
                    .ThenInclude(inProgress => inProgress.User).Include(streamVideo => streamVideo.StreamGames)
                    .ThenInclude(streamGame => streamGame.Game!).ToList();

                if (user is null) return BadRequest($"Пользователь с таким ID: {userId} не найден");

                streams = dbStreams
                    .Where(w => w.Completeds.All(a => a.User != user) && w.InProgresses.All(a => a.User != user))
                    .Select(s => new StreamInfoModel
                    {
                        Id = s.Id,
                        Title = s.Title,
                        StreamDate = s.StreamDate,
                        Duration = s.Duration,
                        Games = s.StreamGames.Where(w => w.StreamVideoId == s.Id).Select(l => new GameInfoModel
                        {
                            Id = l.GameId,
                            Title = l.Game!.Title,
                            GameTimestamp = l.GameTimestamp
                        }).ToList(),
                        ProgressState = new ProgressStateModel
                        {
                            InProgressTimestamp = null,
                            IsCompleted = false
                        },
                        IsPublished = s.IsPublished
                    }).ToList();
                break;
            case UserStreamType.InProgress:
                user = await db.Users.Include(u => u.StreamVideosInProgress)
                    .ThenInclude(streamVideo => streamVideo.StreamGames).ThenInclude(streamGame => streamGame.Game!)
                    .Include(i => i.StreamVideosInProgress).ThenInclude(streamVideo => streamVideo.InProgresses)
                    .ThenInclude(inProgress => inProgress.User)
                    .FirstOrDefaultAsync(f => f.UserId == userId);

                if (user is null) return BadRequest($"Пользователь с таким ID: {userId} не найден");

                streams = user.StreamVideosInProgress.Select(s => new StreamInfoModel
                {
                    Id = s.Id,
                    Title = s.Title,
                    StreamDate = s.StreamDate,
                    Duration = s.Duration,
                    Games = s.StreamGames.Where(w => w.StreamVideoId == s.Id).Select(l => new GameInfoModel
                    {
                        Id = l.GameId,
                        Title = l.Game!.Title,
                        GameTimestamp = l.GameTimestamp
                    }).ToList(),
                    ProgressState = new ProgressStateModel
                    {
                        InProgressTimestamp = s.InProgresses
                            .FirstOrDefault(f => f.User?.UserId == userId && f.StreamVideoId == s.Id)?.StopTimeInSeconds
                            .ToString(),
                        IsCompleted = false
                    },
                    IsPublished = s.IsPublished
                }).ToList();
                break;
            case UserStreamType.Completed:
                user = await db.Users.Include(i => i.StreamVideosCompleted)
                    .ThenInclude(streamVideo => streamVideo.StreamGames).ThenInclude(streamGame => streamGame.Game!)
                    .FirstOrDefaultAsync(f => f.UserId == userId);

                if (user is null) return BadRequest($"Пользователь с таким ID: {userId} не найден");

                streams = user.StreamVideosCompleted.Select(s => new StreamInfoModel
                {
                    Id = s.Id,
                    Title = s.Title,
                    StreamDate = s.StreamDate,
                    Duration = s.Duration,
                    Games = s.StreamGames.Where(w => w.StreamVideoId == s.Id).Select(l => new GameInfoModel
                    {
                        Id = l.GameId,
                        Title = l.Game!.Title,
                        GameTimestamp = l.GameTimestamp
                    }).ToList(),
                    ProgressState = new ProgressStateModel
                    {
                        InProgressTimestamp = null,
                        IsCompleted = true
                    },
                    IsPublished = s.IsPublished
                }).ToList();
                break;
            default:
                return BadRequest("Неверно указан тип");
        }

        if (!admin) streams = streams.Where(w => w.IsPublished).ToList();

        return Ok(JsonConvert.SerializeObject(streams, Formatting.Indented));
    }

    [HttpPut]
    [Route("timestamp/{userId}/{streamId}")]
    public async Task<IActionResult> UpdateUserStreamTimestamp(string userId, Guid streamId, [FromBody] int timestamp)
    {
        await using var db = await _database.CreateDbContextAsync();

        var user = db.Users.Include(i => i.InProgresses).Include(i => i.Completeds)
            .ThenInclude(completed => completed.StreamVideo).FirstOrDefault(f => f.UserId == userId);

        if (user is null)
        {
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                UserId = userId
            };

            db.Users.Add(newUser);
            await db.SaveChangesAsync();

            user = db.Users.Include(i => i.InProgresses).Include(i => i.Completeds)
                .ThenInclude(completed => completed.StreamVideo).FirstOrDefault(f => f.UserId == userId);
        }

        if (user is null) return BadRequest("Ошибка при получении или создании пользователя в базе");

        var completed = user.Completeds.FirstOrDefault(f => f.User?.UserId == userId && f.StreamVideoId == streamId);

        if (completed?.StreamVideo != null)
            user.StreamVideosCompleted.Remove(completed.StreamVideo);

        var inProgress =
            user.InProgresses.FirstOrDefault(f => f.User?.UserId == userId && f.StreamVideoId == streamId);

        if (inProgress is null)
        {
            var stream = db.StreamVideos.FirstOrDefault(f => f.Id == streamId);

            if (stream is null) return BadRequest($"Стрим с таким ID: {streamId} не найден");

            user.StreamVideosInProgress.Add(stream);

            await db.SaveChangesAsync();

            inProgress = user.InProgresses.FirstOrDefault(f => f.User?.UserId == userId && f.StreamVideoId == streamId);
        }

        if (inProgress is null) return BadRequest("Не удалось связать пользователя с стримом");

        inProgress.StopTimeInSeconds = timestamp;

        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete]
    [Route("timestamp/{userId}/{streamId}")]
    public async Task<IActionResult> DeleteUserStreamTimestamp(string userId, Guid streamId,
        [FromQuery] TimestampStates state)
    {
        await using var db = await _database.CreateDbContextAsync();

        var user = await db.Users.Include(user => user.InProgresses).Include(user => user.StreamVideosInProgress)
            .Include(user => user.StreamVideosCompleted).FirstOrDefaultAsync(f => f.UserId == userId);
        var stream = await db.StreamVideos.FirstOrDefaultAsync(f => f.Id == streamId);

        if (user is null) return BadRequest($"Пользователь с таким ID: {userId} не найден");
        if (stream is null) return BadRequest($"Стрим с таким ID: {streamId} не найден");

        bool streamInProgress;
        bool streamCompleted;

        switch (state)
        {
            case TimestampStates.Start:
                streamInProgress = user.StreamVideosInProgress.Contains(stream);
                streamCompleted = user.StreamVideosCompleted.Contains(stream);

                if (streamInProgress) user.StreamVideosInProgress.Remove(stream);
                if (streamCompleted) user.StreamVideosCompleted.Remove(stream);
                break;
            case TimestampStates.End:
                streamInProgress = user.StreamVideosInProgress.Contains(stream);
                streamCompleted = !user.StreamVideosCompleted.Contains(stream);

                if (streamInProgress) user.StreamVideosInProgress.Remove(stream);
                if (streamCompleted) user.StreamVideosCompleted.Add(stream);
                break;
            default:
                return BadRequest("Неправильно указано состояние");
        }

        if (streamInProgress || streamCompleted) await db.SaveChangesAsync();

        return Ok();
    }

    [HttpPatch]
    [Route("scan")]
    public Task<IActionResult> ScanStreamFolder()
    {
        _ = _scanService.CheckStreamFolder();
        return Task.FromResult<IActionResult>(Ok());
    }
}