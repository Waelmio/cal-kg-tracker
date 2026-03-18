using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface ICalorieGoalService
{
    Task<List<CalorieGoalDto>> GetAllAsync();
    Task<CalorieGoalDto?> GetActiveAsync();
    Task<CalorieGoalDto> CreateAsync(CreateCalorieGoalDto dto);
    Task<bool> DeleteAsync(int id);
}
