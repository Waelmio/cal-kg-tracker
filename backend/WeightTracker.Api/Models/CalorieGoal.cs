namespace WeightTracker.Api.Models;

public class CalorieGoal
{
    public int Id { get; set; }
    public int TargetCalories { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
