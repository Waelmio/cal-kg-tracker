using WeightTracker.Api.Models;
using WeightTracker.Api.Services;
using WeightTracker.Api.Stats;

namespace WeightTracker.Api.Integrations;

/// <summary>Which values an import may write, as pure functions. A day the provider has nothing for is never touched.</summary>
public static class SyncRules
{
    /// <summary>From the given number of months back to the end of the current week (Sunday).</summary>
    public static (DateOnly From, DateOnly To) Window(DateOnly today, int months)
    {
        var end = StatsCalculator.SundayOf(today);
        return (end.AddMonths(-months), end);
    }

    /// <summary>Logged before its own day: a plan, which the real value replaces.</summary>
    public static bool SetInAdvance(DailyLog log) => log.CaloriesKcal is not null && log.CaloriesSetOn < log.Date;

    /// <summary>May an import write here without asking? Anything else was logged by the user and is left alone.</summary>
    public static bool IsOpen(DailyLog? log, LogField field, LogSource source) => field == LogField.Calories
        ? log?.CaloriesKcal is null || SetInAdvance(log) || log.CaloriesSource == source
        : log?.WeightKg is null || log.WeightSource == source;

    public static decimal? AppValue(DailyLog? log, LogField field) => field == LogField.Calories ? log?.CaloriesKcal : log?.WeightKg;

    private static decimal? Imported(ImportedDay day, LogField field) => field == LogField.Calories ? day.CaloriesKcal : day.WeightKg;

    /// <summary>
    /// What an automatic sync writes. Calories after today are the provider's plan: they replace whatever the app has and
    /// count as set in advance, so later syncs keep following the provider. There is no weight after today.
    /// </summary>
    public static (List<LogWrite> Writes, FieldSyncResult Calories, FieldSyncResult Weight) Plan(
        IEnumerable<ImportedDay> days, IReadOnlyDictionary<DateOnly, DailyLog> logs, LogSource source, DateOnly today, bool importTodayCalories)
    {
        var writes = new List<LogWrite>();
        FieldSyncResult PlanField(LogField field)
        {
            int written = 0, unchanged = 0, kept = 0;
            foreach (var day in days)
            {
                if (Imported(day, field) is not { } value) continue;
                if (field == LogField.Weight && day.Date > today) continue;
                if (field == LogField.Calories && day.Date >= today && !importTodayCalories) continue;     // not over yet
                var log = logs.GetValueOrDefault(day.Date);
                if (AppValue(log, field) == value) unchanged++;
                else if (!IsOpen(log, field, source)) kept++;
                else
                {
                    writes.Add(new LogWrite(day.Date, field, value, source));
                    written++;
                }
            }
            return new FieldSyncResult(written, unchanged, kept);
        }

        var calories = PlanField(LogField.Calories);
        var weight = PlanField(LogField.Weight);
        return (writes, calories, weight);
    }

    /// <summary>Every difference, for the user to decide. Weights compare to 0.1 kg, so rounding is not a difference.</summary>
    public static (List<SyncRow> Rows, int Equal, int Missing) Compare(
        IReadOnlyCollection<ImportedDay> days, IReadOnlyDictionary<DateOnly, DailyLog> logs, LogSource source)
    {
        var rows = new List<SyncRow>();
        var equal = 0;
        var matched = 0;
        foreach (var day in days.OrderBy(d => d.Date))
        {
            foreach (var field in new[] { LogField.Calories, LogField.Weight })
            {
                if (Imported(day, field) is not { } value) continue;
                var log = logs.GetValueOrDefault(day.Date);
                if (AppValue(log, field) is not { } appValue)
                {
                    rows.Add(new SyncRow(day.Date, field, SyncRowKind.New, null, value, false, false, SyncChoice.Replace));
                    continue;
                }
                matched++;
                if (field == LogField.Calories ? appValue == value : Tenths(appValue) == Tenths(value))
                {
                    equal++;
                    continue;
                }
                rows.Add(new SyncRow(
                    day.Date, field, SyncRowKind.Conflict, appValue, value,
                    SetInAdvance: field == LogField.Calories && SetInAdvance(log!),
                    Imported: (field == LogField.Calories ? log!.CaloriesSource : log!.WeightSource) == source,
                    Suggested: IsOpen(log, field, source) ? SyncChoice.Replace : SyncChoice.Keep));
            }
        }
        var appValues = logs.Values.Sum(l => (l.CaloriesKcal is null ? 0 : 1) + (l.WeightKg is null ? 0 : 1));
        return (rows, equal, appValues - matched);
    }

    private static decimal Tenths(decimal kg) => Math.Round(kg, 1, MidpointRounding.AwayFromZero);
}
