using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class CalorieLogServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CalorieLogService CreateService(AppDbContext db) => new(db);

    // ── GetEnrichedAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetEnrichedAsync_ReturnsEmpty_WhenNoLogs()
    {
        using var db = CreateDb();
        var result = await CreateService(db).GetEnrichedAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEnrichedAsync_ReturnsLogsOrderedByDateDesc()
    {
        using var db = CreateDb();
        db.DailyLogs.AddRange(
            new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 80m },
            new DailyLog { Date = new DateOnly(2024, 1, 15), WeightKg = 79m },
            new DailyLog { Date = new DateOnly(2024, 1, 12), WeightKg = 79.5m }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(new DateOnly(2024, 1, 15), result[0].Log.Date);
        Assert.Equal(new DateOnly(2024, 1, 12), result[1].Log.Date);
        Assert.Equal(new DateOnly(2024, 1, 10), result[2].Log.Date);
    }

    [Fact]
    public async Task GetEnrichedAsync_FiltersFromDate()
    {
        using var db = CreateDb();
        db.DailyLogs.AddRange(
            new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 80m },
            new DailyLog { Date = new DateOnly(2024, 1, 15), WeightKg = 79m }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync(from: new DateOnly(2024, 1, 12));

        Assert.Single(result);
        Assert.Equal(new DateOnly(2024, 1, 15), result[0].Log.Date);
    }

    [Fact]
    public async Task GetEnrichedAsync_FiltersToDate()
    {
        using var db = CreateDb();
        db.DailyLogs.AddRange(
            new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 80m },
            new DailyLog { Date = new DateOnly(2024, 1, 15), WeightKg = 79m }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync(to: new DateOnly(2024, 1, 12));

        Assert.Single(result);
        Assert.Equal(new DateOnly(2024, 1, 10), result[0].Log.Date);
    }

    [Fact]
    public async Task GetEnrichedAsync_GoalTarget_IsNull_WhenNoCalorieGoalsExist()
    {
        using var db = CreateDb();
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2024, 1, 10), CaloriesKcal = 2000 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Null(result[0].GoalTarget);
        Assert.Null(result[0].EffectiveTarget);
    }

    [Fact]
    public async Task GetEnrichedAsync_GoalTarget_UsesGoalActiveOnThatDate_NotLatest()
    {
        using var db = CreateDb();
        var jan10 = new DateOnly(2024, 1, 10);
        var jan20 = new DateOnly(2024, 1, 20);

        db.DailyLogs.AddRange(
            new DailyLog { Date = jan10, CaloriesKcal = 2000 },
            new DailyLog { Date = jan20, CaloriesKcal = 1800 }
        );
        // Goal A was active on jan10, Goal B replaced it on jan15
        db.CalorieGoals.AddRange(
            new CalorieGoal { TargetCalories = 2200, CreatedAt = jan10.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) },
            new CalorieGoal { TargetCalories = 1900, CreatedAt = jan20.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        var jan20Entry = result.First(e => e.Log.Date == jan20);
        var jan10Entry = result.First(e => e.Log.Date == jan10);
        Assert.Equal(1900, jan20Entry.GoalTarget);
        Assert.Equal(2200, jan10Entry.GoalTarget);
    }

    [Fact]
    public async Task GetEnrichedAsync_GoalTarget_IsNull_WhenLogPredatesAllGoals()
    {
        using var db = CreateDb();
        var jan1 = new DateOnly(2024, 1, 1);
        var feb1 = new DateOnly(2024, 2, 1);

        db.DailyLogs.Add(new DailyLog { Date = jan1, CaloriesKcal = 2000 });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = feb1.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Null(result[0].GoalTarget);
    }

    [Fact]
    public async Task GetEnrichedAsync_EffectiveTarget_EqualGoalTarget_ForRegularDay()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.DailyLogs.Add(new DailyLog { Date = date, CaloriesKcal = 2000 });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Equal(1800, result[0].GoalTarget);
        Assert.Equal(1800, result[0].EffectiveTarget);
    }

    [Fact]
    public async Task GetEnrichedAsync_EffectiveTarget_UsesTdee_ForCheatDay()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.DailyLogs.Add(new DailyLog { Date = date, CaloriesKcal = 2800, IsCheatDay = true });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        db.UserSettings.Add(new UserSettings { Id = 1, TdeeKcal = 2500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Equal(1800, result[0].GoalTarget);    // goal target unchanged
        Assert.Equal(2500, result[0].EffectiveTarget); // but effective = TDEE
    }

    [Fact]
    public async Task GetEnrichedAsync_EffectiveTarget_IsNull_ForCheatDay_WhenNoTdeeSet()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.DailyLogs.Add(new DailyLog { Date = date, CaloriesKcal = 2800, IsCheatDay = true });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        // No UserSettings seeded → TdeeKcal = null
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEnrichedAsync();

        Assert.Null(result[0].EffectiveTarget);
    }

    // ── EnrichLogsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task EnrichLogsAsync_ReturnsEmpty_WhenListIsEmpty()
    {
        using var db = CreateDb();
        var result = await CreateService(db).EnrichLogsAsync([]);
        Assert.Empty(result);
    }

    [Fact]
    public async Task EnrichLogsAsync_EnrichesWithActiveGoal()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        var log = new DailyLog { Date = date, CaloriesKcal = 2000 };
        db.DailyLogs.Add(log);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).EnrichLogsAsync([log]);

        Assert.Single(result);
        Assert.Equal(1800, result[0].GoalTarget);
        Assert.Equal(1800, result[0].EffectiveTarget);
    }

    // ── GetEffectiveTargetAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetEffectiveTargetAsync_ReturnsNull_WhenNoGoalExists()
    {
        using var db = CreateDb();
        var result = await CreateService(db).GetEffectiveTargetAsync(new DateOnly(2024, 1, 10), false);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetEffectiveTargetAsync_ReturnsActiveGoalTarget_ForRegularDay()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEffectiveTargetAsync(date, false);

        Assert.Equal(1800, result);
    }

    [Fact]
    public async Task GetEffectiveTargetAsync_ReturnsTdee_ForCheatDay()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        db.UserSettings.Add(new UserSettings { Id = 1, TdeeKcal = 2500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEffectiveTargetAsync(date, isCheatDay: true);

        Assert.Equal(2500, result);
    }

    [Fact]
    public async Task GetEffectiveTargetAsync_ReturnsNull_ForCheatDay_WhenNoTdeeSet()
    {
        using var db = CreateDb();
        var date = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEffectiveTargetAsync(date, isCheatDay: true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEffectiveTargetAsync_ReturnsNull_WhenGoalPostdatesTheDate()
    {
        using var db = CreateDb();
        var logDate = new DateOnly(2024, 1, 5);
        var goalDate = new DateOnly(2024, 1, 10);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = goalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetEffectiveTargetAsync(logDate, false);

        Assert.Null(result);
    }
}
