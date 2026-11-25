using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public interface ITdeeEntryService
{
    Task<List<TdeeEntryDto>> GetAllAsync();
    Task<TdeeEntryDto> CreateAsync(CreateTdeeEntryDto dto, DateOnly today);
    Task<bool> DeleteAsync(int id);
}

public class TdeeEntryService(AppDbContext db) : ITdeeEntryService
{
    public async Task<List<TdeeEntryDto>> GetAllAsync()
    {
        var entries = await db.TdeeEntries.AsNoTracking()
            .OrderByDescending(t => t.EffectiveFrom).ThenByDescending(t => t.Id).ToListAsync();
        return entries.Select(ToDto).ToList();
    }

    /// <summary>
    /// Adds a TDEE value effective from a day (default today). It never rewrites earlier days:
    /// past deficits keep the TDEE that was in force. A second entry for the same day replaces the first.
    /// The very first entry covers the days logged before it (nothing was in force then), as the migration does.
    /// </summary>
    public async Task<TdeeEntryDto> CreateAsync(CreateTdeeEntryDto dto, DateOnly today)
    {
        var from = dto.EffectiveFrom ?? await DefaultFromAsync(today);
        db.TdeeEntries.RemoveRange(await db.TdeeEntries.Where(t => t.EffectiveFrom == from).ToListAsync());

        var entry = new TdeeEntry { Kcal = dto.Kcal, EffectiveFrom = from, Source = dto.Source ?? TdeeSource.Manual };
        db.TdeeEntries.Add(entry);

        await db.SaveChangesAsync();
        return ToDto(entry);
    }

    /// <summary>Removes a value: its days fall back to the entry before it, if any.</summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var entry = await db.TdeeEntries.FindAsync(id);
        if (entry is null) return false;
        db.TdeeEntries.Remove(entry);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task<DateOnly> DefaultFromAsync(DateOnly today)
    {
        if (await db.TdeeEntries.AnyAsync()) return today;
        var first = await db.DailyLogs.Select(l => (DateOnly?)l.Date).MinAsync();
        return first < today ? first.Value : today;
    }

    private static TdeeEntryDto ToDto(TdeeEntry t) => new(t.Id, t.Kcal, t.EffectiveFrom, t.Source, t.CreatedAt);
}
