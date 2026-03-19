namespace WeightTracker.Api.Models;

public class Goal
{
    public int Id { get; set; }
    public decimal TargetWeightKg { get; set; }
    public DateOnly TargetDate { get; set; }
    public decimal? StartingWeightKg { get; set; }
    public DateOnly StartDate { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
