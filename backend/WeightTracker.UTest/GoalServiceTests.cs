using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Data;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;
using WeightTracker.Api.Services;
using WeightTracker.Api.Stats;

namespace WeightTracker.UTest;

public class GoalServiceTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options);
    }

    private static GoalService Service(AppDbContext db) => new(db, new StatsService(db), new CalorieGoalService(db));

    private static void Weigh(AppDbContext db, string date, decimal kg) =>
        db.DailyLogs.Add(new DailyLog { Date = DateOnly.Parse(date), WeightKg = kg });

    [Fact]
    public async Task GetActive_ReturnsNull_WhenNoGoals()
    {
        using var db = CreateDb();
        Assert.Null(await Service(db).GetActiveAsync());
    }

    [Fact]
    public async Task Create_DefaultsStartingWeightToSevenDayAverageAtStartDate()
    {
        using var db = CreateDb();
        Weigh(db, "2026-11-20", 84m);
        Weigh(db, "2026-11-22", 83m);
        Weigh(db, "2026-11-10", 99m);   // outside the 7 days
        await db.SaveChangesAsync();

        var goal = await Service(db).CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 2, 1), null, null), Today);

        Assert.Equal(83.5m, goal.StartingWeightKg);
        Assert.Equal(Today, goal.StartDate);
        Assert.Equal(GoalKind.Reach, goal.Kind);
    }

    [Fact]
    public async Task Create_UsesNearestWeighInWithinSevenDays_WhenNoneInTheWindow()
    {
        using var db = CreateDb();
        Weigh(db, "2026-11-27", 82m);      // 3 days after the start, nothing before
        await db.SaveChangesAsync();
        var goal = await Service(db).CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 2, 1), new DateOnly(2026, 11, 24), null), new DateOnly(2026, 11, 28));
        Assert.Equal(82m, goal.StartingWeightKg);
    }

    [Fact]
    public async Task Create_StartingWeightIsNull_WhenNoWeights()
    {
        using var db = CreateDb();
        var goal = await Service(db).CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 2, 1), null, null), Today);
        Assert.Null(goal.StartingWeightKg);
    }

    [Fact]
    public async Task Create_AllowsEditedStartingWeight_AndGainGoals()
    {
        using var db = CreateDb();
        var goal = await Service(db).CreateAsync(new CreateGoalDto(75, new DateOnly(2027, 2, 1), null, null, 70m), Today);
        Assert.Equal(70m, goal.StartingWeightKg);
        Assert.True(goal.TargetWeightKg > goal.StartingWeightKg);
    }

    [Fact]
    public async Task Create_RejectsTargetDateNotAfterStart()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<GoalException>(() =>
            Service(db).CreateAsync(new CreateGoalDto(78, Today, null, null), Today));
    }

    [Fact]
    public async Task Create_EndsTheActiveGoalAsReplaced_AndKeepsItInHistory()
    {
        using var db = CreateDb();
        var service = Service(db);
        var first = await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today.AddDays(-30));
        var second = await service.CreateAsync(new CreateGoalDto(75, new DateOnly(2027, 6, 1), null, null, 88m), Today);

        var all = await service.GetAllAsync();
        Assert.Equal(2, all.Count);
        var old = all.Single(g => g.Id == first.Id);
        Assert.Equal(Today, old.EndedOn);
        Assert.Equal(GoalEndReason.Replaced, old.EndReason);
        Assert.Equal(second.Id, (await service.GetActiveAsync())!.Id);
    }

    [Fact]
    public async Task DeletingTheLatestGoal_NeverRevivesAnOldOne()
    {
        using var db = CreateDb();
        var service = Service(db);
        await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today.AddDays(-30));
        var second = await service.CreateAsync(new CreateGoalDto(75, new DateOnly(2027, 6, 1), null, null, 88m), Today);

        Assert.True(await service.DeleteAsync(second.Id));

        Assert.Null(await service.GetActiveAsync());
        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task End_MarksTheGoalEnded_AndItNeverBecomesActiveAgain()
    {
        using var db = CreateDb();
        var service = Service(db);
        var goal = await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today);

        var ended = await service.EndAsync(goal.Id, new EndGoalDto(GoalEndReason.Reached), Today.AddDays(10));

        Assert.Equal(GoalEndReason.Reached, ended!.EndReason);
        Assert.Equal(Today.AddDays(10), ended.EndedOn);
        Assert.Null(await service.GetActiveAsync());
        await Assert.ThrowsAsync<GoalException>(() => service.EndAsync(goal.Id, new EndGoalDto(GoalEndReason.Ended), Today));
    }

    [Fact]
    public async Task End_RejectsReasonsOnlyTheSystemSets()
    {
        using var db = CreateDb();
        var service = Service(db);
        var goal = await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today);
        await Assert.ThrowsAsync<GoalException>(() => service.EndAsync(goal.Id, new EndGoalDto(GoalEndReason.Replaced), Today));
    }

    [Fact]
    public async Task Update_EditsInPlace_ButNotAnEndedGoal()
    {
        using var db = CreateDb();
        var service = Service(db);
        var goal = await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today);

        var updated = await service.UpdateAsync(goal.Id, new UpdateGoalDto(77m, new DateOnly(2027, 3, 1), Today, 87.5m, "note"));
        Assert.Equal(77m, updated!.TargetWeightKg);
        Assert.Equal(goal.Id, updated.Id);
        Assert.Single(await service.GetAllAsync());

        await service.EndAsync(goal.Id, new EndGoalDto(GoalEndReason.Ended), Today);
        var ex = await Assert.ThrowsAsync<GoalException>(() => service.UpdateAsync(goal.Id, new UpdateGoalDto(77m, new DateOnly(2027, 3, 1), Today, 87.5m, null)));
        Assert.Equal(409, ex.Status);
    }

    [Fact]
    public async Task Maintain_CreatesMaintenanceGoal_AndSetsCalorieTargetToTdee_WhenNoEstimate()
    {
        using var db = CreateDb();
        Weigh(db, "2026-11-23", 80.04m);
        db.TdeeEntries.Add(new TdeeEntry { Kcal = 2400, EffectiveFrom = new DateOnly(2026, 1, 1) });
        await db.SaveChangesAsync();
        var service = Service(db);
        var old = await service.CreateAsync(new CreateGoalDto(78, new DateOnly(2027, 1, 1), null, null, 88m), Today.AddDays(-5));

        var maintain = await service.MaintainAsync(Today);

        Assert.Equal(GoalKind.Maintain, maintain.Kind);
        Assert.Null(maintain.TargetDate);
        Assert.Equal(80.0m, maintain.TargetWeightKg);
        var ended = (await service.GetAllAsync()).Single(g => g.Id == old.Id);
        Assert.Equal(GoalEndReason.Maintain, ended.EndReason);
        var target = await db.CalorieGoals.SingleAsync();
        Assert.Equal(2400, target.TargetCalories);
        Assert.Equal(Today, target.EffectiveFrom);
    }

    [Fact]
    public async Task Maintain_Fails_WithoutAWeight_OrWithoutATdee()
    {
        using var db = CreateDb();
        await Assert.ThrowsAsync<GoalException>(() => Service(db).MaintainAsync(Today));
        Weigh(db, "2026-11-23", 80m);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<GoalException>(() => Service(db).MaintainAsync(Today));
    }
}

public class GoalStatusTests
{
    private static GoalStatusDto Status(Goal goal, StatsCalculator calc) => GoalStatusCalculator.Compute(goal, calc);

    private static Goal FixtureGoal(Action<Goal>? edit = null)
    {
        var f = FixtureLoader.Export().Goals[0];
        var g = new Goal { Id = 1, TargetWeightKg = f.TargetWeightKg, TargetDate = f.TargetDate, StartingWeightKg = f.StartingWeightKg, StartDate = f.StartDate };
        edit?.Invoke(g);
        return g;
    }

    [Fact]
    public void Fixture_IsActiveAndBehind()
    {
        var s = Status(FixtureGoal(), FixtureLoader.Calc());
        Assert.Equal("active", s.State);
        Assert.Equal("behind", s.Pace);
        Assert.Equal(43, Math.Round(s.ProgressPercent!.Value));
        Assert.Equal(0.53, Math.Round(s.NeededKgPerWeek!.Value, 2));
        Assert.Equal("2027-03-07", s.ProjectedDate);
        Assert.Equal(3, s.LateWeeks);
    }

    [Fact]
    public void AheadOfPlan_WhenProjectedWellBeforeTargetDate()
    {
        var s = Status(FixtureGoal(g => g.TargetDate = new DateOnly(2027, 6, 1)), FixtureLoader.Calc());
        Assert.Equal("ahead", s.Pace);
    }

    [Fact]
    public void OnTrack_WhenProjectedWithinAWeekOfTheTargetDate()
    {
        var s = Status(FixtureGoal(g => g.TargetDate = new DateOnly(2027, 3, 5)), FixtureLoader.Calc());
        Assert.Equal("onTrack", s.Pace);
    }

    [Fact]
    public void Reached_WhenSevenDayAverageCrossedTheTarget()
    {
        var s = Status(FixtureGoal(g => g.TargetWeightKg = 85m), FixtureLoader.Calc());
        Assert.Equal("reached", s.State);
        Assert.Equal(100, s.ProgressPercent);
    }

    [Fact]
    public void DatePassed_WhenTheTargetDateIsBehindUs()
    {
        var s = Status(FixtureGoal(g => g.TargetDate = new DateOnly(2026, 11, 1)), FixtureLoader.Calc());
        Assert.Equal("datePassed", s.State);
        Assert.Null(s.Pace);
    }

    [Fact]
    public void GainGoal_ProgressesTowardsAHigherWeight()
    {
        var calc = FixtureLoader.Calc(2, days =>
        {
            // a clear upward trend over the last weeks
            for (var i = 0; i < 28; i++)
            {
                var d = FixtureLoader.Today.AddDays(-i);
                days[d] = new DayData(d, 70 + (28 - i) * 0.05, days.TryGetValue(d, out var x) ? x.Calories : null, null, false, false);
            }
        });
        var s = Status(new Goal { Id = 1, StartingWeightKg = 70m, TargetWeightKg = 75m, TargetDate = new DateOnly(2027, 6, 1), StartDate = new DateOnly(2026, 11, 1) }, calc);
        Assert.Equal("active", s.State);
        Assert.InRange(s.ProgressPercent!.Value, 20, 40);
        Assert.NotNull(s.ProjectedDate);
        Assert.True(s.PaceKgPerWeek > 0);
    }

    [Fact]
    public void MovingTheWrongWay_HasNoProjection()
    {
        // pace is downwards but the goal is a gain
        var s = Status(FixtureGoal(g => { g.StartingWeightKg = 80m; g.TargetWeightKg = 90m; }), FixtureLoader.Calc());
        Assert.Null(s.ProjectedDate);
        Assert.Equal("behind", s.Pace);
    }

    [Fact]
    public void NoWeights_StillReturnsAnActiveGoal()
    {
        var calc = new StatsCalculator(new StatsData { Today = FixtureLoader.Today, Days = new Dictionary<DateOnly, DayData>() });
        var s = Status(FixtureGoal(), calc);
        Assert.Equal("active", s.State);
        Assert.Null(s.CurrentKg);
        Assert.Null(s.ProgressPercent);
    }
}
