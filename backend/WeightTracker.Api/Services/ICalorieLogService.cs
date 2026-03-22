using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

/// <summary>
/// A DailyLog paired with the calorie goal that was active on that specific date.
/// </summary>
public record DailyLogWithTarget(
    DailyLog Log,
    int? GoalTarget,      // CalorieGoal.TargetCalories active on this date; null if no goal existed yet
    int? EffectiveTarget  // GoalTarget, or TdeeKcal when the day is marked as a cheat day
);

public interface ICalorieLogService
{
    /// <summary>
    /// Loads all DailyLogs in the given date range and enriches each one with the
    /// calorie goal that was active on that date, resolving cheat days against TDEE.
    /// </summary>
    Task<List<DailyLogWithTarget>> GetEnrichedAsync(DateOnly? from = null, DateOnly? to = null);

    /// <summary>
    /// Enriches an already-fetched list of logs without hitting the database for the logs themselves.
    /// Loads goals and settings once, then resolves each log's active target in memory.
    /// </summary>
    Task<List<DailyLogWithTarget>> EnrichLogsAsync(IReadOnlyList<DailyLog> logs);

    /// <summary>
    /// Returns the effective calorie target for a single date.
    /// Cheat days use the stored TDEE; regular days use the active CalorieGoal.
    /// </summary>
    Task<int?> GetEffectiveTargetAsync(DateOnly date, bool isCheatDay);
}
