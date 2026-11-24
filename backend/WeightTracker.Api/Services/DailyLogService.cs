using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class DailyLogService(AppDbContext db) : IDailyLogService
{
    public async Task<List<DailyLogDto>> GetAllAsync(DateOnly? from, DateOnly? to, int? limit)
    {
        var query = db.DailyLogs.AsQueryable();
        if (from.HasValue) query = query.Where(l => l.Date >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Date <= to.Value);
        query = query.OrderByDescending(l => l.Date);
        if (limit.HasValue) query = query.Take(limit.Value);

        return (await query.ToListAsync()).Select(ToDto).ToList();
    }

    public async Task<DailyLogDto?> GetByDateAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        return log is null ? null : ToDto(log);
    }

    public async Task<DailyLogDto?> UpsertAsync(UpsertDailyLogDto dto, DateOnly today)
    {
        var date = DateOnly.Parse(dto.Date);
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);

        var notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        var weight = dto.WeightKg is { } w ? Math.Round(w, 2) : (decimal?)null;

        if (weight is null && dto.CaloriesKcal is null && notes is null && !dto.IsCheatDay)
        {
            if (log is not null)
            {
                db.DailyLogs.Remove(log);
                await db.SaveChangesAsync();
            }
            return null;
        }

        if (log is null)
        {
            log = new DailyLog { Date = date };
            db.DailyLogs.Add(log);
        }

        // CaloriesSetOn follows the value only: saving a weight or a note leaves it alone.
        if (log.CaloriesKcal != dto.CaloriesKcal)
            log.CaloriesSetOn = dto.CaloriesKcal is null ? null : today;

        log.WeightKg = weight;
        log.CaloriesKcal = dto.CaloriesKcal;
        log.Notes = notes;
        log.IsCheatDay = dto.IsCheatDay;
        log.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return ToDto(log);
    }

    public async Task<bool> DeleteDayAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null) return false;
        db.DailyLogs.Remove(log);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<DailyLogDto> SetCaloriesAsync(DateOnly date, int kcal, DateOnly today)
    {
        var log = await GetOrAddAsync(date);
        if (log.CaloriesKcal != kcal) log.CaloriesSetOn = today;
        log.CaloriesKcal = kcal;
        log.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(log);
    }

    public async Task<DailyLogDto> SetWeightAsync(DateOnly date, decimal kg)
    {
        var log = await GetOrAddAsync(date);
        log.WeightKg = Math.Round(kg, 2);
        log.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return ToDto(log);
    }

    private async Task<DailyLog> GetOrAddAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null)
        {
            log = new DailyLog { Date = date };
            db.DailyLogs.Add(log);
        }
        return log;
    }

    private static DailyLogDto ToDto(DailyLog l) =>
        new(l.Id, l.Date.ToString("yyyy-MM-dd"), l.WeightKg, l.CaloriesKcal, l.Notes, l.IsCheatDay, l.CreatedAt, l.UpdatedAt, l.CaloriesSetOn);
}
