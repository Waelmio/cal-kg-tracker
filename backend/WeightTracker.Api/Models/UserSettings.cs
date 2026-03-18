namespace WeightTracker.Api.Models;

public class UserSettings
{
    public int Id { get; set; } = 1;
    public decimal? HeightCm { get; set; }
    public string PreferredUnit { get; set; } = "kg";
    public decimal? TdeeKcal { get; set; }

}
