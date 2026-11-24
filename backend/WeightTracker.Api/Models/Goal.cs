namespace WeightTracker.Api.Models;

public enum GoalKind { Reach, Maintain }

public enum GoalEndReason { Ended, Reached, Replaced, Maintain }

public class Goal
{
    public int Id { get; set; }
    public decimal TargetWeightKg { get; set; }

    /// <summary>Null for maintenance goals (no date).</summary>
    public DateOnly? TargetDate { get; set; }

    public decimal? StartingWeightKg { get; set; }
    public DateOnly StartDate { get; set; }
    public string? Notes { get; set; }
    public GoalKind Kind { get; set; } = GoalKind.Reach;

    /// <summary>Set when the goal stops being active. Ended goals are kept in history and never revive.</summary>
    public DateOnly? EndedOn { get; set; }
    public GoalEndReason? EndReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
