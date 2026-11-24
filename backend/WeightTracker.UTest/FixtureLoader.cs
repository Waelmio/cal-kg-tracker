using System.Text.Json;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Stats;

namespace WeightTracker.UTest;

/// <summary>Loads Fixtures/fixture.json (PLAN.md 3.11): synthetic data, 1 June to 24 Nov 2026, today = 2026-11-24.</summary>
public static class FixtureLoader
{
    public static readonly DateOnly Today = new(2026, 11, 24);

    public static ExportImportDto Export()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fixture.json");
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Deserialize<ExportImportDto>(File.ReadAllText(path), options)!;
    }

    public static StatsData Stats(int window = 2, Action<Dictionary<DateOnly, DayData>>? edit = null)
    {
        var f = Export();
        var days = f.DailyLogs.ToDictionary(
            l => DateOnly.Parse(l.Date),
            l => new DayData(DateOnly.Parse(l.Date), (double?)l.WeightKg, l.CaloriesKcal, null, l.IsCheatDay, l.Notes));
        days.Remove(Today);      // 3.11 is the state before today is logged; tests add it back
        edit?.Invoke(days);
        var first = days.Keys.Min();
        var goal = f.Goals[0];
        return new StatsData
        {
            Today = Today,
            Window = window,
            Days = days,
            Targets = f.CalorieGoals.OrderBy(g => g.CreatedAt)
                .Select(g => (DateOnly.FromDateTime(g.CreatedAt.UtcDateTime), g.TargetCalories)).ToList(),
            Tdees = [(first, f.Settings.TdeeKcal!.Value)],
            Goal = new GoalSpan(goal.StartDate, goal.TargetDate),
        };
    }

    public static StatsCalculator Calc(int window = 2, Action<Dictionary<DateOnly, DayData>>? edit = null) =>
        new(Stats(window, edit));
}
