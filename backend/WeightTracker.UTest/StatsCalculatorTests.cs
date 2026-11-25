using WeightTracker.Api.Stats;
using static WeightTracker.Api.Stats.StatsCalculator;

namespace WeightTracker.UTest;

/// <summary>Section 3.11 of the redesign plan, computed from the fixture.</summary>
public class StatsCalculatorTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);
    private static readonly DateOnly Today = FixtureLoader.Today;

    private static int[] BudgetValues(StatsCalculator c) => c.Budget()!.Days.Values.ToArray();

    [Theory]
    [InlineData(1, 1877)]
    [InlineData(2, 1881)]
    [InlineData(3, 1786)]
    [InlineData(4, 1605)]
    [InlineData(5, 1483)]
    public void TodaysBudget_ByWindow(int window, int expected)
    {
        Assert.Equal(expected, FixtureLoader.Calc(window).Budget()!.TodayBudget);
    }

    [Fact]
    public void Window2_TodayEaten2400_OtherDaysGetLess()
    {
        var b = FixtureLoader.Calc(2, days => days[Today] = new DayData(Today, null, 2400, Today, false)).Budget()!;
        Assert.Equal(1881, b.TodayBudget);
        Assert.All(b.Days.Where(p => p.Key > Today), p => Assert.Equal(1777, p.Value));
    }

    [Fact]
    public void Window2_SaturdayPlanned2500_OtherDaysGetLess()
    {
        var saturday = D("2026-11-28");
        var calc = FixtureLoader.Calc(2, days => days[saturday] = new DayData(saturday, null, 2500, Today, false));
        var b = calc.Budget()!;
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
        new(D(date), kg, kcal, null, cheat);

    [Fact]
    public void EmptyDatabase_NothingThrows()
    {
        var c = Empty(target: null, tdee: null);
        Assert.Null(c.Budget());
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
        Assert.Null(c.Budget());
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

    private static StatsCalculator TodayLogged(int window = 2) =>
        FixtureLoader.Calc(window, days => days[Today] = new DayData(Today, null, 1240, Today, false));

    [Fact]
    public void Today_CountsAsSoonAsItIsLogged()
    {
        Assert.False(FixtureLoader.Calc().IsLogged(Today));
        Assert.Null(FixtureLoader.Calc().DayStatus(Today));

        var c = TodayLogged();
        Assert.Equal(Today, c.LastDay);
        Assert.Equal(WeekStatus.Under, c.DayStatus(Today));
        var w = c.Week(MondayOf(Today));
        Assert.Equal((2, 1465, -771, 1771), (w.LoggedDays, Rnd(w.AvgCalories!.Value), w.Gap, w.Deficit));
    }

    [Theory]     // reference.py, "Today logged at 1240"
    [InlineData(2, 2009)]
    [InlineData(3, 1895)]
    public void TodayLogged_TheOtherDaysShareWhatItLeft(int window, int others)
    {
        var b = TodayLogged(window).Budget()!;
        Assert.Equal(window == 2 ? 1881 : 1786, b.TodayBudget);      // what it was before logging
        Assert.Equal(others, b.PerDay);
        Assert.All(b.Days.Where(p => p.Key > Today), p => Assert.Equal(others, p.Value));
    }

    [Fact]
    public void PerDay_OnALoggedSunday_IsTodaysBudget()
    {
        var sunday = D("2026-11-29");
        var b = new StatsCalculator(new StatsData
        {
            Today = sunday, Days = new Dictionary<DateOnly, DayData> { [sunday] = new(sunday, null, 1500, null, false) }, Targets = [(D("2026-01-01"), 1800)],
        }).Budget()!;
        Assert.Equal(1800, b.PerDay);
    }

    [Fact]
    public void FutureLog_ShapesBudgetNow_AndCountsOnceItsDayHasPassed()
    {
        var saturday = D("2026-11-28");
        var c = Empty(d => d[saturday] = Cal("2026-11-28", 3000));
        Assert.False(c.IsLogged(saturday));
        Assert.Equal(3000, c.Budget()!.Days[saturday]);

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
        Assert.Null(c.Budget());
    }

    [Fact]
    public void Budget_HasNoMinimum_ButNeverNegative()
    {
        // way over target all week: the budget is clamped at zero
        var c = Empty(d =>
        {
            for (var i = 16; i <= 23; i++) d[D($"2026-11-{i}")] = Cal($"2026-11-{i}", 6000);
        });
        Assert.All(c.Budget()!.Days.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public void NotEnoughLoggedDays_NoTdeeEstimate()
    {
        var c = Empty(d =>
        {
            for (var i = 0; i < 40; i++)
            {
                var day = Today.AddDays(-i - 1);
                d[day] = new DayData(day, 80 - i * 0.02, i % 2 == 0 ? 2000 : null, null, false);
            }
        });
        var r = c.TdeeEstimate(90);
        Assert.Null(r.Estimate);
        Assert.Contains("70%", r.Missing);
    }
}

public class FutureBudgetTests
{
    private static DateOnly D(string s) => DateOnly.Parse(s);

    [Fact]
    public void Window1_PlansNothingBeyondThisWeek()
    {
        Assert.Empty(FixtureLoader.Calc(1).Budget()!.FutureDays);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void PlansWindowMinusOneWeeksAhead_EveryWindowOnTarget_NeverAboveTheTarget(int window)
    {
        var c = FixtureLoader.Calc(window);
        var b = c.Budget()!;
        Assert.Equal(7 * (window - 1), b.FutureDays.Count);
        Assert.Equal(D("2026-11-30"), b.FutureDays.Keys.Min());
        Assert.All(b.FutureDays, p => Assert.InRange(p.Value, 0, c.Target(p.Key)!.Value));

        for (var k = 1; k < window; k++)
        {
            var monday = MondayOf(FixtureLoader.Today).AddDays(7 * k);
            var counted = StatsCalculator.Range(monday.AddDays(-7 * (window - 1)), monday.AddDays(6))
                .Where(d => d >= FixtureLoader.Today || c.IsLogged(d)).ToList();
            var gap = counted.Sum(d => (d < FixtureLoader.Today ? c.Calories(d)!.Value : b.Days.TryGetValue(d, out var x) ? x : b.FutureDays[d]) - c.Target(d)!.Value);
            Assert.True(gap <= counted.Count, $"week {k}: {gap} over");   // rounding of each day to whole kcal
        }
    }

    private static StatsCalculator Overate(int window, int kcal, string from = "2026-11-16") => FixtureLoader.Calc(window, days =>
    {
        foreach (var d in days.Keys.Where(d => d >= D("2026-11-09")).ToList()) days.Remove(d);
        for (var d = D(from); d < FixtureLoader.Today; d = d.AddDays(1)) days[d] = new DayData(d, null, kcal, null, false);
    });

    [Fact]
    public void PayingBackAWeek_DoesNotHandTheCaloriesBackTheWeekAfter()
    {
        // 8 days at 2250: 3200 over, which this week's 6 days absorb in full.
        var b = Overate(2, 2250).Budget()!;
        Assert.True(b.ReachesTarget);
        Assert.Equal(1850 - 3200 / 6.0, b.TodayBudget, 0.5);
        // Without the cap, next week would be planned as far above the target as this one is below it.
        Assert.All(b.FutureDays.Values, v => Assert.Equal(1850, v));
    }

    [Fact]
    public void ADebtThisWeekCannotAbsorb_LowersTheNextWeeks_UntilItLeavesTheWindow()
    {
        // 8 days at 4000: 17200 over. Six days at 0 absorb 11100, so 6100 are left for next week: 871 a day less.
        var b = Overate(3, 4000).Budget()!;
        Assert.False(b.ReachesTarget);
        Assert.All(b.Days.Values, v => Assert.Equal(0, v));
        Assert.All(b.FutureDays.Where(p => p.Key < D("2026-12-07")), p => Assert.InRange(p.Value, 1850 - 872, 1850 - 871));
        Assert.All(b.FutureDays.Where(p => p.Key >= D("2026-12-07")), p => Assert.Equal(1850, p.Value));   // paid back
    }

    [Theory]
    [InlineData(2900)]
    [InlineData(3230)]
    [InlineData(3250)]
    [InlineData(3300)]
    public void ThePlanChangesSmoothly_WhenTheBudgetReachesZero(int kcal)
    {
        int Next(int k) => Overate(3, k).Budget()!.FutureDays[D("2026-11-30")];
        Assert.InRange(Next(kcal) - Next(kcal + 10), 0, 12);   // 8 days x 10 kcal over 7 days
    }

    [Fact]
    public void ReachesTarget_IsFalse_WhenSundayIsOverBudget()
    {
        var sunday = D("2026-11-29");
        bool Reaches(int kcal) => new StatsCalculator(new StatsData
        {
            Today = sunday, Days = new Dictionary<DateOnly, DayData> { [sunday] = new(sunday, null, kcal, null, false) }, Targets = [(D("2026-01-01"), 1800)],
        }).Budget()!.ReachesTarget;
        Assert.True(Reaches(1800));
        Assert.False(Reaches(2500));
    }

    [Fact]
    public void FutureLogsTheUserTyped_AreKeptAsTyped()
    {
        var day = D("2026-12-02");
        var c = FixtureLoader.Calc(2, days => days[day] = new DayData(day, null, 2600, FixtureLoader.Today, false));
        var b = c.Budget()!;
        Assert.Equal(2600, b.FutureDays[day]);
        Assert.Equal(1850 - (750 + 28) / 6.0, b.FutureDays[D("2026-12-03")], 1.5);   // 750 over, plus this week's 28
    }

    [Fact]
    public void NoTarget_NoFutureDays()
    {
        var c = new StatsCalculator(new StatsData { Today = FixtureLoader.Today, Days = new Dictionary<DateOnly, DayData>(), Window = 3 });
        Assert.Null(c.Budget());
    }
}
