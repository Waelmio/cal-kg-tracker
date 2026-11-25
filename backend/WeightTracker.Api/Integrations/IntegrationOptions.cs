namespace WeightTracker.Api.Integrations;

/// <summary>Section "Integrations" of appsettings.json.</summary>
public class IntegrationOptions
{
    /// <summary>Hours between automatic syncs. 0 turns them off.</summary>
    public double SyncIntervalHours { get; set; } = 12;

    /// <summary>Full weeks a sync covers before the current one.</summary>
    public int SyncWeeksBack { get; set; } = 3;

    /// <summary>Full weeks a sync covers after the current one.</summary>
    public int SyncWeeksAhead { get; set; } = 2;

    /// <summary>
    /// Today counts in every statistic as soon as it has calories, so a sync in the middle of the day counts a partial
    /// total until the next one. False: a sync leaves the calories of a day alone until it is over, so it imports none for the days to come.
    /// </summary>
    public bool ImportTodayCalories { get; set; } = true;
}
