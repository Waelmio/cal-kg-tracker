using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

public interface ITdeeEntryService
{
    Task<List<TdeeEntryDto>> GetAllAsync();
    Task<TdeeEntryDto> CreateAsync(CreateTdeeEntryDto dto, DateOnly today);
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
    /// </summary>
    public async Task<TdeeEntryDto> CreateAsync(CreateTdeeEntryDto dto, DateOnly today)
    {
        var from = dto.EffectiveFrom ?? today;
        db.TdeeEntries.RemoveRange(await db.TdeeEntries.Where(t => t.EffectiveFrom == from).ToListAsync());

        var entry = new TdeeEntry { Kcal = dto.Kcal, EffectiveFrom = from, Source = dto.Source ?? TdeeSource.Manual };
        db.TdeeEntries.Add(entry);

        // Legacy single value, kept until the cleanup phase so the old screens keep working.
        var settings = await db.UserSettings.FindAsync(1);
        if (settings is not null && from <= today) settings.TdeeKcal = dto.Kcal;

        await db.SaveChangesAsync();
        return ToDto(entry);
    }

    private static TdeeEntryDto ToDto(TdeeEntry t) => new(t.Id, t.Kcal, t.EffectiveFrom, t.Source, t.CreatedAt);
}
