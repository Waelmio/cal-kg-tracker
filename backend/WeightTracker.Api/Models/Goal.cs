namespace WeightTracker.Api.Models;

public class Goal
{
    public int Id { get; set; }
    public decimal TargetWeightKg { get; set; }
    public DateOnly TargetDate { get; set; }
    public decimal? StartingWeightKg { get; set; }
    public DateOnly StartDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
