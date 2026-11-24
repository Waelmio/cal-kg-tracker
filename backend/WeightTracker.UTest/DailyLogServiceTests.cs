using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class DailyLogServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static DailyLogService CreateService(AppDbContext db) => new(db, new CalorieLogService(db));

    private static UpsertDailyLogDto Put(string date, decimal? kg = null, int? kcal = null, string? notes = null, bool cheat = false) =>
        new(date, kg, kcal, notes, cheat);

    // ── GetAll / GetByDate ────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoLogs()
    {
        using var db = CreateDb();
        Assert.Empty(await CreateService(db).GetAllAsync(null, null, null));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsLogsOrderedByDateDesc_AndFiltersByRangeAndLimit()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        foreach (var d in new[] { "2024-01-10", "2024-01-15", "2024-01-12" })
            await svc.UpsertAsync(Put(d, kg: 75m), Today);

        var all = await svc.GetAllAsync(null, null, null);
        Assert.Equal(new[] { "2024-01-15", "2024-01-12", "2024-01-10" }, all.Select(l => l.Date));

        var range = await svc.GetAllAsync(new DateOnly(2024, 1, 11), new DateOnly(2024, 1, 14), null);
        Assert.Single(range);
        Assert.Equal(2, (await svc.GetAllAsync(null, null, 2)).Count);
    }

    [Fact]
    public async Task GetByDateAsync_ReturnsNull_WhenMissing()
    {
        using var db = CreateDb();
        Assert.Null(await CreateService(db).GetByDateAsync(new DateOnly(2024, 1, 1)));
    }

    // ── Full replace ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Upsert_CreatesTheDay()
    {
        using var db = CreateDb();
        var saved = await CreateService(db).UpsertAsync(Put("2024-03-01", 80.256m, 1900, "ok"), Today);

        Assert.Equal(80.26m, saved!.WeightKg);                 // 2 decimals
        Assert.Equal(1900, saved.CaloriesKcal);
        Assert.Equal("ok", saved.Notes);
        Assert.Equal(Today, saved.CaloriesSetOn);
    }

    [Fact]
    public async Task Upsert_ReplacesTheWholeDay_NullClearsAField()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2024-03-01", 80m, 1900, "note", cheat: true), Today);

        var saved = await svc.UpsertAsync(Put("2024-03-01", kg: null, kcal: 2000, notes: null, cheat: false), Today);

        Assert.Null(saved!.WeightKg);
        Assert.Equal(2000, saved.CaloriesKcal);
        Assert.Null(saved.Notes);
        Assert.False(saved.IsCheatDay);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task Upsert_ZeroKcalIsARealValue()
    {
        using var db = CreateDb();
        var saved = await CreateService(db).UpsertAsync(Put("2024-03-01", kcal: 0), Today);
        Assert.Equal(0, saved!.CaloriesKcal);
        Assert.Equal(0, (await db.DailyLogs.SingleAsync()).CaloriesKcal);
    }

    [Fact]
    public async Task Upsert_RemovingOneFieldNeverDeletesTheNote()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2024-03-01", 80m, 1900, "keep me"), Today);

        var saved = await svc.UpsertAsync(Put("2024-03-01", kg: null, kcal: null, notes: "keep me"), Today);

        Assert.NotNull(saved);
        Assert.Equal("keep me", (await db.DailyLogs.SingleAsync()).Notes);
    }

    [Fact]
    public async Task Upsert_WithNothingLeft_RemovesTheRow_AndReturnsNull()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2024-03-01", 80m, 1900), Today);

        Assert.Null(await svc.UpsertAsync(Put("2024-03-01"), Today));
        Assert.Empty(await db.DailyLogs.ToListAsync());
        Assert.Null(await svc.UpsertAsync(Put("2024-03-02", notes: "  "), Today));   // nothing was ever stored
        Assert.Empty(await db.DailyLogs.ToListAsync());
    }

    [Fact]
    public async Task Upsert_CheatMarkAloneKeepsTheRow()
    {
        using var db = CreateDb();
        var saved = await CreateService(db).UpsertAsync(Put("2024-03-01", cheat: true), Today);
        Assert.True(saved!.IsCheatDay);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    // ── CaloriesSetOn ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CaloriesSetOn_IsTheDayTheValueChanged_NotTheDayOfTheLog()
    {
        using var db = CreateDb();
        var svc = CreateService(db);

        // Tuesday: log Saturday in advance
        var tuesday = new DateOnly(2026, 11, 24);
        var saved = await svc.UpsertAsync(Put("2026-11-28", kcal: 2500), tuesday);
        Assert.Equal(tuesday, saved!.CaloriesSetOn);

        // Wednesday: only a weight and a note change, the calories value is the same
        var wednesday = tuesday.AddDays(1);
        saved = await svc.UpsertAsync(Put("2026-11-28", kg: 80m, kcal: 2500, notes: "x"), wednesday);
        Assert.Equal(tuesday, saved!.CaloriesSetOn);

        // Thursday: the value itself changes
        saved = await svc.UpsertAsync(Put("2026-11-28", kg: 80m, kcal: 2400, notes: "x"), wednesday.AddDays(1));
        Assert.Equal(wednesday.AddDays(1), saved!.CaloriesSetOn);
    }

    [Fact]
    public async Task CaloriesSetOn_IsClearedWithTheCalories()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2026-11-20", kg: 80m, kcal: 2000), Today);
        var saved = await svc.UpsertAsync(Put("2026-11-20", kg: 80m), Today);
        Assert.Null(saved!.CaloriesSetOn);
    }

    // ── Delete entry ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteDay_RemovesEverything_NoteAndCheatMarkIncluded()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2024-03-01", 80m, 1900, "note", cheat: true), Today);

        Assert.True(await svc.DeleteDayAsync(new DateOnly(2024, 3, 1)));
        Assert.Empty(await db.DailyLogs.ToListAsync());
        Assert.False(await svc.DeleteDayAsync(new DateOnly(2024, 3, 1)));
    }

    // ── Single-field writes (for imports) ─────────────────────────────────────

    [Fact]
    public async Task SetCalories_TouchesOnlyCalories()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2026-11-20", 80m, null, "note", cheat: true), Today);

        var saved = await svc.SetCaloriesAsync(new DateOnly(2026, 11, 20), 1750, Today);

        Assert.Equal(1750, saved.CaloriesKcal);
        Assert.Equal(80m, saved.WeightKg);
        Assert.Equal("note", saved.Notes);
        Assert.True(saved.IsCheatDay);
        Assert.Equal(Today, saved.CaloriesSetOn);
    }

    [Fact]
    public async Task SetCalories_CreatesTheRow_AndKeepsSetOnWhenTheValueIsUnchanged()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        var first = await svc.SetCaloriesAsync(new DateOnly(2026, 11, 20), 1750, Today);
        var again = await svc.SetCaloriesAsync(new DateOnly(2026, 11, 20), 1750, Today.AddDays(1));
        Assert.Equal(first.CaloriesSetOn, again.CaloriesSetOn);
        Assert.Equal(1, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task SetWeight_TouchesOnlyWeight()
    {
        using var db = CreateDb();
        var svc = CreateService(db);
        await svc.UpsertAsync(Put("2026-11-20", kcal: 1800), Today);

        var saved = await svc.SetWeightAsync(new DateOnly(2026, 11, 20), 81.234m);

        Assert.Equal(81.23m, saved.WeightKg);
        Assert.Equal(1800, saved.CaloriesKcal);
    }

    // ── Targets on the DTO ────────────────────────────────────────────────────

    [Fact]
    public async Task Dto_CarriesTheTargetInForceThatDay()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1900, EffectiveFrom = new DateOnly(2024, 1, 1) });
        await db.SaveChangesAsync();
        var saved = await CreateService(db).UpsertAsync(Put("2024-03-01", kcal: 2000), Today);
        Assert.Equal(1900, saved!.CalorieTarget);
    }

    [Fact]
    public async Task NothingIsStoredForADayNobodyLogged()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1900, EffectiveFrom = new DateOnly(2024, 1, 1) });
        await db.SaveChangesAsync();
        await CreateService(db).GetAllAsync(null, null, null);
        Assert.Empty(await db.DailyLogs.ToListAsync());
    }
}
