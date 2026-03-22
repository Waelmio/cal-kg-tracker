using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
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

    private static DailyLogService CreateService(AppDbContext db) =>
        new(db, new CalorieLogService(db));

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoLogs()
    {
        using var db = CreateDb();
        var result = await CreateService(db).GetAllAsync(null, null, null);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsLogsOrderedByDateDesc()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 74m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-12", 74.5m, null, null));

        var result = await svc.GetAllAsync(null, null, null);

        Assert.Equal(3, result.Count);
        Assert.Equal("2024-01-15", result[0].Date);
        Assert.Equal("2024-01-12", result[1].Date);
        Assert.Equal("2024-01-10", result[2].Date);
    }

    [Fact]
    public async Task GetAllAsync_FiltersFromDate()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 74m, null, null));

        var result = await svc.GetAllAsync(new DateOnly(2024, 1, 12), null, null);

        Assert.Single(result);
        Assert.Equal("2024-01-15", result[0].Date);
    }

    [Fact]
    public async Task GetAllAsync_FiltersToDate()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-15", 74m, null, null));

        var result = await svc.GetAllAsync(null, new DateOnly(2024, 1, 12), null);

        Assert.Single(result);
        Assert.Equal("2024-01-10", result[0].Date);
    }

    [Fact]
    public async Task GetAllAsync_RespectsLimit()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-11", 74.5m, null, null));
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-12", 74m, null, null));

        var result = await svc.GetAllAsync(null, null, 2);

        Assert.Equal(2, result.Count);
        Assert.Equal("2024-01-12", result[0].Date);
        Assert.Equal("2024-01-11", result[1].Date);
    }

    [Fact]
    public async Task GetAllAsync_IncludesCalorieTarget()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", null, 2000, null));

        var result = await svc.GetAllAsync(null, null, null);

        Assert.Equal(1800, result[0].CalorieTarget);
    }

    // ── GetByDateAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByDateAsync_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDb();
        var result = await CreateService(db).GetByDateAsync(new DateOnly(2024, 1, 10));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByDateAsync_ReturnsDto_WithCalorieTarget()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, 2000, null));

        var result = await svc.GetByDateAsync(date);

        Assert.NotNull(result);
        Assert.Equal("2024-01-10", result.Date);
        Assert.Equal(75m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
        Assert.Equal(1800, result.CalorieTarget);
    }

    // ── UpsertAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_CreatesNewLog()
    {
        using var db = CreateDb();
        var result = await CreateService(db).UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, 2000, null));

        Assert.Equal("2024-01-10", result.Date);
        Assert.Equal(75m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingLog()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));

        var result = await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 74m, 2000, null));

        Assert.Equal(74m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task UpsertAsync_DoesNotOverwriteExistingWeight_WhenOnlyCaloriesProvided()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));

        var result = await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", null, 2000, null));

        Assert.Equal(75m, result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
    }

    // ── DeleteDayAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDayAsync_ReturnsFalse_WhenNotFound()
    {
        using var db = CreateDb();
        var result = await CreateService(db).DeleteDayAsync(new DateOnly(2024, 1, 10));
        Assert.False(result);
    }

    [Fact]
    public async Task DeleteDayAsync_DeletesLog_ReturnsTrue()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));

        var result = await svc.DeleteDayAsync(new DateOnly(2024, 1, 10));

        Assert.True(result);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    // ── DeleteWeightAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteWeightAsync_ReturnsNull_WhenLogNotFound()
    {
        using var db = CreateDb();
        var result = await CreateService(db).DeleteWeightAsync(new DateOnly(2024, 1, 10));
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteWeightAsync_NullsWeight_KeepsLog_WhenCaloriesRemain()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, 2000, null));

        var result = await svc.DeleteWeightAsync(new DateOnly(2024, 1, 10));

        Assert.NotNull(result);
        Assert.Null(result.WeightKg);
        Assert.Equal(2000, result.CaloriesKcal);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteWeightAsync_DeletesRow_WhenBothFieldsBecomeNull()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));

        var result = await svc.DeleteWeightAsync(new DateOnly(2024, 1, 10));

        Assert.Null(result);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteWeightAsync_KeepsRow_WhenCheatDay_EvenIfBothFieldsNull()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        var result = await svc.DeleteWeightAsync(new DateOnly(2024, 1, 10));

        Assert.NotNull(result);
        Assert.True(result.IsCheatDay);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    // ── DeleteCaloriesAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task DeleteCaloriesAsync_ReturnsNull_WhenLogNotFound()
    {
        using var db = CreateDb();
        var result = await CreateService(db).DeleteCaloriesAsync(new DateOnly(2024, 1, 10));
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteCaloriesAsync_NullsCalories_KeepsLog_WhenWeightRemains()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, 2000, null));

        var result = await svc.DeleteCaloriesAsync(new DateOnly(2024, 1, 10));

        Assert.NotNull(result);
        Assert.Equal(75m, result.WeightKg);
        Assert.Null(result.CaloriesKcal);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteCaloriesAsync_DeletesRow_WhenBothFieldsBecomeNull()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", null, 2000, null));

        var result = await svc.DeleteCaloriesAsync(new DateOnly(2024, 1, 10));

        Assert.Null(result);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task DeleteCaloriesAsync_KeepsRow_WhenCheatDay_EvenIfBothFieldsNull()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", null, 2000, null));
        await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        var result = await svc.DeleteCaloriesAsync(new DateOnly(2024, 1, 10));

        Assert.NotNull(result);
        Assert.True(result.IsCheatDay);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    // ── SetCheatDayAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetCheatDayAsync_SetsCheatDay_OnExistingLog()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));

        var result = await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        Assert.True(result.IsCheatDay);
    }

    [Fact]
    public async Task SetCheatDayAsync_CreatesNewRow_WhenLogDoesNotExist()
    {
        using var db = CreateDb();
        var result = await CreateService(db).SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        Assert.True(result.IsCheatDay);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task SetCheatDayAsync_RemovesCheatDay_DeletesRow_WhenNoOtherData()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        var result = await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), false);

        Assert.False(result.IsCheatDay);
        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task SetCheatDayAsync_RemovesCheatDay_KeepsRow_WhenWeightExists()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(new UpsertDailyLogDto("2024-01-10", 75m, null, null));
        await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), true);

        var result = await svc.SetCheatDayAsync(new DateOnly(2024, 1, 10), false);

        Assert.False(result.IsCheatDay);
        Assert.Equal(75m, result.WeightKg);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task SetCheatDayAsync_EffectiveTarget_UsesTdee_WhenCheatDay()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        db.UserSettings.Add(new UserSettings { Id = 1, TdeeKcal = 2500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).SetCheatDayAsync(date, true);

        Assert.Equal(2500, result.CalorieTarget);
    }
}
