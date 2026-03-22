using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public class DailyLogService(AppDbContext db, ICalorieLogService calorieLogService) : IDailyLogService
{
    public async Task<List<DailyLogDto>> GetAllAsync(DateOnly? from, DateOnly? to, int? limit)
    {
        var query = db.DailyLogs.AsQueryable();
        if (from.HasValue) query = query.Where(l => l.Date >= from.Value);
        if (to.HasValue) query = query.Where(l => l.Date <= to.Value);
        query = query.OrderByDescending(l => l.Date);
        if (limit.HasValue) query = query.Take(limit.Value);

        var logs = await query.ToListAsync();
        var enriched = await calorieLogService.EnrichLogsAsync(logs);
        return enriched.Select(e => ToDto(e.Log, e.EffectiveTarget)).ToList();
    }

    public async Task<DailyLogDto?> GetByDateAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null) return null;
        var target = await calorieLogService.GetEffectiveTargetAsync(date, log.IsCheatDay);
        return ToDto(log, target);
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
        log.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        var target = await calorieLogService.GetEffectiveTargetAsync(date, log.IsCheatDay);
        return ToDto(log, target);
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
        log.UpdatedAt = DateTimeOffset.UtcNow;

        if (log.CaloriesKcal is null && !log.IsCheatDay)
        {
            db.DailyLogs.Remove(log);
            await db.SaveChangesAsync();
            return null;
        }

        await db.SaveChangesAsync();
        var target = await calorieLogService.GetEffectiveTargetAsync(date, log.IsCheatDay);
        return ToDto(log, target);
    }

    public async Task<DailyLogDto?> DeleteCaloriesAsync(DateOnly date)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null) return null;

        log.CaloriesKcal = null;
        log.UpdatedAt = DateTimeOffset.UtcNow;

        if (log.WeightKg is null && !log.IsCheatDay)
        {
            db.DailyLogs.Remove(log);
            await db.SaveChangesAsync();
            return null;
        }

        await db.SaveChangesAsync();
        var target = await calorieLogService.GetEffectiveTargetAsync(date, log.IsCheatDay);
        return ToDto(log, target);
    }

    public async Task<DailyLogDto> SetCheatDayAsync(DateOnly date, bool isCheatDay)
    {
        var log = await db.DailyLogs.FirstOrDefaultAsync(l => l.Date == date);
        if (log is null)
        {
            log = new DailyLog { Date = date };
            db.DailyLogs.Add(log);
        }

        log.IsCheatDay = isCheatDay;
        log.UpdatedAt = DateTimeOffset.UtcNow;

        // If removing cheat day and nothing else is logged, delete the row
        if (!isCheatDay && log.WeightKg is null && log.CaloriesKcal is null)
        {
            if (log.Id != 0) db.DailyLogs.Remove(log);
            await db.SaveChangesAsync();
            // Return a synthetic empty DTO since the row no longer exists
            return new DailyLogDto(0, date.ToString("yyyy-MM-dd"), null, null, null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync();
        var target = await calorieLogService.GetEffectiveTargetAsync(date, log.IsCheatDay);
        return ToDto(log, target);
    }

    public async Task PrefillWeekAsync(DateOnly today)
    {
        var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
        var monday = today.AddDays(-daysFromMonday);
        var sunday = monday.AddDays(6);

        var existingLogs = await db.DailyLogs
            .Where(l => l.Date >= monday && l.Date <= sunday)
            .ToListAsync();
        var goals = await db.CalorieGoals
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        var logByDate = existingLogs.ToDictionary(l => l.Date);
        var now = DateTimeOffset.UtcNow;

        for (var date = monday; date <= sunday; date = date.AddDays(1))
        {
            if (logByDate.TryGetValue(date, out var log) && log.CaloriesKcal.HasValue)
                continue;

            var dateEndUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            var target = goals.FirstOrDefault(g => g.CreatedAt <= dateEndUtc)?.TargetCalories;
            if (target is null) continue;

            if (log is null)
            {
                log = new DailyLog { Date = date, CreatedAt = now, UpdatedAt = now };
                db.DailyLogs.Add(log);
            }
            else
            {
                log.UpdatedAt = now;
            }
            log.CaloriesKcal = target;
        }

        await db.SaveChangesAsync();
    }

    private static DailyLogDto ToDto(DailyLog l, int? calorieTarget) =>
        new(l.Id, l.Date.ToString("yyyy-MM-dd"), l.WeightKg, l.CaloriesKcal, l.Notes, calorieTarget, l.IsCheatDay, l.CreatedAt, l.UpdatedAt);
}
