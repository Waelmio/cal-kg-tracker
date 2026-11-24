using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

/// <summary>
/// The rules that bring data from before the redesign (exports without a version) up to the current model.
/// The database migrations apply the same rules in SQL; keep both in step.
/// </summary>
public static class LegacyUpgrade
{
    public static DateOnly DateOf(DateTimeOffset t) => DateOnly.FromDateTime(t.UtcDateTime);

    /// <summary>
    /// Old versions stored the calorie target in every empty day of the week. An untouched one was created before
    /// its own date, never updated, equal to that day's target, and created together with other rows.
    /// </summary>
    public static bool IsUntouchedAutoFill(DailyLogDto log, IReadOnlyList<DailyLogDto> all, IReadOnlyList<CalorieGoalDto> goals)
    {
        if (log.CaloriesKcal is null) return false;
        var date = DateOnly.Parse(log.Date);
        if (DateOf(log.CreatedAt) >= date) return false;
        if (log.UpdatedAt != log.CreatedAt) return false;

        var target = goals
            .Where(g => DateOf(g.CreatedAt) <= date)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => g.TargetCalories)
            .FirstOrDefault();
        if (target != log.CaloriesKcal) return false;

        return all.Any(o => o.Date != log.Date && o.CreatedAt == log.CreatedAt);
    }

    public static bool IsEmptyAfterClearing(DailyLogDto log) =>
        log.WeightKg is null && string.IsNullOrEmpty(log.Notes) && !log.IsCheatDay;
}
