namespace WeightTracker.Api.Models;

public class UserSettings
{
    public int Id { get; set; } = 1;
    public int? HeightCm { get; set; }
    public string PreferredUnit { get; set; } = "kg";

    /// <summary>Legacy single TDEE value. Superseded by <see cref="TdeeEntry"/>; removed in the cleanup phase.</summary>
    public int? TdeeKcal { get; set; }

    /// <summary>Weeks over which the calorie target must be reached on average (1 to 5).</summary>
    public int AveragingWeeks { get; set; } = 2;
}
