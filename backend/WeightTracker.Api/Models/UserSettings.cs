namespace WeightTracker.Api.Models;

public class UserSettings
{
    public int Id { get; set; } = 1;
    public int? HeightCm { get; set; }
    public string PreferredUnit { get; set; } = "kg";

    /// <summary>Weeks over which the calorie target must be reached on average (1 to 5).</summary>
    public int AveragingWeeks { get; set; } = 2;
}
