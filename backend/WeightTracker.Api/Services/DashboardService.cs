using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class DashboardService(AppDbContext db, ICalorieLogService calorieLogService) : IDashboardService
{
    private record WeightStats(
        decimal? Avg7Days,
        decimal? Avg7DaysTrend,
        decimal? PointShift7Days,   // latest vs ~7 days ago, used for goal date projection
        decimal? Trend30Days,
        decimal? VolatilityKg,
        decimal? ChangeRateKgPerWeek);

    private record WeeklyCalorieStats(
        double? AvgCalories,
        int? Deficit,
        int? TdeeDeficit,
        int Days);

    public async Task<DashboardDto> GetAsync(DateOnly today)
    {
        var now = today;
        var settings = await db.UserSettings.FirstAsync();
        var weightGoal = await db.Goals.OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();

        var daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
        var weekStart = now.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);

        // Load enriched logs up to the later of: end of current week, or goal target date (to include future logged days).
        var loadTo = weightGoal is not null && weightGoal.TargetDate > weekEnd ? weightGoal.TargetDate : weekEnd;
        var allEnrichedWithFuture = await calorieLogService.GetEnrichedAsync(to: loadTo);
        var allEnriched = allEnrichedWithFuture.Where(e => e.Log.Date <= now).ToList();
        var allLogs = allEnriched.Select(e => e.Log).ToList();

        var weekEnriched = allEnrichedWithFuture.Where(e => e.Log.Date >= weekStart && e.Log.Date <= weekEnd && e.Log.CaloriesKcal != null).ToList();

        var todayEnriched = allEnriched.FirstOrDefault(e => e.Log.Date == now);
        var currentWeight = allLogs.FirstOrDefault(l => l.WeightKg != null)?.WeightKg;

        var weight = CalcWeightStats(allLogs, now);
        var weekly = CalcWeeklyCalorieStats(weekEnriched, settings.TdeeKcal);
        var (overallCalorieDeficit, overallCalorieDeficitVsTarget, overallCalorieDeficitDays) = CalcOverallCalorieDeficit(allEnrichedWithFuture, weightGoal, settings.TdeeKcal);
        var (progressPercent, kgToGoal, projectedDate) = CalcGoalProgress(weightGoal, weight.Avg7Days, currentWeight, weight.PointShift7Days, now);
        var (calorieStreakDays, calorieStreakNextDays, caloriesExcessOverStreak) = CalcCalorieStreak(allEnriched);

        // If today has no log, fall back to the active goal for today's target display.
        var todayCalorieTarget = todayEnriched?.EffectiveTarget
            ?? await calorieLogService.GetEffectiveTargetAsync(now, false);

        GoalDto? goalDto = weightGoal is null ? null : new GoalDto(
            weightGoal.Id, weightGoal.TargetWeightKg, weightGoal.TargetDate,
            weightGoal.StartingWeightKg, weightGoal.StartDate,
            weightGoal.Notes, weightGoal.CreatedAt);

        return new DashboardDto(
            currentWeight,
            todayEnriched?.Log.CaloriesKcal,
            todayCalorieTarget,
            weekly.AvgCalories,
            weight.Avg7Days,
            weight.Avg7DaysTrend,
            weight.Trend30Days,
            goalDto,
            progressPercent,
            kgToGoal,
            projectedDate,
            CalcBmi(currentWeight, settings),
            allLogs.Count,
            CalcEstimatedTdee(allLogs, now, weight.Trend30Days),
            calorieStreakDays,
            calorieStreakNextDays,
            caloriesExcessOverStreak,
            weight.VolatilityKg,
            weight.ChangeRateKgPerWeek,
            weekly.Deficit,
            weekly.Days,
            weekly.TdeeDeficit,
            overallCalorieDeficit,
            overallCalorieDeficitVsTarget,
            overallCalorieDeficitDays);
    }

    // Computes all weight-related stats from a single pass over the logs.
    private static WeightStats CalcWeightStats(List<DailyLog> logs, DateOnly today)
    {
        var last7 = logs
            .Where(l => l.WeightKg != null && l.Date >= today.AddDays(-6))
            .Select(l => l.WeightKg!.Value).ToList();
        var prev7 = logs
            .Where(l => l.WeightKg != null && l.Date >= today.AddDays(-13) && l.Date < today.AddDays(-6))
            .Select(l => l.WeightKg!.Value).ToList();

        decimal? avg7 = last7.Count > 0 ? Math.Round(last7.Average(), 2) : null;
        decimal? avg7Trend = last7.Count > 0 && prev7.Count > 0
            ? Math.Round(last7.Average() - prev7.Average(), 2) : null;

        // Point-to-point shift from the latest weigh-in to one ~7 days earlier — used for projecting goal date.
        decimal? pointShift7 = null;
        var latestWeight = logs.FirstOrDefault(l => l.WeightKg != null);
        if (latestWeight is not null)
        {
            var ref7 = logs.FirstOrDefault(l => l.WeightKg != null && l.Date <= latestWeight.Date.AddDays(-7));
            if (ref7 is not null) pointShift7 = latestWeight.WeightKg - ref7.WeightKg;
        }

        // 30-day trend: recent 7-day avg vs a 7-day reference window centred on 30 days ago.
        decimal? trend30 = null;
        if (last7.Count > 0)
        {
            var ref30 = logs
                .Where(l => l.WeightKg != null && l.Date >= today.AddDays(-33) && l.Date <= today.AddDays(-27))
                .Select(l => l.WeightKg!.Value).ToList();
            if (ref30.Count > 0)
                trend30 = Math.Round(last7.Average() - ref30.Average(), 2);
        }

        // Population std dev of last 7 days.
        decimal? volatility = null;
        if (last7.Count >= 2)
        {
            var mean = last7.Average();
            var variance = last7.Average(w => (double)((w - mean) * (w - mean)));
            volatility = Math.Round((decimal)Math.Sqrt(variance), 2);
        }

        decimal? changeRate = trend30.HasValue ? Math.Round(trend30.Value / 30m * 7m, 2) : null;

        return new WeightStats(avg7, avg7Trend, pointShift7, trend30, volatility, changeRate);
    }

    // Returns weekly average calories and calorie deficit for the current week.
    // Each log's EffectiveTarget already accounts for cheat days vs. regular goal.
    private static WeeklyCalorieStats CalcWeeklyCalorieStats(List<DailyLogWithTarget> weekLogs, int? tdeeKcal)
    {
        double? avg = weekLogs.Count > 0 ? Math.Round(weekLogs.Average(e => e.Log.CaloriesKcal!.Value), 0) : null;
        var logsWithGoal = weekLogs.Where(e => e.EffectiveTarget.HasValue).ToList();
        int? deficit = logsWithGoal.Count > 0 ? logsWithGoal.Sum(e => e.EffectiveTarget!.Value - e.Log.CaloriesKcal!.Value) : null;
        int? tdeeDeficit = tdeeKcal.HasValue && weekLogs.Count > 0
            ? weekLogs.Sum(e => tdeeKcal.Value - e.Log.CaloriesKcal!.Value)
            : null;
        return new(avg, deficit, tdeeDeficit, weekLogs.Count);
    }

    // Returns overall calorie deficit since the weight goal's start date.
    // vsTdee always uses TDEE as the baseline so that changing a day's calorie target doesn't affect this metric.
    // vsTarget uses each day's effective target (accounting for cheat days and goal changes).
    private static (int? deficitVsTdee, int? deficitVsTarget, int days) CalcOverallCalorieDeficit(List<DailyLogWithTarget> allLogs, Goal? goal, int? tdeeKcal)
    {
        if (goal is null) return (null, null, 0);
        var goalLogs = allLogs
            .Where(e => e.Log.Date >= goal.StartDate && e.Log.Date <= goal.TargetDate && e.Log.CaloriesKcal != null)
            .ToList();
        if (goalLogs.Count == 0) return (null, null, 0);
        int? deficitVsTdee = tdeeKcal.HasValue ? goalLogs.Sum(e => tdeeKcal.Value - e.Log.CaloriesKcal!.Value) : null;
        var logsWithTarget = goalLogs.Where(e => e.EffectiveTarget.HasValue).ToList();
        int? deficitVsTarget = logsWithTarget.Count > 0 ? logsWithTarget.Sum(e => e.EffectiveTarget!.Value - e.Log.CaloriesKcal!.Value) : null;
        return (deficitVsTdee, deficitVsTarget, goalLogs.Count);
    }

    // Returns goal progress: percent complete, kg remaining, and projected completion date.
    private static (double? progress, decimal? kgToGoal, DateOnly? projectedDate) CalcGoalProgress(
        Goal? goal, decimal? avg7Days, decimal? currentWeight, decimal? pointShift7, DateOnly today)
    {
        if (goal is null) return (null, null, null);
        var weight = avg7Days ?? currentWeight;
        if (!weight.HasValue) return (null, null, null);

        var kgToGoal = Math.Max(0, weight.Value - goal.TargetWeightKg);

        double? progress = null;
        if (goal.StartingWeightKg.HasValue)
        {
            var totalToLose = goal.StartingWeightKg.Value - goal.TargetWeightKg;
            var lost = goal.StartingWeightKg.Value - weight.Value;
            if (totalToLose != 0)
                progress = Math.Clamp(Math.Round((double)(lost / totalToLose * 100), 1), 0, 100);
        }

        DateOnly? projectedDate = null;
        if (pointShift7.HasValue && pointShift7.Value < 0 && kgToGoal > 0)
        {
            var ratePerDay = (double)pointShift7.Value / 7.0;
            projectedDate = today.AddDays((int)Math.Ceiling((double)kgToGoal / -ratePerDay));
        }

        return (progress, kgToGoal, projectedDate);
    }

    // Returns BMI based on the most recent weight and stored height.
    private static double? CalcBmi(decimal? currentWeight, UserSettings? settings)
    {
        if (!currentWeight.HasValue || settings?.HeightCm is not { } heightCm) return null;
        var h = (double)heightCm / 100.0;
        return Math.Round((double)currentWeight.Value / (h * h), 1);
    }

    // Returns estimated TDEE from the last-30-days calorie average adjusted for the measured weight trend.
    private static int? CalcEstimatedTdee(List<DailyLog> logs, DateOnly today, decimal? trend30)
    {
        if (!trend30.HasValue) return null;
        var recent = logs
            .Where(l => l.Date > today.AddDays(-30) && l.CaloriesKcal != null && l.WeightKg != null)
            .ToList();
        if (recent.Count < 15) return null;
        var avgDailyCalories = recent.Average(l => l.CaloriesKcal!.Value);
        var dailyWeightChangeKg = (double)trend30.Value / 30.0;
        return (int)Math.Round(avgDailyCalories - dailyWeightChangeKg * 7700.0);
    }

    // Returns the calorie streak: days in aggregate deficit, next potential streak length,
    // and kcal excess above the aggregate target that broke the streak.
    // Each log uses the calorie goal active on its date; logs with no goal are skipped.
    private static (int streakDays, int? nextDays, int excessKcal) CalcCalorieStreak(List<DailyLogWithTarget> logs)
    {
        var calorieDays = logs.Where(e => e.Log.CaloriesKcal != null && e.EffectiveTarget.HasValue).ToList();
        if (calorieDays.Count == 0) return (0, null, 0);

        long runningCalories = 0;
        long runningTarget = 0;
        int count = 0;
        int streakDays = 0;
        int excessKcal = 0;
        int? nextDays = null;

        foreach (var entry in calorieDays)
        {
            runningCalories += entry.Log.CaloriesKcal!.Value;
            runningTarget += entry.EffectiveTarget!.Value;
            count++;

            if (excessKcal == 0)
            {
                if (runningCalories < runningTarget)
                    streakDays = count;
                else
                {
                    nextDays = count;
                    excessKcal = (int)(runningCalories - runningTarget);
                }
            }
            else
            {
                if (runningCalories - excessKcal < runningTarget)
                    nextDays = count;
                else
                    break;
            }
        }

        return (streakDays, nextDays, excessKcal);
    }
}
