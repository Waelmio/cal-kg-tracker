using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class CalorieGoalService(AppDbContext db) : ICalorieGoalService
{
    public async Task<List<CalorieGoalDto>> GetAllAsync()
    {
        var goals = await db.CalorieGoals.OrderByDescending(g => g.CreatedAt).ToListAsync();
        return goals.Select(ToDto).ToList();
    }

    public async Task<CalorieGoalDto?> GetActiveAsync()
    {
        var goal = await db.CalorieGoals.OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync();
        return goal is null ? null : ToDto(goal);
    }

    public async Task<CalorieGoalDto> CreateAsync(CreateCalorieGoalDto dto)
    {
        var todayUtc = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var startUtc = todayUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);
        var existing = await db.CalorieGoals
            .Where(g => g.CreatedAt >= startUtc && g.CreatedAt < endUtc)
            .ToListAsync();
        db.CalorieGoals.RemoveRange(existing);

        var goal = new CalorieGoal { TargetCalories = dto.TargetCalories };
        db.CalorieGoals.Add(goal);
        await db.SaveChangesAsync();
        return ToDto(goal);
    }

    private static CalorieGoalDto ToDto(CalorieGoal g) => new(g.Id, g.TargetCalories, g.CreatedAt);

    public async Task<bool> DeleteAsync(int id)
    {
        var goal = await db.CalorieGoals.FindAsync(id);
        if (goal is null) return false;
        db.CalorieGoals.Remove(goal);
        await db.SaveChangesAsync();
        return true;
    }
}
