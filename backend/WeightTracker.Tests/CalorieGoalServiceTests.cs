using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.Tests;

public class CalorieGoalServiceTests
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
        var service = new CalorieGoalService(db);

        var result = await service.GetActiveAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsMostRecentGoal()
    {
        using var db = CreateDb();
        db.CalorieGoals.AddRange(
            new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new CalorieGoal { TargetCalories = 1800, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = new CalorieGoalService(db);

        var result = await service.GetActiveAsync();

        Assert.NotNull(result);
        Assert.Equal(1800, result.TargetCalories);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsGoalsOrderedByCreatedAtDesc()
    {
        using var db = CreateDb();
        db.CalorieGoals.AddRange(
            new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new CalorieGoal { TargetCalories = 1800, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();
        var service = new CalorieGoalService(db);

        var result = await service.GetAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal(1800, result[0].TargetCalories);
        Assert.Equal(2000, result[1].TargetCalories);
    }

    [Fact]
    public async Task CreateAsync_CreatesGoal()
    {
        using var db = CreateDb();
        var service = new CalorieGoalService(db);

        var result = await service.CreateAsync(new CreateCalorieGoalDto(2000));

        Assert.Equal(2000, result.TargetCalories);
        Assert.Equal(1, await db.CalorieGoals.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ReplacesExistingGoalCreatedToday()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new CalorieGoalService(db);

        await service.CreateAsync(new CreateCalorieGoalDto(1800));

        Assert.Equal(1, await db.CalorieGoals.CountAsync());
        Assert.Equal(1800, (await db.CalorieGoals.FirstAsync()).TargetCalories);
    }

    [Fact]
    public async Task CreateAsync_DoesNotReplaceGoalFromPreviousDay()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
        var service = new CalorieGoalService(db);

        await service.CreateAsync(new CreateCalorieGoalDto(1800));

        Assert.Equal(2, await db.CalorieGoals.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
    {
        using var db = CreateDb();
        var service = new CalorieGoalService(db);

        var result = await service.DeleteAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_RemovesGoal()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { Id = 1, TargetCalories = 2000 });
        await db.SaveChangesAsync();
        var service = new CalorieGoalService(db);

        var deleted = await service.DeleteAsync(1);

        Assert.True(deleted);
        Assert.Equal(0, await db.CalorieGoals.CountAsync());
    }
}
