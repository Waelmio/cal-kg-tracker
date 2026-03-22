using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class DataServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_ReturnsEmptyCollections_WhenDatabaseIsEmpty()
    {
        using var db = CreateDb();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.Empty(result.DailyLogs);
        Assert.Empty(result.Goals);
        Assert.Empty(result.CalorieGoals);
    }

    [Fact]
    public async Task ExportAsync_IncludesDefaultSettings()
    {
        using var db = CreateDb();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.NotNull(result.Settings);
    }

    [Fact]
    public async Task ExportAsync_ExportsDailyLogsOrderedByDateAscending()
    {
        using var db = CreateDb();
        db.DailyLogs.AddRange(
            new DailyLog { Date = new DateOnly(2024, 1, 20), WeightKg = 74m },
            new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 75m },
            new DailyLog { Date = new DateOnly(2024, 1, 15), WeightKg = 74.5m }
        );
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.Equal(3, result.DailyLogs.Count);
        Assert.Equal("2024-01-10", result.DailyLogs[0].Date);
        Assert.Equal("2024-01-15", result.DailyLogs[1].Date);
        Assert.Equal("2024-01-20", result.DailyLogs[2].Date);
    }

    [Fact]
    public async Task ExportAsync_ExportsAllDailyLogFields()
    {
        using var db = CreateDb();
        var created = new DateTime(2024, 1, 10, 12, 0, 0, DateTimeKind.Utc);
        db.DailyLogs.Add(new DailyLog
        {
            Date = new DateOnly(2024, 1, 10),
            WeightKg = 75.5m,
            CaloriesKcal = 2000,
            Notes = "test note",
            CreatedAt = created,
            UpdatedAt = created,
        });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        var log = result.DailyLogs[0];
        Assert.Equal("2024-01-10", log.Date);
        Assert.Equal(75.5m, log.WeightKg);
        Assert.Equal(2000, log.CaloriesKcal);
        Assert.Equal("test note", log.Notes);
        Assert.Equal(created, log.CreatedAt);
    }

    [Fact]
    public async Task ExportAsync_ExportsGoalsOrderedByCreatedAtAscending()
    {
        using var db = CreateDb();
        var older = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Goals.AddRange(
            new Goal { TargetWeightKg = 70m, TargetDate = new DateOnly(2024, 12, 31), StartDate = new DateOnly(2024, 2, 1), CreatedAt = newer },
            new Goal { TargetWeightKg = 72m, TargetDate = new DateOnly(2024, 6, 30), StartDate = new DateOnly(2024, 1, 1), CreatedAt = older }
        );
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.Equal(2, result.Goals.Count);
        Assert.Equal(72m, result.Goals[0].TargetWeightKg);
        Assert.Equal(70m, result.Goals[1].TargetWeightKg);
    }

    [Fact]
    public async Task ExportAsync_ExportsCalorieGoalsOrderedByCreatedAtAscending()
    {
        using var db = CreateDb();
        var older = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        db.CalorieGoals.AddRange(
            new CalorieGoal { TargetCalories = 1800, CreatedAt = newer },
            new CalorieGoal { TargetCalories = 2000, CreatedAt = older }
        );
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.Equal(2, result.CalorieGoals.Count);
        Assert.Equal(2000, result.CalorieGoals[0].TargetCalories);
        Assert.Equal(1800, result.CalorieGoals[1].TargetCalories);
    }

    [Fact]
    public async Task ExportAsync_ExportsCurrentSettings()
    {
        using var db = CreateDb();
        db.UserSettings.Add(new UserSettings { Id = 1, HeightCm = 175, PreferredUnit = "lbs", TdeeKcal = 2200 });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var result = await service.ExportAsync();

        Assert.Equal(175, result.Settings.HeightCm);
        Assert.Equal("lbs", result.Settings.PreferredUnit);
        Assert.Equal(2200, result.Settings.TdeeKcal);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ImportAsync_ClearsExistingDailyLogs()
    {
        using var db = CreateDb();
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 75m });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [],
            [],
            []
        ));

        Assert.Equal(0, await db.DailyLogs.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_ClearsExistingGoals()
    {
        using var db = CreateDb();
        db.Goals.Add(new Goal { TargetWeightKg = 70m, TargetDate = new DateOnly(2024, 12, 31), StartDate = new DateOnly(2024, 1, 1) });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [],
            [],
            []
        ));

        Assert.Equal(0, await db.Goals.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_ClearsExistingCalorieGoals()
    {
        using var db = CreateDb();
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2000 });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [],
            [],
            []
        ));

        Assert.Equal(0, await db.CalorieGoals.CountAsync());
    }

    [Fact]
    public async Task ImportAsync_InsertsNewDailyLogs()
    {
        using var db = CreateDb();
        var service = new DataService(db);
        var ts = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [new DailyLogDto(0, "2024-01-10", 75.5m, 2000, "note", null, false, ts, ts)],
            [],
            []
        ));

        var logs = await db.DailyLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal(new DateOnly(2024, 1, 10), logs[0].Date);
        Assert.Equal(75.5m, logs[0].WeightKg);
        Assert.Equal(2000, logs[0].CaloriesKcal);
        Assert.Equal("note", logs[0].Notes);
        Assert.Equal(ts, logs[0].CreatedAt);
    }

    [Fact]
    public async Task ImportAsync_InsertsNewGoals()
    {
        using var db = CreateDb();
        var service = new DataService(db);
        var ts = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [],
            [new GoalDto(0, 70m, new DateOnly(2024, 12, 31), 75m, new DateOnly(2024, 1, 1), "my goal", ts)],
            []
        ));

        var goals = await db.Goals.ToListAsync();
        Assert.Single(goals);
        Assert.Equal(70m, goals[0].TargetWeightKg);
        Assert.Equal(new DateOnly(2024, 12, 31), goals[0].TargetDate);
        Assert.Equal(75m, goals[0].StartingWeightKg);
        Assert.Equal("my goal", goals[0].Notes);
        Assert.Equal(ts, goals[0].CreatedAt);
    }

    [Fact]
    public async Task ImportAsync_InsertsNewCalorieGoals()
    {
        using var db = CreateDb();
        var service = new DataService(db);
        var ts = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [],
            [],
            [new CalorieGoalDto(0, 1800, ts)]
        ));

        var goals = await db.CalorieGoals.ToListAsync();
        Assert.Single(goals);
        Assert.Equal(1800, goals[0].TargetCalories);
        Assert.Equal(ts, goals[0].CreatedAt);
    }

    [Fact]
    public async Task ImportAsync_UpdatesSettings()
    {
        using var db = CreateDb();
        db.UserSettings.Add(new UserSettings { Id = 1, HeightCm = 170, PreferredUnit = "kg", TdeeKcal = 2000 });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(185, "lbs", 2500),
            [],
            [],
            []
        ));

        var settings = await db.UserSettings.FindAsync(1);
        Assert.Equal(185, settings!.HeightCm);
        Assert.Equal("lbs", settings.PreferredUnit);
        Assert.Equal(2500, settings.TdeeKcal);
    }

    [Fact]
    public async Task ImportAsync_CreatesSettings_WhenNoneExist()
    {
        using var db = CreateDb();
        var service = new DataService(db);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(175, "kg", 1900),
            [],
            [],
            []
        ));

        var settings = await db.UserSettings.FindAsync(1);
        Assert.NotNull(settings);
        Assert.Equal(175, settings.HeightCm);
        Assert.Equal(1900, settings.TdeeKcal);
    }

    [Fact]
    public async Task ImportAsync_ReplacesAllData_WhenExistingDataPresent()
    {
        using var db = CreateDb();
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2023, 6, 1), WeightKg = 80m });
        db.Goals.Add(new Goal { TargetWeightKg = 75m, TargetDate = new DateOnly(2023, 12, 31), StartDate = new DateOnly(2023, 6, 1) });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 2200 });
        await db.SaveChangesAsync();
        var service = new DataService(db);
        var ts = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await service.ImportAsync(new ExportImportDto(
            new UserSettingsDto(null, "kg", null),
            [new DailyLogDto(0, "2024-01-10", 70m, null, null, null, false, ts, ts)],
            [],
            []
        ));

        var logs = await db.DailyLogs.ToListAsync();
        Assert.Single(logs);
        Assert.Equal("2024-01-10", logs[0].Date.ToString("yyyy-MM-dd"));
        Assert.Equal(0, await db.Goals.CountAsync());
        Assert.Equal(0, await db.CalorieGoals.CountAsync());
    }

    [Fact]
    public async Task ExportThenImport_IsIdempotent()
    {
        using var db = CreateDb();
        var ts = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        db.UserSettings.Add(new UserSettings { Id = 1, HeightCm = 175, PreferredUnit = "kg", TdeeKcal = 2000 });
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2024, 1, 10), WeightKg = 75m, CaloriesKcal = 2000, CreatedAt = ts, UpdatedAt = ts });
        db.Goals.Add(new Goal { TargetWeightKg = 70m, TargetDate = new DateOnly(2024, 12, 31), StartDate = new DateOnly(2024, 1, 1), CreatedAt = ts });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1800, CreatedAt = ts });
        await db.SaveChangesAsync();
        var service = new DataService(db);

        var exported = await service.ExportAsync();
        await service.ImportAsync(exported);
        var reimported = await service.ExportAsync();

        Assert.Equal(exported.Settings.HeightCm, reimported.Settings.HeightCm);
        Assert.Equal(exported.Settings.PreferredUnit, reimported.Settings.PreferredUnit);
        Assert.Single(reimported.DailyLogs);
        Assert.Equal(exported.DailyLogs[0].Date, reimported.DailyLogs[0].Date);
        Assert.Equal(exported.DailyLogs[0].WeightKg, reimported.DailyLogs[0].WeightKg);
        Assert.Single(reimported.Goals);
        Assert.Equal(exported.Goals[0].TargetWeightKg, reimported.Goals[0].TargetWeightKg);
        Assert.Single(reimported.CalorieGoals);
        Assert.Equal(exported.CalorieGoals[0].TargetCalories, reimported.CalorieGoals[0].TargetCalories);
    }
}
