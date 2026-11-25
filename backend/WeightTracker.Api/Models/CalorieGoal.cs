namespace WeightTracker.Api.Models;

public class CalorieGoal
{
    public int Id { get; set; }

    /// <summary>Daily calorie target. Null means "target turned off" from <see cref="EffectiveFrom"/>.</summary>
    public int? TargetCalories { get; set; }

    /// <summary>First day (user's local date) on which this target applies.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
