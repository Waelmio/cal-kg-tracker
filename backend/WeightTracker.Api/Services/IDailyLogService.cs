using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IDailyLogService
{
    Task<List<DailyLogDto>> GetAllAsync(DateOnly? from, DateOnly? to, int? limit);
    Task<DailyLogDto?> GetByDateAsync(DateOnly date);
    Task<DailyLogDto> UpsertAsync(UpsertDailyLogDto dto);
    Task<bool> DeleteDayAsync(DateOnly date);
    Task<DailyLogDto?> DeleteWeightAsync(DateOnly date);
    Task<DailyLogDto?> DeleteCaloriesAsync(DateOnly date);
    Task<DailyLogDto> SetCheatDayAsync(DateOnly date, bool isCheatDay);
    Task PrefillWeekAsync(DateOnly today);
}
