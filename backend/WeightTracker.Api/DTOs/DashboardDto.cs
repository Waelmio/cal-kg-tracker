namespace WeightTracker.Api.DTOs;

public record DashboardDto(
    // Today
    decimal? CurrentWeightKg,
    int? TodayCaloriesKcal,
    int? DailyCalorieTarget,

    // Weekly
    double? WeeklyAvgCalories,
    decimal? AvgWeight7Days,
    decimal? AvgWeight7DaysTrend,
    decimal? WeightTrend30Days,

    // Weight goal
    GoalDto? ActiveGoal,
    double? GoalProgressPercent,
    decimal? KgToGoal,
    DateOnly? ProjectedGoalDate,

    // BMI
    double? Bmi,

    int TotalEntries,

    // TDEE & streak
    int? EstimatedTdeeKcal,
    int CalorieStreakDays,
    int? CalorieStreakNextDays,
    int? CalorieStreakNextExcessKcal,
    decimal? WeightVolatilityKg,
    decimal? WeightChangeRateKgPerWeek,
    int? WeeklyCalorieDeficit,
    int WeeklyCalorieDeficitDays,
    int? WeeklyCalorieDeficitVsTdee,
    int? OverallCalorieDeficit,
    int? OverallCalorieDeficitVsTarget,
    int OverallCalorieDeficitDays);
