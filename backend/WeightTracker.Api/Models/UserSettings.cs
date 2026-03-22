namespace WeightTracker.Api.Models;

public class UserSettings
{
    public int Id { get; set; } = 1;
    public int? HeightCm { get; set; }
    public string PreferredUnit { get; set; } = "kg";
    public int? TdeeKcal { get; set; }

}
