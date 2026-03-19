namespace WeightTracker.Api.Models;

public class CalorieGoal
{
    public int Id { get; set; }
    public int TargetCalories { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
