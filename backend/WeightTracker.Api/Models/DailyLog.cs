namespace WeightTracker.Api.Models;

public class DailyLog
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal? WeightKg { get; set; }
    public int? CaloriesKcal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
