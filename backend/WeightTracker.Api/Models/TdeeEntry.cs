namespace WeightTracker.Api.Models;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum TdeeSource { Manual, Estimate }

/// <summary>One TDEE value in force from <see cref="EffectiveFrom"/> until the next entry.</summary>
public class TdeeEntry
{
    public int Id { get; set; }
    public int Kcal { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public TdeeSource Source { get; set; } = TdeeSource.Manual;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
