using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IDataService
{
    Task<ExportImportDto> ExportAsync();
    Task ImportAsync(ExportImportDto dto);

    /// <summary>Deletes every log, goal, calorie target and TDEE entry, and resets the settings to their defaults.</summary>
    Task ClearAsync();
}
