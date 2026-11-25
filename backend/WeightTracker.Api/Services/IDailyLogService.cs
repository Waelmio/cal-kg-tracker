using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum LogField { Calories, Weight }

/// <summary>One field of one day, and who the value comes from.</summary>
public record LogWrite(DateOnly Date, LogField Field, decimal Value, LogSource Source);

public interface IDailyLogService
{
    Task<List<DailyLogDto>> GetAllAsync(DateOnly? from, DateOnly? to, int? limit);
    Task<DailyLogDto?> GetByDateAsync(DateOnly date);

    /// <summary>
    /// Replaces the whole day. Returns null when nothing is left (no weight, calories, note or cheat mark),
    /// in which case the row is removed.
    /// </summary>
    Task<DailyLogDto?> UpsertAsync(UpsertDailyLogDto dto, DateOnly today);

    /// <summary>Delete entry: removes the day with everything in it, note and cheat mark included.</summary>
    Task<bool> DeleteDayAsync(DateOnly date);

    /// <summary>
    /// Single-field writes for imports, all or nothing: each sets only the calories (or only the weight) of a day and
    /// leaves everything else alone. Calories count as set on <paramref name="today"/>, also when the value is the same
    /// (choosing to keep a value is setting it).
    /// </summary>
    Task WriteFieldsAsync(IReadOnlyCollection<LogWrite> writes, DateOnly today);
}
