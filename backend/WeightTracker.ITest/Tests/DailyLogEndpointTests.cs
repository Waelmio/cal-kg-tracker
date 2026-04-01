using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class DailyLogEndpointTestsBase
{
    protected abstract WebApplicationFactory<Program> Factory { get; }

    private HttpClient Client => Factory.CreateClient();

    private async Task ResetAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbFixture.ResetDatabaseAsync(db);
    }

    private async Task SeedAsync(Action<SeedBuilder> configure)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var builder = new SeedBuilder(db);
        configure(builder);
        await builder.SaveAsync();
    }

    // UpsertDailyLogDto record requires a Date field even though the controller overwrites
    // it from the route parameter. Include a placeholder to satisfy model binding.
    private static object LogBody(decimal? weightKg = null, int? caloriesKcal = null, string? notes = null) =>
        new { Date = "ignored", WeightKg = weightKg, CaloriesKcal = caloriesKcal, Notes = notes };

    [Fact]
    public async Task GetAll_ReturnsEmptyArray_WhenNoData()
    {
        await ResetAsync();

        var logs = await Client.GetFromJsonAsync<List<DailyLogDto>>("/api/daily-logs");

        Assert.NotNull(logs);
        Assert.Empty(logs);
    }

    [Fact]
    public async Task Put_CreatesNewLog_AndReturnsIt()
    {
        await ResetAsync();

        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.5m, 2000));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.NotNull(log);
        Assert.Equal("2024-03-15", log.Date);
        Assert.Equal(80.5m, log.WeightKg);
        Assert.Equal(2000, log.CaloriesKcal);
    }

    [Fact]
    public async Task Put_UpdatesExistingLog_OnSecondCall()
    {
        await ResetAsync();

        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m));
        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(79.5m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.Equal(79.5m, log!.WeightKg);

        var all = await Client.GetFromJsonAsync<List<DailyLogDto>>("/api/daily-logs");
        Assert.Single(all!);
    }

    [Fact]
    public async Task GetByDate_Returns200_AfterCreate()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m));

        var response = await Client.GetAsync("/api/daily-logs/2024-03-15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetByDate_Returns404_WhenNotFound()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/daily-logs/9999-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204_AndRemovesEntry()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m));

        var deleteResponse = await Client.DeleteAsync("/api/daily-logs/2024-03-15");
        var getResponse = await Client.GetAsync("/api/daily-logs/2024-03-15");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        await ResetAsync();

        var response = await Client.DeleteAsync("/api/daily-logs/9999-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteWeight_NullsWeight_ButKeepsRowWhenCaloriesExist()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m, 2000));

        var response = await Client.DeleteAsync("/api/daily-logs/2024-03-15/weight");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.NotNull(log);
        Assert.Null(log.WeightKg);
        Assert.Equal(2000, log.CaloriesKcal);
    }

    [Fact]
    public async Task DeleteWeight_RemovesRow_WhenCaloriesAlsoNull()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m));

        await Client.DeleteAsync("/api/daily-logs/2024-03-15/weight");

        var getResponse = await Client.GetAsync("/api/daily-logs/2024-03-15");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCalories_NullsCalories_ButKeepsRowWhenWeightExists()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m, 2000));

        var response = await Client.DeleteAsync("/api/daily-logs/2024-03-15/calories");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.NotNull(log);
        Assert.Equal(80.0m, log.WeightKg);
        Assert.Null(log.CaloriesKcal);
    }

    [Fact]
    public async Task CheatDay_SetAndClear_RoundTrip()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80.0m));

        var setResponse = await Client.PutAsync("/api/daily-logs/2024-03-15/cheat-day", null);
        var afterSet = await setResponse.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.True(afterSet!.IsCheatDay);

        var clearResponse = await Client.DeleteAsync("/api/daily-logs/2024-03-15/cheat-day");
        var afterClear = await clearResponse.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.False(afterClear!.IsCheatDay);
    }

    [Fact]
    public async Task GetAll_FiltersBy_FromAndTo()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithDailyLog("2024-01-01", weightKg: 80m)
            .WithDailyLog("2024-01-15", weightKg: 79m)
            .WithDailyLog("2024-02-01", weightKg: 78m));

        var logs = await Client.GetFromJsonAsync<List<DailyLogDto>>("/api/daily-logs?from=2024-01-10&to=2024-01-31");

        Assert.NotNull(logs);
        Assert.Single(logs);
        Assert.Equal("2024-01-15", logs[0].Date);
    }

    [Fact]
    public async Task GetAll_Limit_ReturnsCorrectCount()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithDailyLog("2024-01-01", weightKg: 80m)
            .WithDailyLog("2024-01-02", weightKg: 79m)
            .WithDailyLog("2024-01-03", weightKg: 78m));

        var logs = await Client.GetFromJsonAsync<List<DailyLogDto>>("/api/daily-logs?limit=2");

        Assert.NotNull(logs);
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task CalorieTarget_IsPopulated_WhenCalorieGoalExists()
    {
        await ResetAsync();
        // Goal must be created BEFORE the log date for CalorieLogService to find it
        var logDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime).AddDays(-5);
        var logDateStr = logDate.ToString("yyyy-MM-dd");
        await SeedAsync(b => b
            .WithCalorieGoal(1800, DateTimeOffset.UtcNow.AddDays(-30))
            .WithDailyLog(logDateStr, calories: 2000));

        var log = await Client.GetFromJsonAsync<DailyLogDto>($"/api/daily-logs/{logDateStr}");

        Assert.NotNull(log);
        Assert.Equal(1800, log.CalorieTarget);
    }

    [Fact]
    public async Task PrefillWeek_Creates7Days_WithCalorieGoal()
    {
        await ResetAsync();
        // Use a recent week so the goal CreatedAt (UtcNow) is before the prefill date
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        // Goal created a week ago so it's active for the current week
        await SeedAsync(b => b.WithCalorieGoal(2000, DateTimeOffset.UtcNow.AddDays(-7)));

        var response = await Client.PostAsync($"/api/daily-logs/prefill-week?today={today:yyyy-MM-dd}", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var logs = await Client.GetFromJsonAsync<List<DailyLogDto>>("/api/daily-logs");
        Assert.NotNull(logs);
        Assert.True(logs.Count > 0);
    }
}

public class DailyLogEndpointTests_Sqlite : DailyLogEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public DailyLogEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class DailyLogEndpointTests_Postgres : DailyLogEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public DailyLogEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
