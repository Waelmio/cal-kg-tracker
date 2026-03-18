using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<DailyLog> DailyLogs => Set<DailyLog>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<CalorieGoal> CalorieGoals => Set<CalorieGoal>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DailyLog>()
            .HasIndex(e => e.Date)
            .IsUnique();

        modelBuilder.Entity<UserSettings>()
            .HasData(new UserSettings { Id = 1, PreferredUnit = "kg", HeightCm = null });
    }
}
