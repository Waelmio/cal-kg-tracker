using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class TdeeEndpointTestsBase
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
    public async Task GetComputed_Returns422_WhenInsufficientData()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/tdee/computed");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetComputed_Returns400_WhenDaysBelowMinimum()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/tdee/computed?days=13");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetComputed_Returns400_WhenDaysAboveMaximum()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/tdee/computed?days=366");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetComputed_Returns200_WithSufficientData()
    {
        await ResetAsync();

        // Seed 14 days of data within the last 90 days (need ≥7 calorie + ≥3 weight entries)
        await SeedAsync(b =>
        {
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
            for (int i = 0; i < 14; i++)
            {
                var date = today.AddDays(-i).ToString("yyyy-MM-dd");
                var weight = i % 3 == 0 ? (decimal?)(80m - i * 0.05m) : null;
                b.WithDailyLog(date, weightKg: weight, calories: 2000 + i * 10);
            }
        });

        var response = await Client.GetAsync("/api/tdee/computed?days=90");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<TdeeComputationDto>();
        Assert.NotNull(result);
        Assert.True(result.EstimatedTdeeKcal > 0);
        Assert.True(result.CalorieDataPoints >= 7);
        Assert.True(result.WeightDataPoints >= 3);
    }
}

public class TdeeEndpointTests_Sqlite : TdeeEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public TdeeEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class TdeeEndpointTests_Postgres : TdeeEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public TdeeEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
