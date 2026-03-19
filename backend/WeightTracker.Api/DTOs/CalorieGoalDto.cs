using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record CalorieGoalDto(int Id, int TargetCalories, DateTimeOffset CreatedAt);

public record CreateCalorieGoalDto([Range(0, 30000)] int TargetCalories);
