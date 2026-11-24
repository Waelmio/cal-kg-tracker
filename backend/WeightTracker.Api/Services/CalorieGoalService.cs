using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class CalorieGoalService(AppDbContext db) : ICalorieGoalService
{
    public async Task<List<CalorieGoalDto>> GetAllAsync()
    {
        var goals = await db.CalorieGoals.OrderByDescending(g => g.EffectiveFrom).ThenByDescending(g => g.CreatedAt).ToListAsync();
        return goals.Select(ToDto).ToList();
    }

    public async Task<CalorieGoalDto?> GetActiveAsync()
    {
        var goal = await db.CalorieGoals.OrderByDescending(g => g.EffectiveFrom).ThenByDescending(g => g.CreatedAt).FirstOrDefaultAsync();
        return goal is null ? null : ToDto(goal);
    }

    public async Task<CalorieGoalDto> CreateAsync(CreateCalorieGoalDto dto, DateOnly? today = null)
    {
        var from = today ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        // One entry per effective day: a second change on the same day replaces the first.
        var existing = await db.CalorieGoals.Where(g => g.EffectiveFrom == from).ToListAsync();
        db.CalorieGoals.RemoveRange(existing);

        var goal = new CalorieGoal { TargetCalories = dto.TargetCalories, EffectiveFrom = from };
        db.CalorieGoals.Add(goal);
        await db.SaveChangesAsync();
        return ToDto(goal);
    }

    private static CalorieGoalDto ToDto(CalorieGoal g) => new(g.Id, g.TargetCalories, g.CreatedAt, g.EffectiveFrom);

    public async Task<bool> DeleteAsync(int id)
    {
        var goal = await db.CalorieGoals.FindAsync(id);
        if (goal is null) return false;
        db.CalorieGoals.Remove(goal);
        await db.SaveChangesAsync();
        return true;
    }
}
