namespace WeightTracker.Api.DTOs;

public record CalorieGoalDto(int Id, int TargetCalories, DateTime CreatedAt);

public record CreateCalorieGoalDto(int TargetCalories);
