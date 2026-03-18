using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.Tests;

public class TdeeComputationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedLogsAsync(
        AppDbContext db,
        int count,
        decimal startWeight,
        decimal weightDeltaPerDay,
        int calories)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < count; i++)
        {
            var date = today.AddDays(-(count - 1 - i));
            db.DailyLogs.Add(new DailyLog
            {
                Date = date,
                WeightKg = startWeight + weightDeltaPerDay * i,
                CaloriesKcal = calories,
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ComputeAsync_ReturnsNull_WhenInsufficientCalorieLogs()
    {
        using var db = CreateDb();
        // Only 3 calorie + weight logs; service requires ≥7 calorie logs
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 3; i++)
            db.DailyLogs.Add(new DailyLog { Date = today.AddDays(-i), WeightKg = 75m, CaloriesKcal = 2000 });
        await db.SaveChangesAsync();
        var service = new TdeeComputationService(db);

        var result = await service.ComputeAsync(30);

        Assert.Null(result);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsNull_WhenInsufficientWeightLogs()
    {
        using var db = CreateDb();
        // 7 calorie logs but only 2 weight logs; service requires ≥3 weight logs
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 0; i < 7; i++)
        {
            db.DailyLogs.Add(new DailyLog
            {
                Date = today.AddDays(-i),
                CaloriesKcal = 2000,
                WeightKg = i < 2 ? 75m : null,
            });
        }
        await db.SaveChangesAsync();
        var service = new TdeeComputationService(db);

        var result = await service.ComputeAsync(30);

        Assert.Null(result);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsTdee_WhenWeightIsFlat()
    {
        using var db = CreateDb();
        // Flat weight → slope = 0 → TDEE = avgCalories = 2000
        await SeedLogsAsync(db, 14, 75m, 0m, 2000);
        var service = new TdeeComputationService(db);

        var result = await service.ComputeAsync(30);

        Assert.NotNull(result);
        Assert.Equal(2000.0, result.EstimatedTdeeKcal);
        Assert.Equal(2000.0, result.AvgDailyCaloriesKcal);
        Assert.Equal(0.0, result.WeightTrendKgPerDay);
        Assert.Equal(14, result.CalorieDataPoints);
        Assert.Equal(14, result.WeightDataPoints);
        Assert.Equal(30, result.PeriodDays);
    }

    [Fact]
    public async Task ComputeAsync_AccountsForWeightGain_InTdeeEstimate()
    {
        using var db = CreateDb();
        // Gaining 0.1 kg/day at 2000 kcal intake
        // TDEE = 2000 - 0.1 * 7700 = 1230
        await SeedLogsAsync(db, 14, 75m, 0.1m, 2000);
        var service = new TdeeComputationService(db);

        var result = await service.ComputeAsync(30);

        Assert.NotNull(result);
        Assert.InRange(result.EstimatedTdeeKcal, 1228.0, 1232.0);
        Assert.InRange(result.WeightTrendKgPerDay, 0.099, 0.101);
    }

    [Fact]
    public async Task ComputeAsync_AccountsForWeightLoss_InTdeeEstimate()
    {
        using var db = CreateDb();
        // Losing 0.1 kg/day at 1500 kcal intake
        // TDEE = 1500 - (-0.1 * 7700) = 1500 + 770 = 2270
        await SeedLogsAsync(db, 14, 80m, -0.1m, 1500);
        var service = new TdeeComputationService(db);

        var result = await service.ComputeAsync(30);

        Assert.NotNull(result);
        Assert.InRange(result.EstimatedTdeeKcal, 2268.0, 2272.0);
        Assert.InRange(result.WeightTrendKgPerDay, -0.101, -0.099);
    }
}
