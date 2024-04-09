using Microsoft.EntityFrameworkCore;
using TowaCinema.Server.Db.DbModels;

namespace TowaCinema.Server.Db.Context;

public sealed class CinemaDbContext : DbContext
{
    public CinemaDbContext()
    {
        Database.Migrate();
    }

    public DbSet<StreamVideo> StreamVideos { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Game> Games { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Path.Combine(ServerVariables.DatabaseFolder, "StreamVideos.db")}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StreamVideo>()
            .HasMany(h => h.UsersInProgress)
            .WithMany(m => m.StreamVideosInProgress)
            .UsingEntity<InProgress>();

        modelBuilder.Entity<StreamVideo>()
            .HasMany(h => h.UsersCompleted)
            .WithMany(m => m.StreamVideosCompleted)
            .UsingEntity<Completed>();

        modelBuilder.Entity<StreamVideo>()
            .HasMany(h => h.Games)
            .WithMany(m => m.StreamVideos)
            .UsingEntity<StreamGame>();
    }
}