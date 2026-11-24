using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class SettingsService(AppDbContext db) : ISettingsService
{
    public async Task<UserSettingsDto> GetAsync()
    {
        var s = await db.UserSettings.FindAsync(1) ?? new UserSettings();
        return new UserSettingsDto(s.HeightCm, s.PreferredUnit, s.TdeeKcal, s.AveragingWeeks);
    }

    public async Task<UserSettingsDto> UpdateAsync(UpdateUserSettingsDto dto)
    {
        var s = await db.UserSettings.FindAsync(1);
        if (s is null)
        {
            s = new UserSettings { Id = 1 };
            db.UserSettings.Add(s);
        }
        s.HeightCm = dto.HeightCm;
        s.PreferredUnit = dto.PreferredUnit;
        if (dto.TdeeKcal != s.TdeeKcal)
        {
            // Keep the TDEE history in step with the legacy single value until the cleanup phase.
            if (dto.TdeeKcal is { } kcal)
            {
                var from = dto.Today ?? DateOnly.FromDateTime(DateTime.UtcNow);
                db.TdeeEntries.RemoveRange(db.TdeeEntries.Where(t => t.EffectiveFrom == from));
                db.TdeeEntries.Add(new TdeeEntry { Kcal = kcal, EffectiveFrom = from, Source = TdeeSource.Manual });
            }
            s.TdeeKcal = dto.TdeeKcal;
        }
        if (dto.AveragingWeeks is { } weeks) s.AveragingWeeks = weeks;
        await db.SaveChangesAsync();
        return new UserSettingsDto(s.HeightCm, s.PreferredUnit, s.TdeeKcal, s.AveragingWeeks);
    }
}
