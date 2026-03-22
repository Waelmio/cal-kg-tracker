using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class CalorieLogService(AppDbContext db) : ICalorieLogService
{
    public async Task<List<DailyLogWithTarget>> GetEnrichedAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var query = db.DailyLogs.AsQueryable();
        if (from.HasValue) query = query.Where(l => l.Date >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Date <= to.Value);
        var logs = await query.OrderByDescending(l => l.Date).ToListAsync();
        return await EnrichCoreAsync(logs);
    }

    public async Task<List<DailyLogWithTarget>> EnrichLogsAsync(IReadOnlyList<DailyLog> logs)
    {
        if (logs.Count == 0) return [];
        return await EnrichCoreAsync(logs);
    }

    public async Task<int?> GetEffectiveTargetAsync(DateOnly date, bool isCheatDay)
    {
        if (isCheatDay)
        {
            var settings = await db.UserSettings.FindAsync(1);
            return settings?.TdeeKcal;
        }
        return await ActiveTargetFromDbAsync(date);
    }

    // Loads goals + settings once, then resolves each log's target in memory.
    private async Task<List<DailyLogWithTarget>> EnrichCoreAsync(IReadOnlyList<DailyLog> logs)
    {
        var goals = await db.CalorieGoals.OrderByDescending(g => g.CreatedAt).ToListAsync();
        var settings = await db.UserSettings.FindAsync(1);
        var tdeeKcal = settings?.TdeeKcal;

        return logs.Select(l =>
        {
            var goalTarget = ActiveTarget(goals, l.Date);
            var effectiveTarget = l.IsCheatDay ? tdeeKcal : goalTarget;
            return new DailyLogWithTarget(l, goalTarget, effectiveTarget);
        }).ToList();
    }

    // Finds the most recent CalorieGoal created on or before the given date.
    private static int? ActiveTarget(IReadOnlyList<CalorieGoal> goals, DateOnly date)
    {
        var dateEndUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        return goals.FirstOrDefault(g => g.CreatedAt <= dateEndUtc)?.TargetCalories;
    }

    // Single-row query used by GetEffectiveTargetAsync for non-cheat days.
    private async Task<int?> ActiveTargetFromDbAsync(DateOnly date)
    {
        var dateEndUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var goal = await db.CalorieGoals
            .Where(g => g.CreatedAt <= dateEndUtc)
            .OrderByDescending(g => g.CreatedAt)
            .FirstOrDefaultAsync();
        return goal?.TargetCalories;
    }
}
