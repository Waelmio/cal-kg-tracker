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
    private static object LogBody(decimal? weightKg = null, int? caloriesKcal = null, string? notes = null, bool isCheatDay = false) =>
        new { Date = "ignored", WeightKg = weightKg, CaloriesKcal = caloriesKcal, Notes = notes, IsCheatDay = isCheatDay };

    private const string T = "?today=2024-03-20";

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

        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80.5m, 2000));

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

        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80.0m));
        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(79.5m));

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
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80.0m));

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
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80.0m));

        var deleteResponse = await Client.DeleteAsync("/api/daily-logs/2024-03-15");
        var getResponse = await Client.GetAsync("/api/daily-logs/2024-03-15");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Put_RequiresToday()
    {
        await ResetAsync();
        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15", LogBody(80m));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_IsAFullReplace_NullClearsAndZeroIsKept()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80m, 2000, "note", true));

        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(null, 0));
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();

        Assert.Equal(0, log!.CaloriesKcal);
        Assert.Null(log.WeightKg);
        Assert.Null(log.Notes);
        Assert.False(log.IsCheatDay);
    }

    [Fact]
    public async Task Put_WithNothing_RemovesTheRow_AndReturns204()
    {
        await ResetAsync();
        await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody(80m));

        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-15" + T, LogBody());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/daily-logs/2024-03-15")).StatusCode);
    }

    [Fact]
    public async Task Put_RecordsCaloriesSetOn_FromTheClientsToday()
    {
        await ResetAsync();
        var response = await Client.PutAsJsonAsync("/api/daily-logs/2024-03-30?today=2024-03-20", LogBody(null, 2500));
        var log = await response.Content.ReadFromJsonAsync<DailyLogDto>();
        Assert.Equal(new DateOnly(2024, 3, 20), log!.CaloriesSetOn);
    }

    [Fact]
    public async Task RemovedEndpoints_AreGone()
    {
        await ResetAsync();
        Assert.False((await Client.DeleteAsync("/api/daily-logs/2024-03-15/weight")).IsSuccessStatusCode);
        Assert.False((await Client.DeleteAsync("/api/daily-logs/2024-03-15/calories")).IsSuccessStatusCode);
        Assert.False((await Client.PutAsync("/api/daily-logs/2024-03-15/cheat-day", null)).IsSuccessStatusCode);
        Assert.False((await Client.PostAsync("/api/daily-logs/prefill-week?today=2024-03-15", null)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenNotFound()
    {
        await ResetAsync();

        var response = await Client.DeleteAsync("/api/daily-logs/9999-01-01");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
