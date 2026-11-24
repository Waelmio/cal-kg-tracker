using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Stats;

namespace WeightTracker.Api.Services;

/// <summary>
/// The active goal is the newest one that has not ended. Ended goals stay in history and never become active
/// again, so deleting the latest goal does not revive an old one.
/// </summary>
public class GoalService(AppDbContext db, IStatsService stats, ICalorieGoalService calorieGoals) : IGoalService
{
    public async Task<List<GoalDto>> GetAllAsync()
    {
        var goals = await db.Goals.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return goals.Select(ToDto).ToList();
    }

    public async Task<GoalDto?> GetActiveAsync()
    {
        var goal = await ActiveAsync();
        return goal is null ? null : ToDto(goal);
    }

    private Task<Goal?> ActiveAsync() =>
        db.Goals.Where(g => g.EndedOn == null).OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();

    public async Task<GoalDto> CreateAsync(CreateGoalDto dto, DateOnly? todayOrNull = null)
    {
        var today = todayOrNull ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var start = dto.StartDate ?? today;
        if (dto.TargetDate <= start) throw new GoalException("The target date must be after the start date.");

        var startingWeight = dto.StartingWeightKg ?? await StartingWeightAsync(start, today);
        if (startingWeight is { } sw && sw == dto.TargetWeightKg)
            throw new GoalException("The target weight must differ from the starting weight.");

        foreach (var active in await db.Goals.Where(g => g.EndedOn == null).ToListAsync())
        {
            active.EndedOn = today;
            active.EndReason = GoalEndReason.Replaced;
        }

        var goal = new Goal
        {
            TargetWeightKg = dto.TargetWeightKg,
            TargetDate = dto.TargetDate,
            StartingWeightKg = startingWeight,
            StartDate = start,
            Notes = dto.Notes,
            Kind = GoalKind.Reach,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();
        return ToDto(goal);
    }

    public async Task<GoalDto?> UpdateAsync(int id, UpdateGoalDto dto)
    {
        var goal = await db.Goals.FindAsync(id);
        if (goal is null) return null;
        if (goal.EndedOn is not null) throw new GoalException("An ended goal can't be edited.", 409);
        if (goal.Kind == GoalKind.Reach && (dto.TargetDate is null || dto.TargetDate <= dto.StartDate))
            throw new GoalException("The target date must be after the start date.");
        if (dto.StartingWeightKg == dto.TargetWeightKg)
            throw new GoalException("The target weight must differ from the starting weight.");

        goal.TargetWeightKg = dto.TargetWeightKg;
        goal.TargetDate = goal.Kind == GoalKind.Maintain ? null : dto.TargetDate;
        goal.StartDate = dto.StartDate;
        goal.StartingWeightKg = dto.StartingWeightKg;
        goal.Notes = dto.Notes;
        await db.SaveChangesAsync();
        return ToDto(goal);
    }

    public async Task<GoalDto?> EndAsync(int id, EndGoalDto dto, DateOnly today)
    {
        var goal = await db.Goals.FindAsync(id);
        if (goal is null) return null;
        if (goal.EndedOn is not null) throw new GoalException("This goal has already ended.", 409);
        if (dto.Reason is not (GoalEndReason.Ended or GoalEndReason.Reached))
            throw new GoalException("A goal can only be ended as 'Ended' or 'Reached'.");

        goal.EndedOn = today;
        goal.EndReason = dto.Reason;
        await db.SaveChangesAsync();
        return ToDto(goal);
    }

    /// <summary>
    /// Ends the active goal and starts keeping the current weight: a goal of the current weight with no date,
    /// and a calorie target of "Calories to keep your current weight" (the TDEE when that is not available).
    /// </summary>
    public async Task<GoalDto> MaintainAsync(DateOnly today)
    {
        var dashboard = await stats.DashboardAsync(today);
        if (dashboard.Weight.Kg is not { } kg) throw new GoalException("Log a weight first.");

        var tdee = await stats.TdeeAsync(90, today);
        var keep = tdee.Diet?.Rows.FirstOrDefault(r => r.Key == "keep")?.Kcal ?? tdee.CurrentTdee;
        if (keep is null) throw new GoalException("Set your TDEE first, so there is a calorie target to keep your weight.");

        foreach (var active in await db.Goals.Where(g => g.EndedOn == null).ToListAsync())
        {
            active.EndedOn = today;
            active.EndReason = GoalEndReason.Maintain;
        }

        var goal = new Goal
        {
            TargetWeightKg = Math.Round((decimal)kg, 1),
            StartingWeightKg = Math.Round((decimal)kg, 1),
            StartDate = today,
            Kind = GoalKind.Maintain,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync();

        await calorieGoals.CreateAsync(new CreateCalorieGoalDto(keep.Value), today);
        return ToDto(goal);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var goal = await db.Goals.FindAsync(id);
        if (goal is null) return false;
        db.Goals.Remove(goal);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Mean of the weigh-ins in the 7 days ending at the date; else the nearest weigh-in within 7 days.</summary>
    private async Task<decimal?> StartingWeightAsync(DateOnly start, DateOnly today)
    {
        var window = await db.DailyLogs.AsNoTracking()
            .Where(l => l.WeightKg != null && l.Date >= start.AddDays(-6) && l.Date <= start && l.Date <= today)
            .Select(l => l.WeightKg!.Value).ToListAsync();
        if (window.Count > 0) return Math.Round(window.Average(), 2);

        var near = await db.DailyLogs.AsNoTracking()
            .Where(l => l.WeightKg != null && l.Date >= start.AddDays(-7) && l.Date <= start.AddDays(7) && l.Date <= today)
            .Select(l => new { l.Date, Kg = l.WeightKg!.Value }).ToListAsync();
        return near.OrderBy(l => Math.Abs(l.Date.DayNumber - start.DayNumber)).Select(l => (decimal?)l.Kg).FirstOrDefault();
    }

    private static GoalDto ToDto(Goal g) => new(
        g.Id, g.TargetWeightKg, g.TargetDate, g.StartingWeightKg, g.StartDate, g.Notes, g.CreatedAt, g.Kind, g.EndedOn, g.EndReason);
}
