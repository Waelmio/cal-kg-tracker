using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class DashboardServiceTests
{
    // Mirror the same "today" the service uses so seeds are correctly positioned.
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

    private static DateOnly WeekStart()
    {
        var daysFromMonday = ((int)Today.DayOfWeek + 6) % 7;
        return Today.AddDays(-daysFromMonday);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DashboardService CreateService(AppDbContext db) =>
        new(db, new CalorieLogService(db));

    // DashboardService calls UserSettings.FirstAsync() — every test must seed it.
    private static async Task SeedSettingsAsync(AppDbContext db, int? heightCm = null, int? tdeeKcal = null)
    {
        db.UserSettings.Add(new UserSettings { Id = 1, HeightCm = heightCm, TdeeKcal = tdeeKcal });
        await db.SaveChangesAsync();
    }

    // ── Basics ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsZerosAndNulls_WhenNoLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(0, result.TotalEntries);
        Assert.Null(result.CurrentWeightKg);
        Assert.Null(result.TodayCaloriesKcal);
        Assert.Null(result.DailyCalorieTarget);
        Assert.Null(result.AvgWeight7Days);
        Assert.Null(result.WeeklyAvgCalories);
        Assert.Equal(0, result.CalorieStreakDays);
    }

    [Fact]
    public async Task GetAsync_TotalEntries_ReflectsLogCount()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, WeightKg = 80m },
            new DailyLog { Date = Today.AddDays(-1), WeightKg = 80.5m }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(2, result.TotalEntries);
    }

    // ── Current weight & today's data ─────────────────────────────────────────

    [Fact]
    public async Task GetAsync_CurrentWeightKg_IsMostRecentWeightLog()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today.AddDays(-5), WeightKg = 82m },
            new DailyLog { Date = Today.AddDays(-1), WeightKg = 80m }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(80m, result.CurrentWeightKg);
    }

    [Fact]
    public async Task GetAsync_TodayCaloriesKcal_ReflectsTodayLog()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 1800 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(1800, result.TodayCaloriesKcal);
    }

    [Fact]
    public async Task GetAsync_DailyCalorieTarget_FromActiveGoal_WhenNoTodayLog()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 2000,
            CreatedAt = Today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(2000, result.DailyCalorieTarget);
    }

    [Fact]
    public async Task GetAsync_DailyCalorieTarget_UsesTdee_ForCheatDay()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db, tdeeKcal: 2500);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 1800,
            CreatedAt = Today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        db.DailyLogs.Add(new DailyLog { Date = Today, IsCheatDay = true });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(2500, result.DailyCalorieTarget);
    }

    // ── BMI ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_Bmi_ComputedFromWeightAndHeight()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db, heightCm: 180);
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 81m }); // 81 / 1.80² = 25.0
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(25.0, result.Bmi);
    }

    [Fact]
    public async Task GetAsync_Bmi_IsNull_WhenNoHeightSet()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db); // no height
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 80m });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.Bmi);
    }

    // ── 7-day weight stats ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_AvgWeight7Days_AveragesLast7Days_ExcludesOlderLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, WeightKg = 80m },            // in window
            new DailyLog { Date = Today.AddDays(-3), WeightKg = 78m }, // in window
            new DailyLog { Date = Today.AddDays(-10), WeightKg = 90m } // outside window — excluded
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(79m, result.AvgWeight7Days); // (80 + 78) / 2
    }

    [Fact]
    public async Task GetAsync_AvgWeight7DaysTrend_ComparesToPrev7Days()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, WeightKg = 79m },            // last 7
            new DailyLog { Date = Today.AddDays(-8), WeightKg = 81m }  // prev 7 (7–13 days ago)
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(-2m, result.AvgWeight7DaysTrend); // 79 − 81
    }

    [Fact]
    public async Task GetAsync_AvgWeight7DaysTrend_IsNull_WhenNoPrev7DaysData()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 79m });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.AvgWeight7DaysTrend);
    }

    // ── 30-day weight trend ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WeightTrend30Days_ComparesToRef30Window()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, WeightKg = 79m },             // last-7 avg = 79
            new DailyLog { Date = Today.AddDays(-30), WeightKg = 82m } // ref-30 window (27–33 days ago)
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(-3m, result.WeightTrend30Days);           // 79 − 82
        Assert.Equal(-0.7m, result.WeightChangeRateKgPerWeek); // −3/30×7
    }

    [Fact]
    public async Task GetAsync_WeightTrend30Days_IsNull_WhenNoRef30Data()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 79m });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.WeightTrend30Days);
    }

    // ── Weekly calorie stats ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_WeeklyAvgCalories_AveragesThisWeeksCalorieLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 1800 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(1800, result.WeeklyAvgCalories);
        Assert.Equal(1, result.WeeklyCalorieDeficitDays);
    }

    [Fact]
    public async Task GetAsync_WeeklyCalorieDeficit_SumsTargetMinusActual()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 2000,
            CreatedAt = Today.AddDays(-7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 1500 }); // deficit 500
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(500, result.WeeklyCalorieDeficit);
    }

    [Fact]
    public async Task GetAsync_WeeklyCalorieDeficit_IsNull_WhenNoCalorieGoal()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 1800 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.WeeklyCalorieDeficit);
    }

    [Fact]
    public async Task GetAsync_WeeklyCalorieDeficit_CheatDayUsesTdeeAsTarget()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db, tdeeKcal: 2500);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 1800,
            CreatedAt = Today.AddDays(-7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        // Cheat day: effective target = TDEE (2500), not regular goal (1800)
        // Deficit = 2500 − 2300 = 200, not 1800 − 2300 = −500
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 2300, IsCheatDay = true });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(200, result.WeeklyCalorieDeficit);
    }

    // ── Overall calorie deficit ───────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_OverallCalorieDeficit_SumsSinceWeightGoalStartDate()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal
        {
            TargetWeightKg = 70m,
            TargetDate = Today.AddDays(90),
            StartDate = Today.AddDays(-7)
        });
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 2000,
            CreatedAt = Today.AddDays(-8).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today.AddDays(-5), CaloriesKcal = 1500 },  // deficit 500 ✓
            new DailyLog { Date = Today.AddDays(-3), CaloriesKcal = 1800 },  // deficit 200 ✓
            new DailyLog { Date = Today.AddDays(-10), CaloriesKcal = 1500 }  // before start — excluded
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(700, result.OverallCalorieDeficit);
        Assert.Equal(2, result.OverallCalorieDeficitDays);
    }

    [Fact]
    public async Task GetAsync_OverallCalorieDeficit_IsNull_WhenNoWeightGoal()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-1), CaloriesKcal = 1500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.OverallCalorieDeficit);
    }

    [Fact]
    public async Task GetAsync_OverallCalorieDeficit_IsNull_WhenNoCalorieGoalExistedForThoseLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal { TargetWeightKg = 70m, TargetDate = Today.AddDays(90), StartDate = Today.AddDays(-7) });
        // No CalorieGoal seeded — EffectiveTarget will be null for all logs
        db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-3), CaloriesKcal = 1500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.OverallCalorieDeficit);
        Assert.Equal(0, result.OverallCalorieDeficitDays);
    }

    [Fact]
    public async Task GetAsync_OverallCalorieDeficit_UsesHistoricalGoalPerDate_NotCurrentGoal()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal { TargetWeightKg = 70m, TargetDate = Today.AddDays(90), StartDate = Today.AddDays(-10) });
        // Goal A (2000 kcal) was active first; Goal B (1500 kcal) replaced it 3 days ago.
        db.CalorieGoals.AddRange(
            new CalorieGoal { TargetCalories = 2000, CreatedAt = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) },
            new CalorieGoal { TargetCalories = 1500, CreatedAt = Today.AddDays(-3).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) }
        );
        db.DailyLogs.AddRange(
            // 7 days ago: Goal A was active → deficit = 2000 − 1800 = 200
            new DailyLog { Date = Today.AddDays(-7), CaloriesKcal = 1800 },
            // 1 day ago: Goal B is active → deficit = 1500 − 1200 = 300
            new DailyLog { Date = Today.AddDays(-1), CaloriesKcal = 1200 }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        // If the service incorrectly used the current goal (B=1500) for all logs:
        // deficit would be (1500−1800) + (1500−1200) = −300 + 300 = 0
        // Correct answer uses the goal active on each date: 200 + 300 = 500
        Assert.Equal(500, result.OverallCalorieDeficit);
    }

    // ── Goal progress ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_GoalProgress_IsNull_WhenNoGoal()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 79m });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.GoalProgressPercent);
        Assert.Null(result.KgToGoal);
        Assert.Null(result.ActiveGoal);
    }

    [Fact]
    public async Task GetAsync_GoalProgress_ComputedFromCurrentWeight()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal
        {
            TargetWeightKg = 70m,
            TargetDate = Today.AddDays(90),
            StartDate = Today.AddDays(-30),
            StartingWeightKg = 80m
        });
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 75m }); // 5 kg lost of 10 kg goal
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(50.0, result.GoalProgressPercent); // (80−75)/(80−70)×100
        Assert.Equal(5m, result.KgToGoal);              // 75−70
    }

    [Fact]
    public async Task GetAsync_KgToGoal_IsZero_WhenWeightAtOrBelowTarget()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal { TargetWeightKg = 80m, TargetDate = Today.AddDays(90), StartDate = Today.AddDays(-30) });
        db.DailyLogs.Add(new DailyLog { Date = Today, WeightKg = 79m }); // already below target
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(0m, result.KgToGoal);
        Assert.Null(result.ProjectedGoalDate); // kgToGoal=0 so no projection
    }

    [Fact]
    public async Task GetAsync_ProjectedGoalDate_ExtrapolatedFromWeeklyWeightRate()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.Goals.Add(new Goal { TargetWeightKg = 70m, TargetDate = Today.AddDays(180), StartDate = Today.AddDays(-30) });
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, WeightKg = 79m },           // latest
            new DailyLog { Date = Today.AddDays(-8), WeightKg = 81m } // ref 7+ days ago → pointShift7 = −2
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        // kgToGoal = 79−70 = 9; rate = −2/7 per day → ceil(9/(2/7)) = ceil(31.5) = 32
        Assert.Equal(Today.AddDays(32), result.ProjectedGoalDate);
    }

    // ── Calorie streak ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_CalorieStreak_IsZero_WhenNoCalorieLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2000, CreatedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(0, result.CalorieStreakDays);
    }

    [Fact]
    public async Task GetAsync_CalorieStreak_IsZero_WhenNoCalorieGoal()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today, CaloriesKcal = 1500 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(0, result.CalorieStreakDays);
    }

    [Fact]
    public async Task GetAsync_CalorieStreak_CountsCumulativeDeficitDays()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 2000,
            CreatedAt = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        // Algorithm processes newest-first. Running totals:
        // Day 0: 1500/2000 → streak=1
        // Day−1: 3000/4000 → streak=2
        // Day−2: 4500/6000 → streak=3
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, CaloriesKcal = 1500 },
            new DailyLog { Date = Today.AddDays(-1), CaloriesKcal = 1500 },
            new DailyLog { Date = Today.AddDays(-2), CaloriesKcal = 1500 }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(3, result.CalorieStreakDays);
        Assert.Null(result.CalorieStreakNextDays);
        Assert.Equal(0, result.CalorieStreakNextExcessKcal);
    }

    [Fact]
    public async Task GetAsync_CalorieStreak_BreaksWhenCumulativeSurplusOccurs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 2000,
            CreatedAt = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        // Day 0:  1500 → running: 1500/2000 → streak=1
        // Day−1:  1500 → running: 3000/4000 → streak=2
        // Day−2:  3500 → running: 6500/6000 → BREAK → nextDays=3, excess=500
        // Day−3:  1500 → running: 8000/8000 → 8000−500=7500 < 8000 → nextDays=4
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, CaloriesKcal = 1500 },
            new DailyLog { Date = Today.AddDays(-1), CaloriesKcal = 1500 },
            new DailyLog { Date = Today.AddDays(-2), CaloriesKcal = 3500 },
            new DailyLog { Date = Today.AddDays(-3), CaloriesKcal = 1500 }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(2, result.CalorieStreakDays);
        Assert.Equal(4, result.CalorieStreakNextDays);
        Assert.Equal(500, result.CalorieStreakNextExcessKcal);
    }

    [Fact]
    public async Task GetAsync_CalorieStreak_CheatDayUsesTdeeAsTarget()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db, tdeeKcal: 2500);
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = 1800,
            CreatedAt = Today.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        // Day 0 (cheat): target=TDEE=2500, cal=2400 → 2400/2500 → streak=1
        // Day−1 (normal): target=1800, cal=1700 → 4100/4300 → streak=2
        db.DailyLogs.AddRange(
            new DailyLog { Date = Today, CaloriesKcal = 2400, IsCheatDay = true },
            new DailyLog { Date = Today.AddDays(-1), CaloriesKcal = 1700 }
        );
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Equal(2, result.CalorieStreakDays);
    }

    // ── Estimated TDEE ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_EstimatedTdee_IsNull_WhenFewerThan15RecentPairedLogs()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-30), WeightKg = 82m }); // ref30 for trend
        for (int i = 1; i <= 5; i++) // only 5 paired logs — below the 15 minimum
            db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-i), WeightKg = 79m, CaloriesKcal = 2000 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.EstimatedTdeeKcal);
    }

    [Fact]
    public async Task GetAsync_EstimatedTdee_IsNull_WhenNoWeightTrend()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        // 16 paired logs but no ref30 data → trend30 = null → TDEE = null
        for (int i = 1; i <= 16; i++)
            db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-i), WeightKg = 79m, CaloriesKcal = 2000 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        Assert.Null(result.EstimatedTdeeKcal);
    }

    [Fact]
    public async Task GetAsync_EstimatedTdee_AdjustsCaloricIntakeForWeightTrend()
    {
        using var db = CreateDb();
        await SeedSettingsAsync(db);
        // ref30 baseline: 82 kg at 30 days ago (within 27–33 day window)
        db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-30), WeightKg = 82m });
        // 16 paired logs from 1–16 days ago (strictly within last 30 days, both fields set)
        for (int i = 1; i <= 16; i++)
            db.DailyLogs.Add(new DailyLog { Date = Today.AddDays(-i), WeightKg = 79m, CaloriesKcal = 2000 });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetAsync(Today);

        // trend30 = 79 − 82 = −3 kg / 30 days → dailyChange = −0.1 kg/day
        // EstimatedTdee = avg(2000) − (−0.1 × 7700) = 2000 + 770 = 2770
        Assert.Equal(2770, result.EstimatedTdeeKcal);
    }
}
