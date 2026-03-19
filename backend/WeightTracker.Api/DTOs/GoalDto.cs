using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record GoalDto(
    int Id,
    decimal TargetWeightKg,
    DateOnly TargetDate,
    decimal? StartingWeightKg,
    DateOnly StartDate,
    string? Notes,
    DateTimeOffset CreatedAt);

public record CreateGoalDto(
    [Range(1, 700)] decimal TargetWeightKg,
    DateOnly TargetDate,
    [MaxLength(500)] string? Notes);
