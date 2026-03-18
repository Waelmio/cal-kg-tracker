using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public class DashboardService(AppDbContext db) : IDashboardService
{
    public async Task<DashboardDto> GetAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var settings = await db.UserSettings.FindAsync(1);
        var goal = await db.Goals.OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();
        var todayUtcEnd = today.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var activeCalorieGoal = await db.CalorieGoals
            .Where(g => g.CreatedAt <= todayUtcEnd)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();

        // All logs ordered desc for trend calculations
        var logs = await db.DailyLogs
            .Where(l => l.Date <= today)
            .OrderByDescending(l => l.Date)
            .ToListAsync();

        var todayLog = logs.FirstOrDefault(l => l.Date == today);

        var latestWeightLog = logs.FirstOrDefault(l => l.WeightKg != null);
        var currentWeight = latestWeightLog?.WeightKg;

        // Weekly avg calories (current week Mon–Sun)
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7; // Mon=0 … Sun=6
        var weekStart = today.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);
        var weekCalories = await db.DailyLogs
            .Where(l => l.Date >= weekStart && l.Date <= weekEnd && l.CaloriesKcal != null)
            .Select(l => l.CaloriesKcal!.Value)
            .ToListAsync();
        double? weeklyAvgCalories = weekCalories.Count > 0
            ? Math.Round(weekCalories.Average(), 0)
            : null;

        // 7-day trend (latest weight vs weight closest to 7 days ago)
        // kept for internal projected-date calculation only
        decimal? trend7 = null;
        if (latestWeightLog is not null)
        {
            var cutoff7 = latestWeightLog.Date.AddDays(-7);
            var ref7 = logs.FirstOrDefault(l => l.WeightKg != null && l.Date <= cutoff7);
            if (ref7 is not null) trend7 = latestWeightLog.WeightKg - ref7.WeightKg;
        }

        // 7-day average weight and its trend vs previous 7 days
        var window7End = today;
        var window7Start = today.AddDays(-6);
        var window14Start = today.AddDays(-13);
        var last7Weights = logs
            .Where(l => l.WeightKg != null && l.Date >= window7Start && l.Date <= window7End)
            .Select(l => l.WeightKg!.Value).ToList();
        var prev7Weights = logs
            .Where(l => l.WeightKg != null && l.Date >= window14Start && l.Date < window7Start)
            .Select(l => l.WeightKg!.Value).ToList();
        decimal? avgWeight7Days = last7Weights.Count > 0
            ? Math.Round(last7Weights.Average(), 2)
            : null;
        decimal? avgWeight7DaysTrend = last7Weights.Count > 0 && prev7Weights.Count > 0
            ? Math.Round(last7Weights.Average() - prev7Weights.Average(), 2)
            : null;

        // 30-day trend using 7-day window averages to reduce single-weigh-in noise:
        // recent avg (last 7 days) vs reference avg (7-day window centred on 30 days ago)
        decimal? trend30 = null;
        var ref30Start = today.AddDays(-33);
        var ref30End = today.AddDays(-27);
        var ref30Weights = logs
            .Where(l => l.WeightKg != null && l.Date >= ref30Start && l.Date <= ref30End)
            .Select(l => l.WeightKg!.Value).ToList();
        if (last7Weights.Count > 0 && ref30Weights.Count > 0)
            trend30 = Math.Round(last7Weights.Average() - ref30Weights.Average(), 2);

        // Weight volatility: population std dev of last 7 days
        decimal? weightVolatilityKg = null;
        if (last7Weights.Count >= 2)
        {
            var mean = last7Weights.Average();
            var variance = last7Weights.Average(w => (double)((w - mean) * (w - mean)));
            weightVolatilityKg = Math.Round((decimal)Math.Sqrt(variance), 2);
        }

        // Rate of change derived from 30-day trend
        decimal? weightChangeRateKgPerWeek = trend30.HasValue
            ? Math.Round(trend30.Value / 30m * 7m, 2)
            : null;

        // Weekly calorie deficit: sum of (target - calories) for each logged day this week
        int? weeklyCalorieDeficit = null;
        int weeklyCalorieDeficitDays = weekCalories.Count;
        if (activeCalorieGoal != null && weekCalories.Count > 0)
            weeklyCalorieDeficit = weekCalories.Count * activeCalorieGoal.TargetCalories - weekCalories.Sum();

        // Goal progress
        double? progressPercent = null;
        decimal? kgToGoal = null;
        DateOnly? projectedDate = null;

        if (goal is not null && currentWeight.HasValue)
        {
            kgToGoal = Math.Max(0, currentWeight.Value - goal.TargetWeightKg);

            if (goal.StartingWeightKg.HasValue)
            {
                var totalToLose = goal.StartingWeightKg.Value - goal.TargetWeightKg;
                var lost = goal.StartingWeightKg.Value - currentWeight.Value;
                if (totalToLose != 0)
                    progressPercent = Math.Clamp(Math.Round((double)(lost / totalToLose * 100), 1), 0, 100);
            }

            // Project goal completion date from 7-day trend
            if (trend7.HasValue && trend7.Value < 0 && kgToGoal > 0)
            {
                var ratePerDay = (double)trend7.Value / 7.0;
                var daysNeeded = (double)kgToGoal.Value / (-ratePerDay);
                projectedDate = today.AddDays((int)Math.Ceiling(daysNeeded));
            }
        }

        // BMI
        double? bmi = null;
        if (currentWeight.HasValue && settings?.HeightCm.HasValue == true)
        {
            var h = (double)settings.HeightCm!.Value / 100.0;
            bmi = Math.Round((double)currentWeight.Value / (h * h), 1);
        }

        // Estimated TDEE from last-30-days calorie avg + 30-day weight trend
        var thirtyDaysAgo = today.AddDays(-30);
        var last30CalorieLogs = logs
            .Where(l => l.Date > thirtyDaysAgo && l.CaloriesKcal != null && l.WeightKg != null)
            .ToList();
        int? estimatedTdeeKcal = null;
        if (last30CalorieLogs.Count >= 15 && trend30.HasValue)
        {
            double avgDailyCalories = last30CalorieLogs.Average(l => l.CaloriesKcal!.Value);
            double dailyWeightChangeKg = (double)trend30.Value / 30.0;
            estimatedTdeeKcal = (int)Math.Round(avgDailyCalories - dailyWeightChangeKg * 7700.0);
        }

        // Calorie streak: longest window from today backwards (logged days only)
        // where the running average stays below the daily calorie target
        int calorieStreakDays = 0;
        int caloriesExcessOverStreak = 0;
        int? calorieStreakNextDays = null;
        if (activeCalorieGoal != null)
        {
            var loggedCalorieDays = logs
                .Where(l => l.CaloriesKcal != null)
                .OrderByDescending(l => l.Date)
                .ToList();

            long runningSum = 0;
            int count = 0;
            foreach (var log in loggedCalorieDays)
            {
                runningSum += log.CaloriesKcal!.Value;
                count++;
                double avg = (double) runningSum / count;
                
                // We are not finished with the actual streak
                if (caloriesExcessOverStreak == 0) {
                    if (avg < activeCalorieGoal.TargetCalories)
                        calorieStreakDays = count;
                    else
                    {
                        calorieStreakNextDays = count;
                        caloriesExcessOverStreak = (int) Math.Ceiling(runningSum - (double) activeCalorieGoal.TargetCalories * count);
                    }
                }
                // We finished checking the first streak, now calculating what the next streak would be like.
                else {
                    // If we win caloriesExcessOverStreak calories
                    avg -= (double)caloriesExcessOverStreak / count;

                    // Maybe we can win a few more days
                    if (avg < activeCalorieGoal.TargetCalories)
                        calorieStreakNextDays = count;
                    else
                        break;
                }

            }
        }

        GoalDto? goalDto = goal is null ? null : new GoalDto(
            goal.Id, goal.TargetWeightKg, goal.TargetDate,
            goal.StartingWeightKg, goal.StartDate,
            goal.Notes, goal.CreatedAt);

        return new DashboardDto(
            currentWeight,
            todayLog?.CaloriesKcal,
            activeCalorieGoal?.TargetCalories,
            weeklyAvgCalories,
            avgWeight7Days,
            avgWeight7DaysTrend,
            trend30,
            goalDto,
            progressPercent,
            kgToGoal,
            projectedDate,
            bmi,
            logs.Count,
            estimatedTdeeKcal,
            calorieStreakDays,
            calorieStreakNextDays,
            caloriesExcessOverStreak,
            weightVolatilityKg,
            weightChangeRateKgPerWeek,
            weeklyCalorieDeficit,
            weeklyCalorieDeficitDays);
    }
}
