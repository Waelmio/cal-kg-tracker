using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record CalorieGoalDto([property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Id, int? TargetCalories, DateTimeOffset CreatedAt, DateOnly? EffectiveFrom = null);

public record CreateCalorieGoalDto([Range(0, 30000)] int? TargetCalories);
