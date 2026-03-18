using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IDataService
{
    Task<ExportImportDto> ExportAsync();
    Task ImportAsync(ExportImportDto dto);
}
