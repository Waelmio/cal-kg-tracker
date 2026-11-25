using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class DataService(AppDbContext db) : IDataService
{
    public const int CurrentVersion = 2;

    public async Task<ExportImportDto> ExportAsync()
    {
        var settings = await db.UserSettings.FindAsync(1) ?? new UserSettings();
        var logs = await db.DailyLogs.OrderBy(l => l.Date).ToListAsync();
        var goals = await db.Goals.OrderBy(g => g.CreatedAt).ToListAsync();
        var calorieGoals = await db.CalorieGoals.OrderBy(g => g.CreatedAt).ToListAsync();
        var tdeeEntries = await db.TdeeEntries.OrderBy(t => t.EffectiveFrom).ThenBy(t => t.Id).ToListAsync();

        return new ExportImportDto(
            new ExportSettingsDto(settings.HeightCm, settings.PreferredUnit, settings.AveragingWeeks),
            logs.Select(l => new DailyLogDto(0, l.Date.ToString("yyyy-MM-dd"), l.WeightKg, l.CaloriesKcal, l.Notes, l.IsCheatDay, l.CreatedAt, l.UpdatedAt, l.CaloriesSetOn)).ToList(),
            goals.Select(g => new GoalDto(0, g.TargetWeightKg, g.TargetDate, g.StartingWeightKg, g.StartDate, g.Notes, g.CreatedAt, g.Kind, g.EndedOn, g.EndReason)).ToList(),
            calorieGoals.Select(g => new CalorieGoalDto(0, g.TargetCalories, g.CreatedAt, g.EffectiveFrom)).ToList(),
            CurrentVersion,
            tdeeEntries.Select(t => new TdeeEntryDto(0, t.Kcal, t.EffectiveFrom, t.Source, t.CreatedAt)).ToList()
        );
    }

    public async Task ClearAsync()
    {
        db.DailyLogs.RemoveRange(await db.DailyLogs.ToListAsync());
        db.Goals.RemoveRange(await db.Goals.ToListAsync());
        db.CalorieGoals.RemoveRange(await db.CalorieGoals.ToListAsync());
        db.TdeeEntries.RemoveRange(await db.TdeeEntries.ToListAsync());

        // The settings row always exists (Id = 1); reset it rather than delete it.
        var s = await db.UserSettings.FindAsync(1);
        if (s is null) { s = new UserSettings { Id = 1 }; db.UserSettings.Add(s); }
        s.HeightCm = null;
        s.PreferredUnit = "kg";
        s.AveragingWeeks = 2;

        await db.SaveChangesAsync();
    }

    public async Task ImportAsync(ExportImportDto dto)
    {
        var legacy = dto.Version is null or < 2;

        db.DailyLogs.RemoveRange(await db.DailyLogs.ToListAsync());
        db.Goals.RemoveRange(await db.Goals.ToListAsync());
        db.CalorieGoals.RemoveRange(await db.CalorieGoals.ToListAsync());
        db.TdeeEntries.RemoveRange(await db.TdeeEntries.ToListAsync());

        foreach (var log in dto.DailyLogs)
        {
            var calories = log.CaloriesKcal;
            DateOnly? setOn = log.CaloriesSetOn;
            if (legacy) setOn = calories is null ? null : LegacyUpgrade.DateOf(log.UpdatedAt);

            db.DailyLogs.Add(new DailyLog
            {
                Date = DateOnly.Parse(log.Date),
                WeightKg = log.WeightKg,
                CaloriesKcal = calories,
                CaloriesSetOn = setOn,
                Notes = log.Notes,
                IsCheatDay = log.IsCheatDay,
                CreatedAt = log.CreatedAt,
                UpdatedAt = log.UpdatedAt,
            });
        }

        var orderedGoals = dto.Goals.OrderBy(g => g.CreatedAt).ToList();
        for (var i = 0; i < orderedGoals.Count; i++)
        {
            var goal = orderedGoals[i];
            var endedOn = goal.EndedOn;
            var reason = goal.EndReason;
            if (legacy && i < orderedGoals.Count - 1)
            {
                endedOn = LegacyUpgrade.DateOf(orderedGoals[i + 1].CreatedAt);
                reason = GoalEndReason.Replaced;
            }

            db.Goals.Add(new Goal
            {
                TargetWeightKg = goal.TargetWeightKg,
                TargetDate = goal.TargetDate,
                StartingWeightKg = goal.StartingWeightKg,
                StartDate = goal.StartDate,
                Notes = goal.Notes,
                Kind = goal.Kind,
                EndedOn = endedOn,
                EndReason = reason,
                CreatedAt = goal.CreatedAt,
            });
        }

        foreach (var cg in dto.CalorieGoals)
        {
            db.CalorieGoals.Add(new CalorieGoal
            {
                TargetCalories = cg.TargetCalories,
                EffectiveFrom = cg.EffectiveFrom ?? LegacyUpgrade.DateOf(cg.CreatedAt),
                CreatedAt = cg.CreatedAt,
            });
        }

        if (dto.TdeeEntries is { Count: > 0 })
        {
            foreach (var t in dto.TdeeEntries)
                db.TdeeEntries.Add(new TdeeEntry { Kcal = t.Kcal, EffectiveFrom = t.EffectiveFrom, Source = t.Source, CreatedAt = t.CreatedAt });
        }
        else if (dto.Settings.TdeeKcal is { } kcal)
        {
            // One entry effective from the earliest log, so past deficits don't change.
            var first = dto.DailyLogs.Select(l => DateOnly.Parse(l.Date)).DefaultIfEmpty(DateOnly.FromDateTime(DateTime.UtcNow)).Min();
            db.TdeeEntries.Add(new TdeeEntry { Kcal = kcal, EffectiveFrom = first, Source = TdeeSource.Manual });
        }

        var s = await db.UserSettings.FindAsync(1);
        if (s is null) { s = new UserSettings { Id = 1 }; db.UserSettings.Add(s); }
        s.HeightCm = dto.Settings.HeightCm;
        s.PreferredUnit = dto.Settings.PreferredUnit;
        s.AveragingWeeks = dto.Settings.AveragingWeeks is >= 1 and <= 5 ? dto.Settings.AveragingWeeks : 2;

        await db.SaveChangesAsync();
    }
}
