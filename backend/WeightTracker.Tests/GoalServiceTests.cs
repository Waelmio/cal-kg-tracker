using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.Tests;

public class GoalServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsNull_WhenNoGoals()
    {
        using var db = CreateDb();
        var service = new GoalService(db);

        var result = await service.GetActiveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsMostRecentGoal()
    {
        using var db = CreateDb();
        db.Goals.AddRange(
            new Goal
            {
                TargetWeightKg = 70m,
                TargetDate = new DateOnly(2025, 1, 1),
                StartDate = new DateOnly(2024, 1, 1),
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new Goal
            {
                TargetWeightKg = 65m,
                TargetDate = new DateOnly(2025, 6, 1),
                StartDate = new DateOnly(2024, 1, 1),
                CreatedAt = DateTime.UtcNow
            }
        );
        await db.SaveChangesAsync();
        var service = new GoalService(db);

        var result = await service.GetActiveAsync();

        Assert.NotNull(result);
        Assert.Equal(65m, result.TargetWeightKg);
    }

    [Fact]
    public async Task CreateAsync_SetsStartingWeightFromLatestLog()
    {
        using var db = CreateDb();
        db.DailyLogs.AddRange(
            new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 80m },
            new DailyLog { Date = new DateOnly(2024, 1, 15), WeightKg = 79m }
        );
        await db.SaveChangesAsync();
        var service = new GoalService(db);

        var result = await service.CreateAsync(new CreateGoalDto(70m, new DateOnly(2025, 1, 1), null));

        Assert.Equal(79m, result.StartingWeightKg);
        Assert.Equal(70m, result.TargetWeightKg);
    }

    [Fact]
    public async Task CreateAsync_SetsNullStartingWeight_WhenNoLogs()
    {
        using var db = CreateDb();
        var service = new GoalService(db);

        var result = await service.CreateAsync(new CreateGoalDto(70m, new DateOnly(2025, 1, 1), null));

        Assert.Null(result.StartingWeightKg);
    }

    [Fact]
    public async Task CreateAsync_PersistsGoalToDatabase()
    {
        using var db = CreateDb();
        var service = new GoalService(db);

        await service.CreateAsync(new CreateGoalDto(70m, new DateOnly(2025, 1, 1), "Lose weight"));

        Assert.Equal(1, await db.Goals.CountAsync());
        var goal = await db.Goals.FirstAsync();
        Assert.Equal(70m, goal.TargetWeightKg);
        Assert.Equal("Lose weight", goal.Notes);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        using var db = CreateDb();
        var service = new GoalService(db);

        var result = await service.DeleteAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGoal()
    {
        using var db = CreateDb();
        db.Goals.Add(new Goal
        {
            Id = 1,
            TargetWeightKg = 70m,
            TargetDate = new DateOnly(2025, 1, 1),
            StartDate = new DateOnly(2024, 1, 1)
        });
        await db.SaveChangesAsync();
        var service = new GoalService(db);

        var deleted = await service.DeleteAsync(1);

        Assert.True(deleted);
        Assert.Equal(0, await db.Goals.CountAsync());
    }
}
