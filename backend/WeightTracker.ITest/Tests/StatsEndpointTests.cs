using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

/// <summary>Section 3.11 of the redesign plan, end to end: fixture imported through the API, stats read back.</summary>
public abstract class StatsEndpointTestsBase
{
    protected abstract WebApplicationFactory<Program> Factory { get; }
    private const string Today = "2026-11-24";

    private async Task<HttpClient> LoadFixtureAsync(int? averagingWeeks = null)
    {
        var client = Factory.CreateClient();
        using (var scope = Factory.Services.CreateScope())
            await DbFixture.ResetDatabaseAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fixture.json"));
        var response = await client.PostAsync("/api/data/import", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        if (averagingWeeks is not null)
        {
            var r = await client.PutAsJsonAsync("/api/settings", new { heightCm = 178, preferredUnit = "kg", averagingWeeks });
            r.EnsureSuccessStatusCode();
        }
        return client;
    }

    private static async Task<JsonElement> Get(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Theory]
    [InlineData(1, 1877)]
    [InlineData(2, 1881)]
    [InlineData(3, 1786)]
    [InlineData(4, 1605)]
    [InlineData(5, 1483)]
    public async Task Dashboard_TodaysBudget_FollowsAveragingWindow(int window, int expected)
    {
        var client = await LoadFixtureAsync(window);
        var d = await Get(client, $"/api/stats/dashboard?today={Today}");
        Assert.Equal(expected, d.GetProperty("budget").GetProperty("todayBudget").GetInt32());
        Assert.Equal(window, d.GetProperty("averagingWeeks").GetInt32());
    }

    [Fact]
    public async Task BudgetByWindow_ReturnsTodaysBudgetForEachWindow()
    {
        var client = await LoadFixtureAsync();
        var r = await Get(client, $"/api/stats/budget-by-window?today={Today}");
        Assert.Equal(new[] { 1877, 1881, 1786, 1605, 1483 }, r.EnumerateArray().Select(x => x.GetProperty("todayBudget").GetInt32()).ToArray());
    }

    [Fact]
    public async Task ADayNobodyLoggedThisWeek_IsLeftOutOfTheExpectedFigures()
    {
        var client = await LoadFixtureAsync();
        (await client.DeleteAsync("/api/daily-logs/2026-11-23")).EnsureSuccessStatusCode();
        var b = (await Get(client, $"/api/stats/dashboard?today={Today}")).GetProperty("budget");
        var days = b.GetProperty("days").EnumerateArray().Select(d => d.GetProperty("kcal").GetInt32()).ToList();
        Assert.Equal(6, days.Count);
        Assert.Equal((int)Math.Round(days.Average(), MidpointRounding.AwayFromZero), b.GetProperty("expectedAverage").GetInt32());
        Assert.Equal(6 * 2350 - days.Sum(), b.GetProperty("expectedDeficitKcal").GetInt32());
    }

    [Fact]
    public async Task LogWeek_HasEverythingTheSheetNeeds()
    {
        var client = await LoadFixtureAsync();
        await client.PutAsJsonAsync($"/api/daily-logs/2026-11-25?today={Today}", new { date = "2026-11-25", notes = "dinner out", isCheatDay = true });
        var w = await Get(client, $"/api/stats/log-week?date=2026-11-24&today={Today}");

        Assert.Equal("2026-11-23", w.GetProperty("monday").GetString());
        var days = w.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        Assert.Equal(1689, days[0].GetProperty("calories").GetInt32());
        Assert.Equal("under", days[0].GetProperty("status").GetString());
        Assert.Equal("dinner out", days[2].GetProperty("notes").GetString());
        Assert.True(days[2].GetProperty("isCheatDay").GetBoolean());
        Assert.Equal(1850, days[2].GetProperty("calorieTarget").GetInt32());   // the plain target, the sheet toggles cheat itself
        Assert.Equal(2350, days[2].GetProperty("tdee").GetInt32());
        Assert.Equal(83.8, days[6].GetProperty("lastWeightKg").GetDouble());
        Assert.Equal(days[1].GetProperty("planned").GetInt32(), w.GetProperty("thisWeek").GetProperty("todayBudget").GetDouble(), 0.5);
        Assert.Equal(5, w.GetProperty("thisWeek").GetProperty("otherDays").GetInt32());

        // Another week has no pool to share, and a day far in the future still knows the last weight.
        var later = await Get(client, $"/api/stats/log-week?date=2028-01-01&today={Today}");
        Assert.Equal(JsonValueKind.Null, later.GetProperty("thisWeek").ValueKind);
        Assert.Equal(83.8, later.GetProperty("days")[0].GetProperty("lastWeightKg").GetDouble());
    }

    [Fact]
    public async Task ThePool_DoesNotDependOnWhatIsEatenToday()
    {
        var client = await LoadFixtureAsync();
        async Task<double> Pool(int kcal)
        {
            await client.PutAsJsonAsync($"/api/daily-logs/{Today}?today={Today}", new { date = Today, caloriesKcal = kcal });
            return (await Get(client, $"/api/stats/log-week?date={Today}&today={Today}")).GetProperty("thisWeek").GetProperty("pool").GetDouble();
        }
        Assert.Equal(await Pool(1240), await Pool(2400), 6);
        // 2400 eaten leaves 1777 for each of the five other days (PLAN.md 3.11).
        Assert.Equal(1777, (int)Math.Round((await Pool(2400) - 2400) / 5.0));
    }

    [Fact]
    public async Task AGainGoal_IsOnTimeAboveTheTdee()
    {
        var client = await LoadFixtureAsync();
        await client.PostAsJsonAsync($"/api/goals?today={Today}", new { targetWeightKg = 90, targetDate = "2027-06-01", startingWeightKg = 84 });
        var d = await Get(client, $"/api/stats/dashboard?today={Today}");
        Assert.False(d.GetProperty("goal").GetProperty("losing").GetBoolean());
        Assert.True(d.GetProperty("targetComparison").GetProperty("onTimeKcal").GetInt32() > 2350);
    }

    [Fact]
    public async Task OnceTheGoalsDateHasPassed_TheDashboardIsTrackingOnly()
    {
        var client = await LoadFixtureAsync();
        var d = await Get(client, "/api/stats/dashboard?today=2027-02-20");
        Assert.Equal("datePassed", d.GetProperty("goal").GetProperty("state").GetString());
        Assert.False(d.GetProperty("deficits").GetProperty("overall").GetProperty("sinceGoalStart").GetBoolean());
    }

    [Theory]
    [InlineData("/api/goals")]
    [InlineData("/api/goals/maintain")]
    [InlineData("/api/calorie-goals")]
    public async Task WritesThatDependOnTheDate_RequireToday(string url)
    {
        var client = await LoadFixtureAsync();
        var r = await client.PostAsJsonAsync(url, new { targetWeightKg = 70, targetDate = "2028-01-01", targetCalories = 1800 });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Figures()
    {
        var client = await LoadFixtureAsync();
        var d = await Get(client, $"/api/stats/dashboard?today={Today}");

        var budget = d.GetProperty("budget");
        Assert.Equal(1854, budget.GetProperty("expectedAverage").GetInt32());
        Assert.Equal(3475, budget.GetProperty("expectedDeficitKcal").GetInt32());

        var streak = d.GetProperty("streak");
        Assert.Equal(9, streak.GetProperty("days").GetInt32());
        Assert.Equal(26, streak.GetProperty("nextGap").GetInt32());
        var rows = streak.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal((1853, 3), (rows[0].GetProperty("avgCalories").GetInt32(), rows[0].GetProperty("avgGap").GetInt32()));
        Assert.Equal((1924, 39), (rows[1].GetProperty("avgCalories").GetInt32(), rows[1].GetProperty("avgGap").GetInt32()));
        Assert.Equal((1953, 79), (rows[2].GetProperty("avgCalories").GetInt32(), rows[2].GetProperty("avgGap").GetInt32()));

        var overall = d.GetProperty("deficits").GetProperty("overall");
        Assert.Equal(26839, overall.GetProperty("kcal").GetInt32());
        Assert.Equal(3.49, Math.Round(overall.GetProperty("kg").GetDouble(), 2));
        Assert.Equal(2361, overall.GetProperty("gapKcal").GetInt32());
        Assert.Equal(67, overall.GetProperty("loggedDays").GetInt32());
        Assert.Equal(75, overall.GetProperty("totalDays").GetInt32());

        var weight = d.GetProperty("weight");
        Assert.Equal(84.07, Math.Round(weight.GetProperty("kg").GetDouble(), 2));
        Assert.Equal(-0.41, Math.Round(weight.GetProperty("paceKgPerWeek").GetDouble(), 2));

        var cmp = d.GetProperty("targetComparison");
        Assert.Equal(500, cmp.GetProperty("targetDeficit").GetInt32());                      // 2350 - 1850
        Assert.Equal(0.45, Math.Round(cmp.GetProperty("targetKgPerWeek").GetDouble(), 2));   // 500 * 7 / 7700
        Assert.Equal(2350 - cmp.GetProperty("last4Avg").GetInt32(), cmp.GetProperty("last4Deficit").GetInt32());
        Assert.InRange(cmp.GetProperty("onTimeKcal").GetInt32(), 1760, 1775);                // 2350 - 0.53 * 7700 / 7

        var goal = d.GetProperty("goal");
        Assert.Equal("active", goal.GetProperty("state").GetString());
        Assert.Equal(43, Math.Round(goal.GetProperty("progressPercent").GetDouble()));
        Assert.Equal(0.53, Math.Round(goal.GetProperty("neededKgPerWeek").GetDouble(), 2));
        Assert.Equal("2027-03-07", goal.GetProperty("projectedDate").GetString());
        Assert.Equal(3, goal.GetProperty("lateWeeks").GetInt32());
    }

    [Fact]
    public async Task Calendar_WeeksAndDays()
    {
        var client = await LoadFixtureAsync();
        var c = await Get(client, $"/api/stats/calendar?from=2026-09-28&to=2026-11-30&today={Today}");

        var weeks = c.GetProperty("weeks").EnumerateArray().ToDictionary(w => w.GetProperty("monday").GetString()!);
        Assert.Equal("over", weeks["2026-09-28"].GetProperty("figures").GetProperty("status").GetString());

        var w47 = weeks["2026-11-16"].GetProperty("figures");
        Assert.Equal(1847, w47.GetProperty("avgCalories").GetInt32());
        Assert.Equal(1850, w47.GetProperty("avgTarget").GetInt32());
        Assert.Equal(-22, w47.GetProperty("gap").GetInt32());
        Assert.Equal(-0.98, Math.Round(w47.GetProperty("weightChangeKg").GetDouble(), 2));

        var days = c.GetProperty("days").EnumerateArray().ToDictionary(x => x.GetProperty("date").GetString()!);
        Assert.Equal("balanced", days["2026-11-20"].GetProperty("status").GetString());
        Assert.Equal("over", days["2026-11-14"].GetProperty("status").GetString());
        Assert.Equal(1881, days[Today].GetProperty("planned").GetInt32());
        Assert.Equal(JsonValueKind.Null, days["2026-11-25"].GetProperty("calories").ValueKind);
    }

    [Fact]
    public async Task Calendar_PlansTheFollowingWeeks_AsFarAsTheWindowReaches()
    {
        var two = await Get(await LoadFixtureAsync(2), $"/api/stats/calendar?from=2026-11-30&to=2026-12-13&today={Today}");
        var days = two.GetProperty("days").EnumerateArray().ToDictionary(x => x.GetProperty("date").GetString()!);
        Assert.InRange(days["2026-12-03"].GetProperty("planned").GetInt32(), 1845, 1847);   // next week: planned
        Assert.Equal(JsonValueKind.Null, days["2026-12-08"].GetProperty("planned").ValueKind);   // two weeks ahead: not

        var one = await Get(await LoadFixtureAsync(1), $"/api/stats/calendar?from=2026-11-30&to=2026-12-06&today={Today}");
        Assert.All(one.GetProperty("days").EnumerateArray(), d => Assert.Equal(JsonValueKind.Null, d.GetProperty("planned").ValueKind));
    }

    [Fact]
    public async Task Calendar_WeekStatus_DependsOnWindow()
    {
        var client = await LoadFixtureAsync(3);
        var c = await Get(client, $"/api/stats/calendar?from=2026-09-28&to=2026-10-04&today={Today}");
        var w40 = c.GetProperty("weeks")[0].GetProperty("figures");
        Assert.Equal("balanced", w40.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Month_October()
    {
        var client = await LoadFixtureAsync();
        var m = await Get(client, $"/api/stats/month/2026-10?today={Today}");
        var f = m.GetProperty("figures");
        Assert.Equal(1978, f.GetProperty("avgCalories").GetInt32());
        Assert.Equal(1912, f.GetProperty("avgTarget").GetInt32());
        Assert.Equal(9680, f.GetProperty("deficitKcal").GetInt32());
        Assert.Equal(1720, f.GetProperty("gap").GetInt32());
        Assert.Equal(26, f.GetProperty("loggedDays").GetInt32());
        Assert.Equal("over", f.GetProperty("status").GetString());
        Assert.Equal(2306, m.GetProperty("tdee").GetProperty("kcal").GetInt32());
    }

    [Fact]
    public async Task Periods_ReturnsRequestedCount_OldestFirst()
    {
        var client = await LoadFixtureAsync();
        var p = await Get(client, $"/api/stats/periods?unit=week&count=4&today={Today}");
        Assert.Equal(4, p.GetArrayLength());
        Assert.Equal("2026-11-02", p[0].GetProperty("from").GetString());
        Assert.Equal("2026-11-23", p[3].GetProperty("from").GetString());
        var months = await Get(client, $"/api/stats/periods?unit=month&count=3&today={Today}");
        Assert.Equal("2026-09-01", months[0].GetProperty("from").GetString());
    }

    [Fact]
    public async Task Tdee_AllThreePeriods_AndDietEffect()
    {
        var client = await LoadFixtureAsync();
        Assert.Equal(2357, (await Get(client, $"/api/stats/tdee?days=30&today={Today}")).GetProperty("estimate").GetProperty("kcal").GetInt32());
        Assert.Equal(2351, (await Get(client, $"/api/stats/tdee?days=60&today={Today}")).GetProperty("estimate").GetProperty("kcal").GetInt32());
        var t90 = await Get(client, $"/api/stats/tdee?days=90&today={Today}");
        Assert.Equal(2352, t90.GetProperty("estimate").GetProperty("kcal").GetInt32());
        Assert.False(t90.GetProperty("suggestUpdate").GetBoolean());   // 2352 vs 2350 in force

        var rows = t90.GetProperty("diet").GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("key").GetString()!, r => r.GetProperty("kcal").GetInt32());
        Assert.Equal(2576, rows["before"]);
        Assert.Equal(-77, rows["lighter"]);
        Assert.Equal(-61, rows["digest"]);
        Assert.Equal(-86, rows["slowed"]);
        Assert.Equal(2474, rows["keep"]);
    }

    [Fact]
    public async Task Stats_RequireToday_AndValidateInput()
    {
        var client = await LoadFixtureAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/stats/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/stats/tdee?days=45&today={Today}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/stats/month/oct?today={Today}")).StatusCode);
    }

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyStatsWithoutErrors()
    {
        var client = Factory.CreateClient();
        using (var scope = Factory.Services.CreateScope())
            await DbFixture.ResetDatabaseAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        var d = await Get(client, $"/api/stats/dashboard?today={Today}");
        Assert.Equal(JsonValueKind.Null, d.GetProperty("budget").ValueKind);
        Assert.Equal(JsonValueKind.Null, d.GetProperty("goal").ValueKind);
        Assert.Equal(0, d.GetProperty("streak").GetProperty("days").GetInt32());
        await Get(client, $"/api/stats/calendar?from=2026-11-01&to=2026-11-30&today={Today}");
        await Get(client, $"/api/stats/month/2026-11?today={Today}");
        var t = await Get(client, $"/api/stats/tdee?days=90&today={Today}");
        Assert.Equal(JsonValueKind.Null, t.GetProperty("estimate").ValueKind);
        Assert.False(string.IsNullOrEmpty(t.GetProperty("missing").GetString()));
    }
}

public class StatsEndpointTests_Sqlite : StatsEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public StatsEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class StatsEndpointTests_Postgres : StatsEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public StatsEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
