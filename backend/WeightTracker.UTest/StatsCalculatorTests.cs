using WeightTracker.Api.Stats;
using static WeightTracker.Api.Stats.StatsCalculator;

namespace WeightTracker.UTest;

/// <summary>Section 3.11 of the redesign plan, computed from the fixture.</summary>
public class StatsCalculatorTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);
    private static readonly DateOnly Today = FixtureLoader.Today;

    private static int[] BudgetValues(StatsCalculator c, int? eaten = null) => c.Budget(eaten)!.Days.Values.ToArray();

    [Theory]
    [InlineData(1, 1877)]
    [InlineData(2, 1881)]
    [InlineData(3, 1786)]
    [InlineData(4, 1605)]
    [InlineData(5, 1483)]
    public void TodaysBudget_ByWindow(int window, int expected)
    {
        Assert.Equal(expected, FixtureLoader.Calc(window).Budget(null)!.TodayBudget);
    }

    [Fact]
    public void Window2_TodayEaten2400_OtherDaysGetLess()
    {
        var b = FixtureLoader.Calc(2).Budget(2400)!;
        Assert.Equal(1881, b.TodayBudget);
        Assert.All(b.Days.Where(p => p.Key > Today), p => Assert.Equal(1777, p.Value));
    }

    [Fact]
    public void Window2_SaturdayPlanned2500_OtherDaysGetLess()
    {
        var saturday = D("2026-11-28");
        var calc = FixtureLoader.Calc(2, days => days[saturday] = new DayData(saturday, null, 2500, Today, false, false));
        var b = calc.Budget(null)!;
        Assert.Equal(2500, b.Days[saturday]);
        Assert.Contains(saturday, b.UserLogged);
        Assert.All(b.Days.Where(p => p.Key != saturday), p => Assert.Equal(1757, p.Value));
    }

    [Fact]
    public void ExpectedWeekAverageAndDeficit()
    {
        var c = FixtureLoader.Calc(2);
        var monday = MondayOf(Today);
        var logged = c.Logged(monday, Today.AddDays(-1)).Sum(d => c.Calories(d)!.Value);
        var total = logged + BudgetValues(c).Sum();
        Assert.Equal(1854, Rnd(total / 7.0));
        Assert.Equal(3475, Enumerable.Range(0, 7).Sum(i => c.Tdee(monday.AddDays(i))!.Value) - total);
    }

    [Fact]
    public void WeekStatus_W40_DependsOnWindow()
    {
        var w40 = D("2026-09-28");
        Assert.Equal(WeekStatus.Over, FixtureLoader.Calc(2).WeekStatusOf(w40));
        Assert.Equal(WeekStatus.Balanced, FixtureLoader.Calc(3).WeekStatusOf(w40));
    }

    [Fact]
    public void W47_Figures()
    {
        var c = FixtureLoader.Calc(2);
        var monday = D("2026-11-16");
        var w = c.Week(monday);
        Assert.Equal(1847, Rnd(w.AvgCalories!.Value));
        Assert.Equal(1850, Rnd(w.AvgTarget!.Value));
        Assert.Equal(-22, w.Gap);
        Assert.Equal(-0.98, Math.Round(w.AvgWeightKg!.Value - c.Week(monday.AddDays(-7)).AvgWeightKg!.Value, 2));
        Assert.Equal(WeekStatus.Under, c.WeekStatusOf(monday));
    }

    [Fact]
    public void DayStatus_20Nov_14Nov()
    {
        var c = FixtureLoader.Calc(2);
        Assert.Equal(WeekStatus.Balanced, c.DayStatus(D("2026-11-20")));
        Assert.Equal(WeekStatus.Over, c.DayStatus(D("2026-11-14")));
    }

    [Fact]
    public void Streak_NineDays_Next26()
    {
        var s = FixtureLoader.Calc(2).Streak();
        Assert.Equal(9, s.Days);
        Assert.Equal(26, s.NextGap);
    }

    [Theory]
    [InlineData(7, 1853, 3)]
    [InlineData(14, 1924, 39)]
    [InlineData(21, 1953, 79)]
    public void LastNDays(int n, int avg, int avgGap)
    {
        var c = FixtureLoader.Calc(2);
        var days = c.Logged(Today.AddDays(-n), Today.AddDays(-1));
        Assert.Equal(avg, Rnd(days.Average(d => (double)c.Calories(d)!.Value)));
        Assert.Equal(avgGap, Rnd((double)c.Gap(days) / days.Count));
    }

    [Fact]
    public void OverallDeficit()
    {
        var c = FixtureLoader.Calc(2);
        var p = c.Period(D("2026-09-10"), Today.AddDays(-1));
        Assert.Equal(26839, p.Deficit);
        Assert.Equal(3.49, Math.Round(p.Deficit / KcalPerKg, 2));
        Assert.Equal(2361, p.Gap);
        Assert.Equal(67, p.LoggedDays);
        Assert.Equal(75, Today.DayNumber - D("2026-09-10").DayNumber);
    }

    [Fact]
    public void WeightAndPace()
    {
        var c = FixtureLoader.Calc(2);
        Assert.Equal(84.07, Math.Round(c.Weight()!.Kg, 2));
        var pace = c.Pace();
        Assert.Equal(22, pace.WeighIns);
        Assert.Equal(-0.41, Math.Round(pace.KgPerDay!.Value * 7, 2));
    }

    [Theory]
    [InlineData(30, 2357)]
    [InlineData(60, 2351)]
    [InlineData(90, 2352)]
    public void Tdee_ByPeriod(int days, int expected)
    {
        var e = FixtureLoader.Calc(2).TdeeEstimate(days).Estimate!;
        Assert.Equal(expected, Rnd(e.Tdee));
    }

    [Fact]
    public void Tdee90_At31Oct() =>
        Assert.Equal(2306, Rnd(FixtureLoader.Calc(2).TdeeEstimate(90, D("2026-10-31")).Estimate!.Tdee));

    [Fact]
    public void DietEffect_90Days()
    {
        var c = FixtureLoader.Calc(2);
        var e = c.TdeeEstimate(90).Estimate!;
        var d = c.DietEffect(e, 88.6)!;
        Assert.Equal(2576, Rnd(d.BurnBefore));
        Assert.Equal(77, Rnd(d.Lighter));
        Assert.Equal(61, Rnd(d.Digest));
        Assert.Equal(86, Rnd(d.Slowed));
        Assert.Equal(2474, Rnd(d.KeepWeight));
    }

    [Fact]
    public void October_Month()
    {
        var c = FixtureLoader.Calc(2);
        var m = c.Period(D("2026-10-01"), D("2026-10-31"));
        Assert.Equal(1978, Rnd(m.AvgCalories!.Value));
        Assert.Equal(1912, Rnd(m.AvgTarget!.Value));
        Assert.Equal(9680, m.Deficit);
        Assert.Equal(1720, m.Gap);
        Assert.Equal(26, m.LoggedDays);
    }

    // ── Edge cases ───────────────────────────────────────────────────────────

    private static StatsCalculator Empty(Action<Dictionary<DateOnly, DayData>>? edit = null, int? target = 2000, int? tdee = 2400)
    {
        var days = new Dictionary<DateOnly, DayData>();
        edit?.Invoke(days);
        return new StatsCalculator(new StatsData
        {
            Today = Today, Days = days,
            Targets = target is null ? [] : [(D("2026-01-01"), target)],
            Tdees = tdee is null ? [] : [(D("2026-01-01"), tdee.Value)],
        });
    }

    private static DayData Cal(string date, int? kcal, double? kg = null, bool cheat = false) =>
        new(D(date), kg, kcal, null, cheat, false);

    [Fact]
    public void EmptyDatabase_NothingThrows()
    {
        var c = Empty(target: null, tdee: null);
        Assert.Null(c.Budget(null));
        Assert.Null(c.Weight());
        Assert.Equal(0, c.Streak().Days);
        Assert.Null(c.Pace().KgPerDay);
        Assert.Null(c.TdeeEstimate(90).Estimate);
        Assert.Null(c.WeekStatusOf(MondayOf(Today)));
    }

    [Fact]
    public void NoTarget_NoBudgetNoStatus()
    {
        var c = Empty(d => d[D("2026-11-20")] = Cal("2026-11-20", 2500), target: null);
        Assert.Null(c.Budget(null));
        Assert.Null(c.DayStatus(D("2026-11-20")));
        Assert.Null(c.WeekStatusOf(D("2026-11-16")));
    }

    [Fact]
    public void NoTdee_NoDeficit()
    {
        var c = Empty(d => d[D("2026-11-20")] = Cal("2026-11-20", 1800), tdee: null);
        Assert.Equal(0, c.Period(D("2026-11-16"), D("2026-11-22")).Deficit);
    }

    [Fact]
    public void ZeroKcal_IsALoggedFastingDay()
    {
        var c = Empty(d => d[D("2026-11-20")] = Cal("2026-11-20", 0));
        Assert.True(c.IsLogged(D("2026-11-20")));
        Assert.Equal(-2000, c.Gap([D("2026-11-20")]));
        Assert.Equal(WeekStatus.Under, c.DayStatus(D("2026-11-20")));
    }

    [Fact]
    public void Today_IsNeverLogged()
    {
        var c = Empty(d => d[Today] = Cal("2026-11-24", 3000));
        Assert.False(c.IsLogged(Today));
        Assert.Equal(0, c.Period(MondayOf(Today), Today).LoggedDays);
    }

    [Fact]
    public void FutureLog_ShapesBudgetNow_AndCountsOnceItsDayHasPassed()
    {
        var saturday = D("2026-11-28");
        var c = Empty(d => d[saturday] = Cal("2026-11-28", 3000));
        Assert.False(c.IsLogged(saturday));
        Assert.Equal(3000, c.Budget(null)!.Days[saturday]);

        // a week later the same row is a real logged day
        var later = new StatsCalculator(new StatsData
        {
            Today = D("2026-12-01"), Days = c.Data.Days, Targets = c.Data.Targets, Tdees = c.Data.Tdees,
        });
        Assert.True(later.IsLogged(saturday));
        Assert.Equal(1000, later.Gap([saturday]));
    }

    [Fact]
    public void CheatDay_UsesTdee_OrTargetWithoutTdee()
    {
        var withTdee = Empty(d => d[D("2026-11-20")] = Cal("2026-11-20", 2600, cheat: true));
        Assert.Equal(2400, withTdee.Target(D("2026-11-20")));
        var without = Empty(d => d[D("2026-11-20")] = Cal("2026-11-20", 2600, cheat: true), tdee: null);
        Assert.Equal(2000, without.Target(D("2026-11-20")));
    }

    [Fact]
    public void ThreeConsecutiveWeighIns_GiveNoEstimateAndNoPace()
    {
        var c = Empty(d =>
        {
            foreach (var i in new[] { 20, 21, 22 }) d[D($"2026-11-{i}")] = Cal($"2026-11-{i}", 2000, 80 - i * 0.01);
        });
        Assert.Null(c.Pace().KgPerDay);
        Assert.NotNull(c.TdeeEstimate(30).Missing);
    }

    [Fact]
    public void TargetTurnedOff_FromADate()
    {
        var c = new StatsCalculator(new StatsData
        {
            Today = Today, Days = new Dictionary<DateOnly, DayData>(),
            Targets = [(D("2026-01-01"), 2000), (D("2026-11-10"), null)],
        });
        Assert.Equal(2000, c.Target(D("2026-11-09")));
        Assert.Null(c.Target(D("2026-11-10")));
        Assert.Null(c.Budget(null));
    }

    [Fact]
    public void Budget_HasNoMinimum_ButNeverNegative()
    {
        // way over target all week: the budget is clamped at zero
        var c = Empty(d =>
        {
            for (var i = 16; i <= 23; i++) d[D($"2026-11-{i}")] = Cal($"2026-11-{i}", 6000);
        });
        Assert.All(c.Budget(null)!.Days.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void NotEnoughLoggedDays_NoTdeeEstimate()
    {
        var c = Empty(d =>
        {
            for (var i = 0; i < 40; i++)
            {
                var day = Today.AddDays(-i - 1);
                d[day] = new DayData(day, 80 - i * 0.02, i % 2 == 0 ? 2000 : null, null, false, false);
            }
        });
        var r = c.TdeeEstimate(90);
        Assert.Null(r.Estimate);
        Assert.Contains("70%", r.Missing);
    }
}
