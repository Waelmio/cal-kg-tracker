namespace WeightTracker.Api.Models;

/// <summary>Who wrote a value: the user (by hand or by choosing to keep it), or an import.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum LogSource { User, FatSecret }

public class DailyLog
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public decimal? WeightKg { get; set; }
    public int? CaloriesKcal { get; set; }

    /// <summary>
    /// The user's local date on which the calories <b>value</b> last changed. Tells a value logged in advance
    /// (before its own day) from one logged that day. Not touched when only weight or note change.
    /// </summary>
    public DateOnly? CaloriesSetOn { get; set; }

    /// <summary>Null when there is no value. An import only ever updates values it wrote itself (or none, or ones set in advance).</summary>
    public LogSource? CaloriesSource { get; set; }
    public LogSource? WeightSource { get; set; }

    public string? Notes { get; set; }
    public bool IsCheatDay { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
