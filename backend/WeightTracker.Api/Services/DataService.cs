using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class DataService(AppDbContext db) : IDataService
{
    public async Task<ExportImportDto> ExportAsync()
    {
        var settings = await db.UserSettings.FindAsync(1) ?? new UserSettings();
        var logs = await db.DailyLogs.OrderBy(l => l.Date).ToListAsync();
        var goals = await db.Goals.OrderBy(g => g.CreatedAt).ToListAsync();
        var calorieGoals = await db.CalorieGoals.OrderBy(g => g.CreatedAt).ToListAsync();

        return new ExportImportDto(
            new UserSettingsDto(settings.HeightCm, settings.PreferredUnit, settings.TdeeKcal),
            logs.Select(l => new DailyLogDto(l.Id, l.Date.ToString("yyyy-MM-dd"), l.WeightKg, l.CaloriesKcal, l.Notes, l.CreatedAt, l.UpdatedAt)).ToList(),
            goals.Select(g => new GoalDto(g.Id, g.TargetWeightKg, g.TargetDate, g.StartingWeightKg, g.StartDate, g.Notes, g.CreatedAt)).ToList(),
            calorieGoals.Select(g => new CalorieGoalDto(g.Id, g.TargetCalories, g.CreatedAt)).ToList()
        );
    }

    public async Task ImportAsync(ExportImportDto dto)
    {
        db.DailyLogs.RemoveRange(await db.DailyLogs.ToListAsync());
        db.Goals.RemoveRange(await db.Goals.ToListAsync());
        db.CalorieGoals.RemoveRange(await db.CalorieGoals.ToListAsync());

        foreach (var log in dto.DailyLogs)
        {
            db.DailyLogs.Add(new DailyLog
            {
                Date = DateOnly.Parse(log.Date),
                WeightKg = log.WeightKg,
                CaloriesKcal = log.CaloriesKcal,
                Notes = log.Notes,
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
            });
        }

        foreach (var goal in dto.Goals)
        {
            db.Goals.Add(new Goal
            {
                TargetWeightKg = goal.TargetWeightKg,
                TargetDate = goal.TargetDate,
                StartingWeightKg = goal.StartingWeightKg,
                StartDate = goal.StartDate,
                Notes = goal.Notes,
                CreatedAt = goal.CreatedAt,
            });
        }

        foreach (var cg in dto.CalorieGoals)
        {
            db.CalorieGoals.Add(new CalorieGoal
            {
                TargetCalories = cg.TargetCalories,
                CreatedAt = cg.CreatedAt,
            });
        }

        var s = await db.UserSettings.FindAsync(1);
        if (s is null) { s = new UserSettings { Id = 1 }; db.UserSettings.Add(s); }
        s.HeightCm = dto.Settings.HeightCm;
        s.PreferredUnit = dto.Settings.PreferredUnit;
        s.TdeeKcal = dto.Settings.TdeeKcal;

        await db.SaveChangesAsync();
    }
}
