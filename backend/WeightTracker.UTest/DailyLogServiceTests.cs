using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class DailyLogServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoLogs()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);

        var result = await service.GetAllAsync(null, null, null);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsLogsOrderedByDateDesc()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 74m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-12", 74.5m, null, null));

        var result = await service.GetAllAsync(null, null, null);

        Assert.Equal("2024-01-15", result[0].Date);
        Assert.Equal("2024-01-12", result[1].Date);
        Assert.Equal("2024-01-10", result[2].Date);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByDateRange()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 74m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-20", 73m, null, null));

        var result = await service.GetAllAsync(
            new DateOnly(2024, 1, 12),
            new DateOnly(2024, 1, 18),
            null);

        Assert.Single(result);
        Assert.Equal("2024-01-15", result[0].Date);
    }

    [Fact]
    public async Task GetAllAsync_RespectsLimit()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-11", 74m, null, null));
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-12", 73m, null, null));

        var result = await service.GetAllAsync(null, null, 2);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task UpsertAsync_CreatesNewLog()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);

        var result = await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, 2000, null));

        Assert.Equal("2024-01-15", result.Date);
        Assert.Equal(75.5m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingLog()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, null, null));

        var result = await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", null, 2000, null));

        Assert.Equal(75.5m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
    }

    [Fact]
    public async Task UpsertAsync_DoesNotOverwriteExistingFieldsWithNull()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, 2000, null));

        var result = await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", null, null, null));

        Assert.Equal(75.5m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
    }

    [Fact]
    public async Task DeleteDayAsync_ReturnsFalse_WhenNotFound()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);

        var result = await service.DeleteDayAsync(new DateOnly(2024, 1, 15));

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteDayAsync_RemovesLog()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, 2000, null));

        var deleted = await service.DeleteDayAsync(new DateOnly(2024, 1, 15));
        var log = await service.GetByDateAsync(new DateOnly(2024, 1, 15));

        Assert.True(deleted);
        Assert.Null(log);
    }

    [Fact]
    public async Task DeleteWeightAsync_ReturnsNull_WhenLogNotFound()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);

        var result = await service.DeleteWeightAsync(new DateOnly(2024, 1, 15));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteWeightAsync_NullsWeight_WhenCaloriesExist()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, 2000, null));

        var result = await service.DeleteWeightAsync(new DateOnly(2024, 1, 15));

        Assert.NotNull(result);
        Assert.Null(result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
    }

    [Fact]
    public async Task DeleteWeightAsync_RemovesRow_WhenCaloriesAlsoNull()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, null, null));

        var result = await service.DeleteWeightAsync(new DateOnly(2024, 1, 15));

        Assert.Null(result);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteCaloriesAsync_ReturnsNull_WhenLogNotFound()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);

        var result = await service.DeleteCaloriesAsync(new DateOnly(2024, 1, 15));

        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteCaloriesAsync_NullsCalories_WhenWeightExists()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 75.5m, 2000, null));

        var result = await service.DeleteCaloriesAsync(new DateOnly(2024, 1, 15));

        Assert.NotNull(result);
        Assert.Equal(75.5m, result.WeightKg);
        Assert.Null(result.CaloriesKcal);
    }

    [Fact]
    public async Task DeleteCaloriesAsync_RemovesRow_WhenWeightAlsoNull()
    {
        using var db = CreateDb();
        var service = new DailyLogService(db);
        await service.UpsertAsync(new UpsertDailyLogDto("2024-01-15", null, 2000, null));

        var result = await service.DeleteCaloriesAsync(new DateOnly(2024, 1, 15));

        Assert.Null(result);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }
}
