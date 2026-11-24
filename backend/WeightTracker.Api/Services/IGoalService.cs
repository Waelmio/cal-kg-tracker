using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public class GoalException(string title, int status = 400) : Exception(title)
{
    public int Status { get; } = status;
}

public interface IGoalService
{
    Task<List<GoalDto>> GetAllAsync();
    Task<GoalDto?> GetActiveAsync();
    Task<GoalDto> CreateAsync(CreateGoalDto dto, DateOnly? today = null);
    Task<GoalDto?> UpdateAsync(int id, UpdateGoalDto dto);
    Task<GoalDto?> EndAsync(int id, EndGoalDto dto, DateOnly today);
    Task<GoalDto> MaintainAsync(DateOnly today);
    Task<bool> DeleteAsync(int id);
}
