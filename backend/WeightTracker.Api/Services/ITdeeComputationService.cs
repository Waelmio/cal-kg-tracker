using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface ITdeeComputationService
{
    Task<TdeeComputationDto?> ComputeAsync(int days);
}
