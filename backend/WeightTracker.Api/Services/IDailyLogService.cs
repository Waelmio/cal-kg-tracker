using WeightTracker.Api.DTOs;

namespace WeightTracker.Api.Services;

public interface IDailyLogService
{
    Task<List<DailyLogDto>> GetAllAsync(DateOnly? from, DateOnly? to, int? limit);
    Task<DailyLogDto?> GetByDateAsync(DateOnly date);

    /// <summary>
    /// Replaces the whole day. Returns null when nothing is left (no weight, calories, note or cheat mark),
    /// in which case the row is removed.
    /// </summary>
    Task<DailyLogDto?> UpsertAsync(UpsertDailyLogDto dto, DateOnly? today = null);

    /// <summary>Delete entry: removes the day with everything in it, note and cheat mark included.</summary>
    Task<bool> DeleteDayAsync(DateOnly date);

    /// <summary>
    /// Single-field write for imports: sets only the calories (or only the weight) of a day and leaves everything else alone.
    /// </summary>
    Task<DailyLogDto> SetCaloriesAsync(DateOnly date, int kcal, DateOnly today);
    Task<DailyLogDto> SetWeightAsync(DateOnly date, decimal kg);
}
