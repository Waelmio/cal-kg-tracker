using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class GoalService(AppDbContext db) : IGoalService
{
    public async Task<List<GoalDto>> GetAllAsync()
    {
        var goals = await db.Goals.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return goals.Select(ToDto).ToList();
    }

    public async Task<GoalDto?> GetActiveAsync()
    {
        var goal = await db.Goals.OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();
        return goal is null ? null : ToDto(goal);
    }

    public async Task<GoalDto> CreateAsync(CreateGoalDto dto)
    {
        var latestWeight = await db.DailyLogs
            .Where(l => l.WeightKg != null)
            .OrderByDescending(l => l.Date)
            .Select(l => l.WeightKg)
            .FirstOrDefaultAsync();

        var goal = new Goal
        {
            TargetWeightKg = dto.TargetWeightKg,
            TargetDate = dto.TargetDate,
            StartingWeightKg = latestWeight,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Notes = dto.Notes,
        };

        db.Goals.Add(goal);
        await db.SaveChangesAsync();
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

    private static GoalDto ToDto(Goal g) => new(
        g.Id,
        g.TargetWeightKg,
        g.TargetDate,
        g.StartingWeightKg,
        g.StartDate,
        g.Notes,
        g.CreatedAt);
}
