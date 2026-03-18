namespace WeightTracker.Api.DTOs;

public record GoalDto(
    int Id,
    decimal TargetWeightKg,
    DateOnly TargetDate,
    decimal? StartingWeightKg,
    DateOnly StartDate,
    string? Notes,
    DateTime CreatedAt);

public record CreateGoalDto(decimal TargetWeightKg, string TargetDate, string? Notes);
