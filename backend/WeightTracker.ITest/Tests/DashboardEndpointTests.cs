using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class DashboardEndpointTestsBase
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

    [Fact]
    public async Task Get_ReturnsGracefully_WithNoData()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/dashboard?today=2024-03-15");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await response.Content.ReadFromJsonAsync<DashboardDto>();
        Assert.NotNull(dashboard);
        Assert.Null(dashboard.CurrentWeightKg);
        Assert.Null(dashboard.TodayCaloriesKcal);
        Assert.Null(dashboard.AvgWeight7Days);
        Assert.Equal(0, dashboard.TotalEntries);
    }

    [Fact]
    public async Task Get_PopulatesCurrentWeight_WhenTodayLogExists()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-03-15", weightKg: 80.5m));

        var dashboard = await Client.GetFromJsonAsync<DashboardDto>("/api/dashboard?today=2024-03-15");

        Assert.NotNull(dashboard);
        Assert.Equal(80.5m, dashboard.CurrentWeightKg);
    }

    [Fact]
    public async Task Get_ComputesAvgWeight7Days_WithSufficientData()
    {
        await ResetAsync();
        // Seed 7 consecutive days of weight
        await SeedAsync(b =>
        {
            for (int i = 6; i >= 0; i--)
                b.WithDailyLog($"2024-03-{15 - i:D2}", weightKg: 80m - i * 0.1m);
            return;
        });

        var dashboard = await Client.GetFromJsonAsync<DashboardDto>("/api/dashboard?today=2024-03-15");

        Assert.NotNull(dashboard);
        Assert.NotNull(dashboard.AvgWeight7Days);
    }

    [Fact]
    public async Task Get_PopulatesTodayCalories_WhenLogExists()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-03-15", calories: 1800));

        var dashboard = await Client.GetFromJsonAsync<DashboardDto>("/api/dashboard?today=2024-03-15");

        Assert.NotNull(dashboard);
        Assert.Equal(1800, dashboard.TodayCaloriesKcal);
    }

    [Fact]
    public async Task Get_PopulatesGoalProgress_WhenGoalAndWeightExist()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithDailyLog("2024-03-15", weightKg: 80m)
            .WithWeightGoal(70m, new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1), startingWeightKg: 85m));

        var dashboard = await Client.GetFromJsonAsync<DashboardDto>("/api/dashboard?today=2024-03-15");

        Assert.NotNull(dashboard);
        Assert.NotNull(dashboard.ActiveGoal);
        Assert.NotNull(dashboard.GoalProgressPercent);
        Assert.True(dashboard.GoalProgressPercent > 0);
    }

    [Fact]
    public async Task Get_TodayOverride_RespectsQueryParam()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithDailyLog("2024-01-10", weightKg: 80m)
            .WithDailyLog("2024-06-20", weightKg: 75m));

        var dashboard = await Client.GetFromJsonAsync<DashboardDto>("/api/dashboard?today=2024-01-10");

        Assert.NotNull(dashboard);
        Assert.Equal(80m, dashboard.CurrentWeightKg);
    }
}

public class DashboardEndpointTests_Sqlite : DashboardEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public DashboardEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class DashboardEndpointTests_Postgres : DashboardEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public DashboardEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
