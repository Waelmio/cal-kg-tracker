using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class SettingsService(AppDbContext db) : ISettingsService
{
    public async Task<UserSettingsDto> GetAsync() => ToDto(await db.UserSettings.FindAsync(1) ?? new UserSettings());

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
        if (dto.AveragingWeeks is { } weeks) s.AveragingWeeks = weeks;
        await db.SaveChangesAsync();
        return ToDto(s);
    }

    private static UserSettingsDto ToDto(UserSettings s) => new(s.HeightCm, s.PreferredUnit, s.AveragingWeeks);
}
