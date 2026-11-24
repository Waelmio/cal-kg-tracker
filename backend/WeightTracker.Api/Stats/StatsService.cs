using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.Models;
using static WeightTracker.Api.Stats.StatsCalculator;

namespace WeightTracker.Api.Stats;

public interface IStatsService
{
    Task<DashboardStatsDto> DashboardAsync(DateOnly today);
    Task<CalendarDto> CalendarAsync(DateOnly from, DateOnly to, DateOnly today);
    Task<List<PeriodDto>> PeriodsAsync(string unit, int count, DateOnly today);
    Task<MonthSummaryDto> MonthAsync(DateOnly firstOfMonth, DateOnly today);
    Task<TdeeStatsDto> TdeeAsync(int days, DateOnly today);
    Task<GoalStatusDto?> GoalStatusAsync(DateOnly today);
    Task<List<WeightPointDto>> WeightsAsync(DateOnly from, DateOnly today);
    Task<List<BudgetByWindowDto>> BudgetByWindowAsync(DateOnly today);
}

public class StatsService(AppDbContext db) : IStatsService
{
    private sealed record Loaded(StatsCalculator Calc, UserSettings Settings, List<Goal> Goals);

    private async Task<Loaded> LoadAsync(DateOnly today)
    {
        var logs = await db.DailyLogs.AsNoTracking().ToListAsync();
        var targets = await db.CalorieGoals.AsNoTracking().OrderBy(g => g.EffectiveFrom).ThenBy(g => g.CreatedAt).ToListAsync();
        var tdees = await db.TdeeEntries.AsNoTracking().OrderBy(t => t.EffectiveFrom).ThenBy(t => t.Id).ToListAsync();
        var goals = await db.Goals.AsNoTracking().OrderByDescending(g => g.CreatedAt).ToListAsync();
        var settings = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == 1) ?? new UserSettings();

        var latest = goals.FirstOrDefault();
        var data = new StatsData
        {
            Today = today,
            Window = Math.Clamp(settings.AveragingWeeks, 1, 5),
            Days = logs.ToDictionary(l => l.Date, l => new DayData(
                l.Date, l.WeightKg is null ? null : (double)l.WeightKg, l.CaloriesKcal, l.CaloriesSetOn, l.IsCheatDay, !string.IsNullOrEmpty(l.Notes))),
            Targets = targets.Select(t => (t.EffectiveFrom, t.TargetCalories)).ToList(),
            Tdees = tdees.Select(t => (t.EffectiveFrom, t.Kcal)).ToList(),
            Goal = latest is null ? null : new GoalSpan(latest.StartDate, latest.EndedOn ?? latest.TargetDate),
        };
        return new Loaded(new StatsCalculator(data), settings, goals);
    }

    // ── Dashboard ────────────────────────────────────────────────────────────

    public async Task<DashboardStatsDto> DashboardAsync(DateOnly today)
    {
        var (c, settings, goals) = await LoadAsync(today);
        var monday = MondayOf(today);
        var sunday = monday.AddDays(6);
        var activeGoal = goals.FirstOrDefault(g => g.EndedOn is null);

        var week = ToDto(c, c.Week(monday), PreviousWeekWeight(c, monday), c.WeekStatusOf(monday));

        // Budget
        int? eaten = c.Calories(today);
        var budget = c.Budget(eaten);
        BudgetDto? budgetDto = null;
        int? expectedDeficit = null;
        if (budget is not null)
        {
            var loggedKcal = c.Logged(monday, today.AddDays(-1)).Sum(d => c.Calories(d)!.Value);
            var expectedTotal = loggedKcal + budget.Days.Values.Sum();
            var tdees = Range(monday, sunday).Select(c.Tdee).ToList();
            if (tdees.All(t => t is not null)) expectedDeficit = tdees.Sum(t => t!.Value) - expectedTotal;

            var wk = c.Week(monday);
            budgetDto = new BudgetDto(
                budget.TodayBudget, eaten, eaten is null ? null : budget.TodayBudget - eaten.Value,
                wk.AvgCalories is null ? null : Rnd(wk.AvgCalories.Value), wk.AvgTarget is null ? null : Rnd(wk.AvgTarget.Value),
                Rnd(expectedTotal / 7.0), expectedDeficit,
                budget.Days.Select(p => new PlannedDayDto(Fmt(p.Key), p.Value, budget.UserLogged.Contains(p.Key))).ToList());
        }

        // Streak
        var streak = c.Streak();
        var rows = new[] { 7, 14, 21 }.Select(n =>
        {
            var days = c.Logged(today.AddDays(-n), today.AddDays(-1));
            return days.Count == 0
                ? new StreakRowDto(n, null, null)
                : new StreakRowDto(n, Rnd(days.Average(d => (double)c.Calories(d)!.Value)), Rnd((double)c.Gap(days) / days.Count));
        }).ToList();

        // Deficits
        var wkP = c.Week(monday);
        var weekly = new WeeklyDeficitDto(wkP.Deficit, wkP.Deficit / KcalPerKg, expectedDeficit,
            expectedDeficit is null ? null : expectedDeficit / KcalPerKg, wkP.LoggedDays);
        OverallDeficitDto overall;
        if (activeGoal is not null && activeGoal.StartDate < today)
        {
            var p = c.Period(activeGoal.StartDate, today.AddDays(-1));
            overall = new OverallDeficitDto(p.Deficit, p.Deficit / KcalPerKg, p.Gap, p.LoggedDays, today.DayNumber - activeGoal.StartDate.DayNumber, Fmt(activeGoal.StartDate), true);
        }
        else
        {
            var from = monday.AddDays(-28);
            var p = c.Period(from, monday.AddDays(-1));
            overall = new OverallDeficitDto(p.Deficit, p.Deficit / KcalPerKg, p.Gap, p.LoggedDays, 28, Fmt(from), false);
        }

        // Weight
        var weight = c.Weight();
        var pace = c.Pace();
        double? bmi = weight is not null && settings.HeightCm is > 0
            ? Math.Round(weight.Kg / Math.Pow(settings.HeightCm.Value / 100.0, 2), 1) : null;
        var weightDto = new WeightDto(weight?.Kg, weight?.IsAverage ?? false, weight?.Date is { } wd ? Fmt(wd) : null,
            pace.KgPerDay is { } kd ? kd * 7 : null, pace.WeighIns, bmi,
            activeGoal is not null && settings.HeightCm is > 0
                ? Math.Round((double)activeGoal.TargetWeightKg / Math.Pow(settings.HeightCm.Value / 100.0, 2), 1) : null);

        return new DashboardStatsDto(
            Fmt(today), c.Window, c.CalorieTarget(today), c.Tdee(today), budgetDto, week,
            new StreakDto(streak.Days, streak.NextGap, rows),
            new DeficitsDto(weekly, overall), weightDto,
            GoalFor(goals, c),
            WeightSeries(c, activeGoal?.StartDate ?? today.AddDays(-90)),
            Comparison(c, GoalFor(goals, c)));
    }

    private static TargetComparisonDto Comparison(StatsCalculator c, GoalStatusDto? goal)
    {
        var today = c.Today;
        var tdee = c.Tdee(today);
        var target = c.CalorieTarget(today);
        static double Pace(int deficit) => deficit * 7 / KcalPerKg;

        int? tDef = tdee is { } t && target is { } k ? t - k : null;
        var monday = MondayOf(today);
        var last4 = c.Period(monday.AddDays(-28), monday.AddDays(-1));
        int? avg = last4.AvgCalories is { } a ? Rnd(a) : null;
        int? l4Def = tdee is { } t2 && avg is { } a2 ? t2 - a2 : null;

        // Intake that reaches the goal on time: the TDEE minus the deficit the needed weekly loss takes.
        int? onTime = tdee is { } t3 && goal is { State: "active", Kind: "Reach", NeededKgPerWeek: { } need }
            ? Rnd(t3 - need * KcalPerKg / 7) : null;

        return new TargetComparisonDto(target, tdee, tDef, tDef is { } d1 ? Pace(d1) : null,
            avg, l4Def, l4Def is { } d2 ? Pace(d2) : null, onTime);
    }

    /// <summary>Today's budget for each averaging window, so settings can preview what a window changes.</summary>
    public async Task<List<BudgetByWindowDto>> BudgetByWindowAsync(DateOnly today)
    {
        var (c, _, _) = await LoadAsync(today);
        return Enumerable.Range(1, 5).Select(w =>
        {
            var calc = new StatsCalculator(c.Data.WithWindow(w));
            return new BudgetByWindowDto(w, calc.Budget(calc.Calories(today))?.TodayBudget);
        }).ToList();
    }

    public async Task<List<WeightPointDto>> WeightsAsync(DateOnly from, DateOnly today)
    {
        var (c, _, _) = await LoadAsync(today);
        return WeightSeries(c, from);
    }

    private static List<WeightPointDto> WeightSeries(StatsCalculator c, DateOnly from) =>
        Range(from, c.Today)
            .Where(d => c.Data.Days.TryGetValue(d, out var x) && x.WeightKg is not null)
            .Select(d => new WeightPointDto(Fmt(d), c.Data.Days[d].WeightKg!.Value, c.Average7(d)!.Value))
            .ToList();

    public async Task<GoalStatusDto?> GoalStatusAsync(DateOnly today)
    {
        var (c, _, goals) = await LoadAsync(today);
        return GoalFor(goals, c);
    }

    private static GoalStatusDto? GoalFor(List<Goal> goals, StatsCalculator c)
    {
        var active = goals.FirstOrDefault(g => g.EndedOn is null);
        return active is null ? null : GoalStatusCalculator.Compute(active, c);
    }

    // ── Calendar ─────────────────────────────────────────────────────────────

    public async Task<CalendarDto> CalendarAsync(DateOnly from, DateOnly to, DateOnly today)
    {
        var (c, _, _) = await LoadAsync(today);
        var budget = c.Budget(c.Calories(today));

        var days = Range(from, to).Select(d =>
        {
            c.Data.Days.TryGetValue(d, out var day);
            int? planned = null;
            var plannedLogged = false;
            if (budget is not null && budget.Days.TryGetValue(d, out var b) && d >= today)
            {
                planned = b;
                plannedLogged = budget.UserLogged.Contains(d);
            }
            return new CalendarDayDto(
                Fmt(d), day?.Calories, c.Target(d), Status(c.DayStatus(d)), planned, plannedLogged, c.InGoal(d),
                day?.WeightKg, day?.IsCheatDay ?? false, day?.HasNote ?? false);
        }).ToList();

        var weeks = new List<CalendarWeekDto>();
        for (var m = MondayOf(from); m <= to; m = m.AddDays(7)) weeks.Add(WeekDto(c, m));

        return new CalendarDto(Fmt(today), days, weeks);
    }

    // ── Average calories chart ───────────────────────────────────────────────

    public async Task<List<PeriodDto>> PeriodsAsync(string unit, int count, DateOnly today)
    {
        var (c, _, _) = await LoadAsync(today);
        count = Math.Clamp(count, 1, 60);
        var result = new List<PeriodDto>();

        if (unit == "month")
        {
            var current = new DateOnly(today.Year, today.Month, 1);
            for (var i = count - 1; i >= 0; i--)
                result.Add(MonthDto(c, current.AddMonths(-i)));
        }
        else
        {
            var monday = MondayOf(today);
            for (var i = count - 1; i >= 0; i--)
                result.Add(WeekDto(c, monday.AddDays(-7 * i)).Figures);
        }
        return result;
    }

    // ── Month summary ────────────────────────────────────────────────────────

    public async Task<MonthSummaryDto> MonthAsync(DateOnly firstOfMonth, DateOnly today)
    {
        var (c, _, _) = await LoadAsync(today);
        var last = firstOfMonth.AddMonths(1).AddDays(-1);
        var figures = MonthDto(c, firstOfMonth);

        var weeks = new List<CalendarWeekDto>();
        for (var m = MondayOf(firstOfMonth); m <= last; m = m.AddDays(7)) weeks.Add(WeekDto(c, m));

        var prev = c.Period(firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1));
        var inMonth = Range(firstOfMonth, last).Where(d => c.Data.Days.ContainsKey(d)).Select(d => c.Data.Days[d]).ToList();
        var weighed = inMonth.Where(d => d.WeightKg is not null).OrderBy(d => d.Date).ToList();

        // 90 days ending that month (or today, for the running month)
        var end = last < today ? last : today;
        var est = c.TdeeEstimate(90, end).Estimate;

        return new MonthSummaryDto(
            firstOfMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture), figures, last.Day,
            inMonth.Count(d => d.IsCheatDay && d.Calories is not null && d.Date < today),
            prev.AvgWeightKg, weighed.FirstOrDefault()?.WeightKg, weighed.LastOrDefault()?.WeightKg,
            weeks, est is null ? null : ToDto(est));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Fmt(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Status(WeekStatus? s) => s?.ToString().ToLowerInvariant();

    private static double? PreviousWeekWeight(StatsCalculator c, DateOnly monday) => c.Week(monday.AddDays(-7)).AvgWeightKg;

    private static PeriodDto ToDto(StatsCalculator c, PeriodStats p, double? previousWeight, WeekStatus? status, bool? inGoal = null) =>
        new(Fmt(p.From), Fmt(p.To), p.LoggedDays,
            p.AvgCalories is null ? null : Rnd(p.AvgCalories.Value),
            p.AvgTarget is null ? null : Rnd(p.AvgTarget.Value),
            p.Gap, p.Deficit, p.Deficit / KcalPerKg, p.AvgWeightKg,
            p.AvgWeightKg is { } a && previousWeight is { } b ? a - b : null,
            p.WeighIns, Status(status), inGoal ?? c.InGoal(p.From, p.To));

    private static CalendarWeekDto WeekDto(StatsCalculator c, DateOnly monday)
    {
        var figures = ToDto(c, c.Week(monday), PreviousWeekWeight(c, monday), c.WeekStatusOf(monday));
        return new CalendarWeekDto(Fmt(monday), System.Globalization.ISOWeek.GetWeekOfYear(monday.ToDateTime(TimeOnly.MinValue)),
            System.Globalization.ISOWeek.GetYear(monday.ToDateTime(TimeOnly.MinValue)), figures);
    }

    private static PeriodDto MonthDto(StatsCalculator c, DateOnly first)
    {
        var last = first.AddMonths(1).AddDays(-1);
        var prev = c.Period(first.AddMonths(-1), first.AddDays(-1));
        return ToDto(c, c.Period(first, last), prev.AvgWeightKg, c.MonthStatus(first));
    }

    internal static TdeeEstimateDto ToDto(TdeeEstimateResult e) =>
        new(Rnd(e.Tdee), Rnd(e.AvgCalories), Math.Round(e.WeightTrendKgPerDay * 1000, 1), Rnd(e.Adjustment),
            e.WeighIns, e.LoggedDays, e.SpanDays, Fmt(e.First), Fmt(e.Last), e.PeriodDays);

    // ── TDEE (phase 3) ───────────────────────────────────────────────────────

    public async Task<TdeeStatsDto> TdeeAsync(int days, DateOnly today)
    {
        var (c, _, goals) = await LoadAsync(today);
        var attempt = c.TdeeEstimate(days);
        var current = c.Tdee(today);

        TdeeEstimateDto? dto = attempt.Estimate is null ? null : ToDto(attempt.Estimate);
        var suggest = days == 90 && attempt.Estimate is { } e90 && (current is null || Math.Abs(Rnd(e90.Tdee) - current.Value) > 100);
        var note = days == 30 && dto is not null ? "Quick estimate. Can be off by 300 kcal or more." : null;

        DietEffectDto? diet = null;
        var goal = goals.FirstOrDefault(g => g.EndedOn is null && g.Kind == GoalKind.Reach);
        if (attempt.Estimate is { } est && goal?.StartingWeightKg is { } startKg && (double)goal.TargetWeightKg < (double)startKg
            && c.DietEffect(est, (double)startKg) is { } d)
        {
            diet = new DietEffectDto(d.LighterKg, [
                new("before", "Burn before the diet", Rnd(d.BurnBefore)),
                new("lighter", $"{Math.Round(d.LighterKg, 1).ToString("0.0", CultureInfo.InvariantCulture)} kg lighter", -Rnd(d.Lighter)),
                new("digest", "Less food to digest", -Rnd(d.Digest)),
                new("slowed", "Metabolism slowed", -Rnd(d.Slowed)),
                new("now", "What you burn now (TDEE)", Rnd(d.BurnNow)),
                new("keep", "Calories to keep your current weight", Rnd(d.KeepWeight)),
            ], "Typical values from Hall et al. 2011, not measured on you.");
        }

        return new TdeeStatsDto(days, dto, attempt.Missing, attempt.WeighIns, attempt.SpanDays, attempt.LoggedDays,
            current, suggest, note, diet);
    }
}
