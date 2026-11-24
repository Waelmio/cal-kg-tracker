using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IGoalService
{
    Task<List<GoalDto>> GetAllAsync();
    Task<GoalDto?> GetActiveAsync();
    Task<GoalDto> CreateAsync(CreateGoalDto dto, DateOnly? today = null);
    Task<bool> DeleteAsync(int id);
}
