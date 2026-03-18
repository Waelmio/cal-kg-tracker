using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class SettingsService(AppDbContext db) : ISettingsService
{
    public async Task<UserSettingsDto> GetAsync()
    {
        var s = await db.UserSettings.FindAsync(1) ?? new UserSettings();
        return new UserSettingsDto(s.HeightCm, s.PreferredUnit, s.TdeeKcal);
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
        s.TdeeKcal = dto.TdeeKcal;
        await db.SaveChangesAsync();
        return new UserSettingsDto(s.HeightCm, s.PreferredUnit, s.TdeeKcal);
    }
}
