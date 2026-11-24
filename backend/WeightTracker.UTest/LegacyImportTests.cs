using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;

namespace WeightTracker.UTest;

public class LegacyImportTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Import_OfTodaysFormat_AcceptsNoVersion_AndKeepsEveryValue()
    {
        // an export as written before the redesign: no version, no CaloriesSetOn, no EffectiveFrom
        const string json = """
        {
          "settings": { "heightCm": 178, "preferredUnit": "kg", "tdeeKcal": 2350 },
          "dailyLogs": [
            { "id": 1, "date": "2026-11-12", "weightKg": null, "caloriesKcal": 1850, "notes": null, "calorieTarget": null, "isCheatDay": false, "createdAt": "2026-11-10T08:00:00Z", "updatedAt": "2026-11-10T08:00:00Z" },
            { "id": 2, "date": "2026-11-13", "weightKg": null, "caloriesKcal": 1850, "notes": null, "calorieTarget": null, "isCheatDay": false, "createdAt": "2026-11-10T08:00:00Z", "updatedAt": "2026-11-10T08:00:00Z" },
            { "id": 3, "date": "2026-11-11", "weightKg": 80.25, "caloriesKcal": 2100, "notes": null, "calorieTarget": null, "isCheatDay": false, "createdAt": "2026-11-10T08:00:00Z", "updatedAt": "2026-11-11T20:00:00Z" }
          ],
          "goals": [
            { "id": 1, "targetWeightKg": 78, "targetDate": "2027-01-01", "startingWeightKg": 88, "startDate": "2026-06-01", "notes": null, "createdAt": "2026-06-01T10:00:00Z" },
            { "id": 2, "targetWeightKg": 77, "targetDate": "2027-03-01", "startingWeightKg": 86, "startDate": "2026-09-10", "notes": null, "createdAt": "2026-09-10T10:00:00Z" }
          ],
          "calorieGoals": [ { "id": 1, "targetCalories": 1850, "createdAt": "2026-10-15T09:00:00Z" } ]
        }
        """;
        var dto = JsonSerializer.Deserialize<ExportImportDto>(json, Json)!;
        Assert.Null(dto.Version);

        using var db = CreateDb();
        await new DataService(db).ImportAsync(dto);

        var logs = await db.DailyLogs.OrderBy(l => l.Date).ToListAsync();
        Assert.Equal(3, logs.Count);                           // nothing is deleted, auto-filled looking days included
        Assert.All(logs.Where(l => l.Date.Day != 11), l => Assert.Equal(1850, l.CaloriesKcal));
        Assert.Equal(new DateOnly(2026, 11, 10), logs[1].CaloriesSetOn);
        Assert.Equal(new DateOnly(2026, 11, 11), logs[0].CaloriesSetOn);
        Assert.Equal(80.25m, logs[0].WeightKg);

        var cg = await db.CalorieGoals.SingleAsync();
        Assert.Equal(new DateOnly(2026, 10, 15), cg.EffectiveFrom);

        var goals = await db.Goals.OrderBy(g => g.CreatedAt).ToListAsync();
        Assert.Equal(new DateOnly(2026, 9, 10), goals[0].EndedOn);
        Assert.Equal(GoalEndReason.Replaced, goals[0].EndReason);
        Assert.Null(goals[1].EndedOn);

        var tdee = await db.TdeeEntries.SingleAsync();
        Assert.Equal(2350, tdee.Kcal);
        Assert.Equal(new DateOnly(2026, 11, 11), tdee.EffectiveFrom);
    }

    [Fact]
    public async Task ExportV2_RoundTripsWithoutChangingData()
    {
        using var db = CreateDb();
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2026, 11, 1), CaloriesKcal = 0, CaloriesSetOn = new DateOnly(2026, 10, 30), WeightKg = 80.25m });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = null, EffectiveFrom = new DateOnly(2026, 11, 1) });
        db.Goals.Add(new Goal { TargetWeightKg = 80, StartDate = new DateOnly(2026, 1, 1), Kind = GoalKind.Maintain, EndedOn = new DateOnly(2026, 5, 1), EndReason = GoalEndReason.Maintain });
        db.TdeeEntries.Add(new TdeeEntry { Kcal = 2400, EffectiveFrom = new DateOnly(2026, 1, 1), Source = TdeeSource.Estimate });
        await db.SaveChangesAsync();

        var export = await new DataService(db).ExportAsync();
        Assert.Equal(2, export.Version);

        var roundTripped = JsonSerializer.Deserialize<ExportImportDto>(JsonSerializer.Serialize(export, Json), Json)!;
        using var db2 = CreateDb();
        await new DataService(db2).ImportAsync(roundTripped);

        var log = await db2.DailyLogs.SingleAsync();
        Assert.Equal(0, log.CaloriesKcal);                      // 0 kcal is a real value
        Assert.Equal(new DateOnly(2026, 10, 30), log.CaloriesSetOn);
        Assert.Null((await db2.CalorieGoals.SingleAsync()).TargetCalories);
        var goal = await db2.Goals.SingleAsync();
        Assert.Equal(GoalKind.Maintain, goal.Kind);
        Assert.Null(goal.TargetDate);
        Assert.Equal(TdeeSource.Estimate, (await db2.TdeeEntries.SingleAsync()).Source);
    }

    [Fact]
    public async Task Export_HasNoIds_AndAFileWithoutIdsImports()
    {
        using var db = CreateDb();
        db.DailyLogs.Add(new DailyLog { Date = new DateOnly(2026, 11, 1), CaloriesKcal = 1900, WeightKg = 80m });
        db.Goals.Add(new Goal { TargetWeightKg = 75, TargetDate = new DateOnly(2027, 1, 1), StartDate = new DateOnly(2026, 6, 1) });
        db.CalorieGoals.Add(new CalorieGoal { TargetCalories = 1900, EffectiveFrom = new DateOnly(2026, 6, 1) });
        db.TdeeEntries.Add(new TdeeEntry { Kcal = 2400, EffectiveFrom = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(await new DataService(db).ExportAsync(), Json);

        using var doc = JsonDocument.Parse(json);
        foreach (var list in new[] { "dailyLogs", "goals", "calorieGoals", "tdeeEntries" })
            foreach (var row in doc.RootElement.GetProperty(list).EnumerateArray())
                Assert.False(row.TryGetProperty("id", out _), $"{list} rows must not carry an id");

        using var db2 = CreateDb();
        await new DataService(db2).ImportAsync(JsonSerializer.Deserialize<ExportImportDto>(json, Json)!);
        Assert.Equal(1900, (await db2.DailyLogs.SingleAsync()).CaloriesKcal);
        Assert.Single(await db2.Goals.ToListAsync());
        Assert.Single(await db2.TdeeEntries.ToListAsync());
    }
}
