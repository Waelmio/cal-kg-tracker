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
        return await query.Select(l => ToDto(l)).ToListAsync();
    }

    public async Task<DailyLogDto?> GetByDateAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        return log is null ? null : ToDto(log);
    }

    public async Task<DailyLogDto> UpsertAsync(UpsertDailyLogDto dto)
    {
        var date = DateOnly.Parse(dto.Date);
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);

        if (log is null)
        {
            log = new DailyLog { Date = date };
            db.DailyLogs.Add(log);
        }

        if (dto.WeightKg.HasValue) log.WeightKg = dto.WeightKg;
        if (dto.CaloriesKcal.HasValue) log.CaloriesKcal = dto.CaloriesKcal;
        if (dto.Notes is not null) log.Notes = dto.Notes;
        log.UpdatedAt = DateTime.UtcNow;

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

    public async Task<DailyLogDto?> DeleteWeightAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null) return null;

        log.WeightKg = null;
        log.UpdatedAt = DateTime.UtcNow;

        if (log.CaloriesKcal is null)
        {
            db.DailyLogs.Remove(log);
            await db.SaveChangesAsync();
            return null;
        }

        await db.SaveChangesAsync();
        return ToDto(log);
    }

    public async Task<DailyLogDto?> DeleteCaloriesAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null) return null;

        log.CaloriesKcal = null;
        log.UpdatedAt = DateTime.UtcNow;

        if (log.WeightKg is null)
        {
            db.DailyLogs.Remove(log);
            await db.SaveChangesAsync();
            return null;
        }

        await db.SaveChangesAsync();
        return ToDto(log);
    }

    private static DailyLogDto ToDto(DailyLog l) =>
        new(l.Id, l.Date.ToString("yyyy-MM-dd"), l.WeightKg, l.CaloriesKcal, l.Notes, l.CreatedAt, l.UpdatedAt);
}
