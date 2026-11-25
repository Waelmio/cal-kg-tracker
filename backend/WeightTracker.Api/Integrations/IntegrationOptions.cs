namespace WeightTracker.Api.Integrations;

/// <summary>Section "Integrations" of appsettings.json.</summary>
public class IntegrationOptions
{
    /// <summary>Hours between automatic syncs. 0 turns them off.</summary>
    public double SyncIntervalHours { get; set; } = 12;

    /// <summary>How far back a sync goes, from the end of the current week.</summary>
    public int SyncMonths { get; set; } = 3;

    /// <summary>
    /// Today counts in every statistic as soon as it has calories, so a sync in the middle of the day counts a partial
    /// total until the next one. False: a sync leaves the calories of a day alone until it is over, the rest of the week included.
    /// </summary>
    public bool ImportTodayCalories { get; set; } = true;
}
