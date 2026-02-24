using Microsoft.EntityFrameworkCore;

namespace StatsServer;

public class StatisticData
{
    public int Id { get; set; }
    public DateTime StartTime { get; set; }
    public string WorkMode { get; set; } = "";
    public string ElevationResult { get; set; } = "";
    public bool DownloadResult { get; set; } 
    public string DownloadError { get; set; } = "";
    public bool LaunchResult { get; set; } 
    public DateTime ReceivedAt { get; set; }
    public string RawData { get; set; } = "";
}

public class AppDbContext : DbContext
{
    public DbSet<StatisticData> Statistics { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=statistics.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StatisticData>()
            .ToTable("Statistics");
    }
}