using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(DateOnly today);
}
