using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public class TdeeComputationService(AppDbContext db) : ITdeeComputationService
{
    private const double KcalPerKg = 7700.0;
    private const int MinCaloriePoints = 7;
    private const int MinWeightPoints = 3;

    public async Task<TdeeComputationDto?> ComputeAsync(int days)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var from = today.AddDays(-days);

        var logs = await db.DailyLogs
            .Where(l => l.Date >= from && l.Date <= today)
            .OrderBy(l => l.Date)
            .ToListAsync();

        var calorieLogs = logs.Where(l => l.CaloriesKcal.HasValue).ToList();
        var weightLogs = logs.Where(l => l.WeightKg.HasValue).ToList();

        if (calorieLogs.Count < MinCaloriePoints || weightLogs.Count < MinWeightPoints)
            return null;

        var avgCalories = calorieLogs.Average(l => (double)l.CaloriesKcal!.Value);

        // Linear regression of weight (kg) vs day index to get trend (kg/day)
        var origin = weightLogs[0].Date;
        var xs = weightLogs.Select(l => (double)l.Date.DayNumber - origin.DayNumber).ToArray();
        var ys = weightLogs.Select(l => (double)l.WeightKg!.Value).ToArray();
        var trendKgPerDay = LinearRegressionSlope(xs, ys);

        // TDEE = avg intake minus the calories accounted for by weight change
        // Positive trend → gaining → subtract surplus; negative → losing → add deficit
        var tdee = avgCalories - trendKgPerDay * KcalPerKg;

        return new TdeeComputationDto(
            EstimatedTdeeKcal: Math.Round(tdee, 0),
            AvgDailyCaloriesKcal: Math.Round(avgCalories, 0),
            WeightTrendKgPerDay: Math.Round(trendKgPerDay, 5),
            CalorieDataPoints: calorieLogs.Count,
            WeightDataPoints: weightLogs.Count,
            PeriodDays: days
        );
    }

    private static double LinearRegressionSlope(double[] xs, double[] ys)
    {
        var n = xs.Length;
        var xMean = xs.Average();
        var yMean = ys.Average();
        var num = xs.Zip(ys).Sum(p => (p.First - xMean) * (p.Second - yMean));
        var den = xs.Sum(x => (x - xMean) * (x - xMean));
        return den == 0 ? 0 : num / den;
    }
}
