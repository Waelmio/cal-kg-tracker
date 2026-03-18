using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface ISettingsService
{
    Task<UserSettingsDto> GetAsync();
    Task<UserSettingsDto> UpdateAsync(UpdateUserSettingsDto dto);
}
