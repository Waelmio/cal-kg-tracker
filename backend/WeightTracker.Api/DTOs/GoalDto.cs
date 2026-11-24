using System.ComponentModel.DataAnnotations;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.DTOs;

/// <remarks>
/// ChangeKg is computed for goals that ended: the weight around the end date minus the starting weight. Not stored.
/// In an export the id is left out (0): it carries no meaning and is ignored on import.
/// </remarks>
public record GoalDto(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Id,
    decimal TargetWeightKg,
    DateOnly? TargetDate,
    decimal? StartingWeightKg,
    DateOnly StartDate,
    string? Notes,
    DateTimeOffset CreatedAt,
    GoalKind Kind = GoalKind.Reach,
    DateOnly? EndedOn = null,
    GoalEndReason? EndReason = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] decimal? ChangeKg = null);

/// <summary>Creating a goal ends the active one (reason Replaced). StartingWeightKg defaults to the 7-day average at the start date.</summary>
public record CreateGoalDto(
    [Range(1, 700)] decimal TargetWeightKg,
    DateOnly TargetDate,
    DateOnly? StartDate,
    [MaxLength(500)] string? Notes,
    [Range(1, 700)] decimal? StartingWeightKg = null);

public record UpdateGoalDto(
    [Range(1, 700)] decimal TargetWeightKg,
    DateOnly? TargetDate,
    DateOnly StartDate,
    [Range(1, 700)] decimal? StartingWeightKg,
    [MaxLength(500)] string? Notes);

public record EndGoalDto(GoalEndReason Reason);
