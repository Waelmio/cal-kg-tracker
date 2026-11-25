namespace WeightTracker.Api.Stats;

public enum WeekStatus { Under, Balanced, Over }

public sealed record PeriodStats(
    DateOnly From, DateOnly To,
    int LoggedDays, double? AvgCalories, double? AvgTarget, int Gap, int Deficit,
    double? AvgWeightKg, int WeighIns);

public sealed record RegressionLine(double Slope, double Intercept);

public sealed record TdeeEstimateResult(
    double Tdee, double AvgCalories, double WeightTrendKgPerDay, double Adjustment,
    int WeighIns, int LoggedDays, int SpanDays, DateOnly First, DateOnly Last, int PeriodDays);

public sealed record DietEffectResult(
    double BurnBefore, double LighterKg, double Lighter, double Digest, double Slowed, double BurnNow, double KeepWeight);

/// <summary>
/// Every statistic of the app, as pure functions of <see cref="StatsData"/>.
/// Rules: redesign PLAN.md section 3. Rounding is for display only; callers round with <see cref="Rnd"/>.
/// </summary>
public sealed class StatsCalculator(StatsData data)
{
    // 7700 kcal per kg is the energy stored in the weight lost. Studies quote 8840-9100 (Hall & Chow 2011,
    // Sanghvi 2015), but that is this value divided by 0.76: it answers "how much must intake change",
    // including the 24% drop in burn when eating less. Our estimate is built from what was eaten and lost,
    // so that drop is already in the data. Using 9100 would return the burn from before the diet.
    //
    // 17 kcal per kg: burn falls as weight falls, so a period average is too high while losing. Hall et al. 2011
    // (Lancet, web appendix eq. 13) publish 22 for the same reason divided by 0.76; 22 * 0.76 = 17.
    public const double KcalPerKg = 7700;
    public const double BurnPerKg = 17;
    public const double DigestShare = 0.10;
    public const double AdaptShare = 0.14;

    public StatsData Data => data;
    public DateOnly Today => data.Today;
    public int Window => data.Window;

    public static int Rnd(double x) => (int)Math.Round(x, MidpointRounding.AwayFromZero);

    // ── Dates ────────────────────────────────────────────────────────────────

    /// <summary>Monday of the ISO week containing the date.</summary>
    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public static DateOnly SundayOf(DateOnly d) => MondayOf(d).AddDays(6);

    public static IEnumerable<DateOnly> Range(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) yield return d;
    }

    // ── Targets and TDEE in force ────────────────────────────────────────────

    public int? Tdee(DateOnly d)
    {
        int? result = null;
        foreach (var (from, kcal) in data.Tdees)
        {
            if (from > d) break;
            result = kcal;
        }
        return result;
    }

    public int? CalorieTarget(DateOnly d)
    {
        int? result = null;
        foreach (var (from, kcal) in data.Targets)
        {
            if (from > d) break;
            result = kcal;
        }
        return result;
    }

    /// <summary>Cheat day: the TDEE in force that day (the calorie target if there is none). Otherwise the calorie target.</summary>
    public int? Target(DateOnly d)
    {
        var target = CalorieTarget(d);
        if (target is null) return null;   // target turned off or never set
        if (data.Days.TryGetValue(d, out var day) && day.IsCheatDay) return Tdee(d) ?? target;
        return target;
    }

    // ── Logged days ──────────────────────────────────────────────────────────

    public int? Calories(DateOnly d) => data.Days.TryGetValue(d, out var x) ? x.Calories : null;

    /// <summary>
    /// Logged day: up to today, calories not null (0 counts). Today counts as soon as it is logged: a day is
    /// normally logged once, when it is over. A future day that is logged counts when it comes.
    /// </summary>
    public bool IsLogged(DateOnly d) => d <= data.Today && Calories(d) is not null;

    /// <summary>The last day the figures cover: today once it is logged, yesterday until then.</summary>
    public DateOnly LastDay => IsLogged(data.Today) ? data.Today : data.Today.AddDays(-1);

    public List<DateOnly> Logged(DateOnly from, DateOnly to) => Range(from, to).Where(IsLogged).ToList();

    public int Gap(IEnumerable<DateOnly> days)
    {
        var gap = 0;
        foreach (var d in days)
            if (Target(d) is { } t) gap += Calories(d)!.Value - t;
        return gap;
    }

    // ── Week and month figures (3.2) ─────────────────────────────────────────

    public PeriodStats Period(DateOnly from, DateOnly to)
    {
        var logged = Logged(from, to);
        var withTarget = logged.Where(d => Target(d) is not null).ToList();
        var deficit = logged.Where(d => Tdee(d) is not null).Sum(d => Tdee(d)!.Value - Calories(d)!.Value);
        var weights = Range(from, to).Where(d => data.Days.TryGetValue(d, out var x) && x.WeightKg is not null)
            .Select(d => data.Days[d].WeightKg!.Value).ToList();

        return new PeriodStats(
            from, to, logged.Count,
            logged.Count > 0 ? logged.Average(d => (double)Calories(d)!.Value) : null,
            withTarget.Count > 0 ? withTarget.Average(d => (double)Target(d)!.Value) : null,
            Gap(logged), deficit,
            weights.Count > 0 ? weights.Average() : null, weights.Count);
    }

    private readonly Dictionary<DateOnly, PeriodStats> _weeks = [];

    /// <summary>Cached: a week's status looks at its neighbours, and every over-target day asks for its week's status.</summary>
    public PeriodStats Week(DateOnly monday)
    {
        if (!_weeks.TryGetValue(monday, out var week))
            _weeks[monday] = week = Period(monday, monday.AddDays(6));
        return week;
    }

    // ── Status colors (3.3) ──────────────────────────────────────────────────

    private bool HasFigures(PeriodStats w) => w.LoggedDays > 0 && w.AvgTarget is not null;

    public WeekStatus? WeekStatusOf(DateOnly monday)
    {
        var w = Week(monday);
        if (!HasFigures(w)) return null;
        if (w.Gap <= 0) return WeekStatus.Under;

        for (var n = 2; n <= data.Window; n++)
            for (var k = 0; k < n; k++)
            {
                var weeks = Enumerable.Range(0, n).Select(j => Week(monday.AddDays(7 * (j - k)))).ToList();
                if (weeks.All(x => x.LoggedDays > 0) && weeks.Sum(x => x.Gap) <= 0) return WeekStatus.Balanced;
            }
        return WeekStatus.Over;
    }

    public WeekStatus? DayStatus(DateOnly d)
    {
        if (!IsLogged(d) || Target(d) is not { } target) return null;
        if (Calories(d)!.Value <= target) return WeekStatus.Under;
        return WeekStatusOf(MondayOf(d)) == WeekStatus.Over ? WeekStatus.Over : WeekStatus.Balanced;
    }

    /// <summary>Months are only green or red.</summary>
    public WeekStatus? MonthStatus(DateOnly firstOfMonth)
    {
        var m = Period(firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1));
        if (!HasFigures(m)) return null;
        return m.Gap <= 0 ? WeekStatus.Under : WeekStatus.Over;
    }

    /// <summary>Inside the current goal (start to target date, or end date if it ended). No goal: always inside.</summary>
    public bool InGoal(DateOnly d) =>
        data.Goal is not { } g || (d >= g.Start && (g.End is null || d <= g.End));

    /// <summary>A week or month is inside the goal if more than half its days are.</summary>
    public bool InGoal(DateOnly from, DateOnly to)
    {
        if (data.Goal is null) return true;
        var days = Range(from, to).ToList();
        return days.Count(InGoal) * 2 > days.Count;
    }

    // ── Budget and planned days (3.4) ────────────────────────────────────────

    public sealed record BudgetResult(
        IReadOnlyDictionary<DateOnly, int> Days,        // today..Sunday, rounded, planned days included
        IReadOnlySet<DateOnly> UserLogged,              // future days the user logged (kept as typed)
        int TodayBudget,                                // today's share, whether or not it is logged
        int PerDay,                                     // the budget of the days to come, today included until it is logged
        double TodayExact,                              // the same, before rounding
        double Pool,                                    // what all the free days (today included) share, before clamping
        bool ReachesTarget,                             // false when even this plan leaves the window over target
        IReadOnlyDictionary<DateOnly, int> FutureDays); // the next Window-1 weeks (see FutureBudget)

    /// <summary>Until today is logged it counts as its budget. Once it is, the other days share what it left.</summary>
    public BudgetResult? Budget()
    {
        var today = data.Today;
        if (Target(today) is null) return null;

        var monday = MondayOf(today);
        var sunday = monday.AddDays(6);
        var past = Logged(monday.AddDays(-7 * (Window - 1)), today.AddDays(-1)).Where(d => Target(d) is not null).ToList();
        var rest = Range(today, sunday).ToList();

        // Future days the user logged are real logs, they shape the budget at once.
        var planned = rest.Where(d => d > today && Calories(d) is not null).ToDictionary(d => d, d => (double)Calories(d)!.Value);

        double T(DateOnly d) => Target(d) ?? 0;
        var targets = past.Concat(rest).Sum(T);
        var eatenPast = past.Sum(d => (double)Calories(d)!.Value);

        Dictionary<DateOnly, double> Solve(IReadOnlyCollection<DateOnly> free, double fixedKcal)
        {
            if (free.Count == 0) return [];
            var extra = (targets - eatenPast - fixedKcal - free.Sum(T)) / free.Count;
            return free.ToDictionary(d => d, d => Math.Max(0, T(d) + extra));
        }

        var free = rest.Where(d => !planned.ContainsKey(d)).ToList();
        var result = Solve(free, planned.Values.Sum());
        var todayBudget = result[today];
        var todayEaten = todayBudget;

        if (Calories(today) is { } eaten)
        {
            todayEaten = eaten;
            result = Solve(free.Where(d => d != today).ToList(), planned.Values.Sum() + eaten);
            result[today] = todayBudget;
        }

        // The plan as it would be eaten, which the following weeks build on.
        var simulated = rest.ToDictionary(d => d, d => planned.TryGetValue(d, out var typed) ? typed : d == today ? todayEaten : result[d]);
        var reaches = eatenPast + simulated.Values.Sum() - targets < 0.5;

        var days = new SortedDictionary<DateOnly, int>();
        foreach (var d in rest) days[d] = Rnd(planned.TryGetValue(d, out var typed) ? typed : result[d]);

        var next = IsLogged(today) ? free.FirstOrDefault(d => d > today, today) : today;
        return new BudgetResult(days, planned.Keys.ToHashSet(), Rnd(todayBudget), days[next], todayBudget,
            targets - eatenPast - planned.Values.Sum(), reaches, FutureBudget(monday, simulated));
    }

    /// <summary>
    /// Plans the weeks after this one, as far as it takes for the average to be reached again. Week k looks at the
    /// Window weeks ending with it, counts what was logged and what is planned before it as eaten, and shares what is
    /// left over its free days. So a debt this week can't absorb (budget at 0), or a day typed in advance, lowers the
    /// next weeks until the debt leaves the window. A debt only ever touches the next Window-1 weeks.
    /// <para>
    /// The plan never goes above the target: without that cap, the week that pays back an over-target week would
    /// itself be "under" and hand the same calories back one week later, forever (+G, -G, +G...).
    /// </para>
    /// </summary>
    private IReadOnlyDictionary<DateOnly, int> FutureBudget(DateOnly thisMonday, Dictionary<DateOnly, double> simulated)
    {
        var future = new SortedDictionary<DateOnly, int>();
        for (var k = 1; k < Window; k++)
        {
            var monday = thisMonday.AddDays(7 * k);
            var week = Range(monday, monday.AddDays(6)).ToList();

            // Balance of the window before this week: logged days before today, then the plan.
            var debt = Range(monday.AddDays(-7 * (Window - 1)), monday.AddDays(-1)).Sum(d => Target(d) is not { } t ? 0
                : d < data.Today ? (Calories(d) is { } eaten ? eaten - t : 0)
                : simulated.TryGetValue(d, out var kcal) ? kcal - t : 0);

            // Days of this week the user already logged ahead of time stay as typed.
            debt += week.Sum(d => Calories(d) is { } typed ? typed - (Target(d) ?? 0) : 0);
            var free = week.Where(d => Calories(d) is null).ToList();
            var cut = free.Count == 0 ? 0 : Math.Max(0, debt) / free.Count;

            foreach (var d in week)
            {
                simulated[d] = Calories(d) is { } typed ? typed : Math.Max(0, (Target(d) ?? 0) - cut);
                future[d] = Rnd(simulated[d]);
            }
        }
        return future;
    }

    // ── Streak (3.5) ─────────────────────────────────────────────────────────

    public sealed record StreakResult(int Days, int? NextGap);

    /// <summary>Longest k such that the last k logged days have a total gap at or below zero.</summary>
    public StreakResult Streak()
    {
        var days = data.Days.Keys.Where(d => IsLogged(d) && Target(d) is not null).OrderByDescending(d => d).ToList();
        int best = 0, cum = 0;
        var running = new List<int>();
        for (var k = 1; k <= days.Count; k++)
        {
            cum += Calories(days[k - 1])!.Value - Target(days[k - 1])!.Value;
            running.Add(cum);
            if (cum <= 0) best = k;
        }
        return new StreakResult(best, best < running.Count ? running[best] : null);
    }

    // ── Weight (3.7) ─────────────────────────────────────────────────────────

    private List<(DateOnly Date, double Kg)> WeighIns(DateOnly from, DateOnly to) =>
        Range(from, to)
            .Where(d => data.Days.TryGetValue(d, out var x) && x.WeightKg is not null)
            .Select(d => (d, data.Days[d].WeightKg!.Value)).ToList();

    /// <summary>Mean of the weigh-ins of the 7 days ending at the date. Null if there are none.</summary>
    public double? Average7(DateOnly d)
    {
        var w = WeighIns(d.AddDays(-6), d);
        return w.Count > 0 ? w.Average(x => x.Kg) : null;
    }

    public sealed record DisplayedWeight(double Kg, bool IsAverage, DateOnly? Date);

    /// <summary>Average of the last 7 days; if none, the last weigh-in with its date.</summary>
    public DisplayedWeight? Weight()
    {
        if (Average7(Today) is { } avg) return new DisplayedWeight(avg, true, null);
        var last = data.Days.Values.Where(x => x.WeightKg is not null && x.Date <= Today).OrderByDescending(x => x.Date).FirstOrDefault();
        return last is null ? null : new DisplayedWeight(last.WeightKg!.Value, false, last.Date);
    }

    public static RegressionLine? LeastSquares(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count < 2) return null;
        var mx = points.Average(p => p.X);
        var my = points.Average(p => p.Y);
        var den = points.Sum(p => (p.X - mx) * (p.X - mx));
        if (den == 0) return null;
        var slope = points.Sum(p => (p.X - mx) * (p.Y - my)) / den;
        return new RegressionLine(slope, my - slope * mx);
    }

    /// <summary>The 7-day average at a date, or at the last weigh-in before it when that week has none.</summary>
    public double? LatestAverage7(DateOnly d)
    {
        if (Average7(d) is { } avg) return avg;
        var last = data.Days.Values.Where(x => x.WeightKg is not null && x.Date <= d).Select(x => (DateOnly?)x.Date).Max();
        return last is { } date ? Average7(date) : null;
    }

    private static List<(double X, double Y)> Points(IEnumerable<(DateOnly Date, double Kg)> w) =>
        w.Select(p => ((double)p.Date.DayNumber, p.Kg)).ToList();

    public sealed record PaceResult(double? KgPerDay, int WeighIns);

    /// <summary>Least-squares slope of the last 28 days. Needs 3 weigh-ins spanning 7 days.</summary>
    public PaceResult Pace()
    {
        var w = WeighIns(Today.AddDays(-27), Today);
        if (w.Count < 3 || w[^1].Date.DayNumber - w[0].Date.DayNumber < 7) return new PaceResult(null, w.Count);
        return new PaceResult(LeastSquares(Points(w))?.Slope, w.Count);
    }

    // ── TDEE estimate (3.9) and diet effect (3.10) ───────────────────────────

    /// <summary>The estimate used for suggestions, the month summary and maintaining.</summary>
    public const int DefaultTdeeDays = 90;
    public const int MinWeighIns = 6, MinSpanDays = 21;
    public const double MinLoggedShare = 0.7;

    public sealed record TdeeAttempt(TdeeEstimateResult? Estimate, string? Missing, int WeighIns, int SpanDays, int LoggedDays);

    public TdeeAttempt TdeeEstimate(int days, DateOnly? endDate = null)
    {
        var end = endDate ?? Today;
        var w = WeighIns(end.AddDays(-days), end);
        var span = w.Count > 0 ? w[^1].Date.DayNumber - w[0].Date.DayNumber : 0;

        if (w.Count < MinWeighIns || span < MinSpanDays)
            return new TdeeAttempt(null,
                $"Needs at least {MinWeighIns} weigh-ins spanning {MinSpanDays} days. You have {w.Count} spanning {span}.",
                w.Count, span, 0);

        var first = w[0].Date;
        var last = w[^1].Date;
        var logged = Logged(first, last.AddDays(-1));
        if (logged.Count < MinLoggedShare * span)
            return new TdeeAttempt(null,
                $"Needs calories logged on at least 70% of the {span} days between your first and last weigh-in. You have {logged.Count}.",
                w.Count, span, logged.Count);

        var line = LeastSquares(Points(w))!;
        var avg = logged.Average(d => (double)Calories(d)!.Value);
        var fitEnd = line.Slope * end.DayNumber + line.Intercept;
        var fitMid = line.Slope * (first.DayNumber + last.DayNumber) / 2.0 + line.Intercept;
        var adjust = BurnPerKg * (fitEnd - fitMid);
        var tdee = avg - line.Slope * KcalPerKg + adjust;

        return new TdeeAttempt(
            new TdeeEstimateResult(tdee, avg, line.Slope, adjust, w.Count, logged.Count, span, first, last, days),
            null, w.Count, span, logged.Count);
    }

    /// <summary>What the diet changed. Null when D is below 100 (nothing meaningful to show).</summary>
    public DietEffectResult? DietEffect(TdeeEstimateResult estimate, double startingWeightKg)
    {
        if (Weight()?.Kg is not { } now) return null;
        var lost = startingWeightKg - now;
        var d = estimate.Tdee - estimate.AvgCalories;
        if (d < 100) return null;

        var cut = (d + BurnPerKg * lost) / (1 - DigestShare - AdaptShare);
        return new DietEffectResult(
            estimate.AvgCalories + cut, lost, BurnPerKg * lost, DigestShare * cut, AdaptShare * cut,
            estimate.Tdee, estimate.AvgCalories + d / (1 - DigestShare - AdaptShare));
    }
}
