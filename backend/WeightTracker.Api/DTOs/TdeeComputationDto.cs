namespace WeightTracker.Api.DTOs;

public record TdeeComputationDto(
    double EstimatedTdeeKcal,
    double AvgDailyCaloriesKcal,
    double WeightTrendKgPerDay,
    int CalorieDataPoints,
    int WeightDataPoints,
    int PeriodDays
);
