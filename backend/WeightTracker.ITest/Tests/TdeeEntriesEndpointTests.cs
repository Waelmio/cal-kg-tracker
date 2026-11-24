using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class TdeeEntriesEndpointTestsBase
{
    protected abstract WebApplicationFactory<Program> Factory { get; }

    private async Task<HttpClient> ResetAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await DbFixture.ResetDatabaseAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        return Factory.CreateClient();
    }

    [Fact]
    public async Task Post_AddsEntryFromToday_KeepsHistory_AndReplacesSameDay()
    {
        var client = await ResetAsync();
        await client.PostAsJsonAsync("/api/tdee-entries?today=2026-01-01", new { kcal = 2300 });
        await client.PostAsJsonAsync("/api/tdee-entries?today=2026-06-01", new { kcal = 2400, source = "Estimate" });
        await client.PostAsJsonAsync("/api/tdee-entries?today=2026-06-01", new { kcal = 2450 });

        var all = await client.GetFromJsonAsync<List<TdeeEntryDto>>("/api/tdee-entries");

        Assert.Equal(2, all!.Count);
        Assert.Equal(2450, all[0].Kcal);                        // newest first
        Assert.Equal(new DateOnly(2026, 6, 1), all[0].EffectiveFrom);
        Assert.Equal(2300, all[1].Kcal);
        Assert.Equal(2450, (await client.GetFromJsonAsync<UserSettingsDto>("/api/settings"))!.TdeeKcal);
    }

    [Fact]
    public async Task Post_RequiresToday_AndValidatesKcal()
    {
        var client = await ResetAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tdee-entries", new { kcal = 2300 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/tdee-entries?today=2026-01-01", new { kcal = 5 })).StatusCode);
    }

    [Fact]
    public async Task PastDeficits_KeepTheTdeeThatWasInForce()
    {
        var client = await ResetAsync();
        await client.PostAsJsonAsync("/api/tdee-entries?today=2026-01-01", new { kcal = 2000 });
        await client.PutAsJsonAsync("/api/daily-logs/2026-02-01", new { date = "2026-02-01", caloriesKcal = 1800 });
        await client.PostAsJsonAsync("/api/tdee-entries?today=2026-03-01", new { kcal = 2500 });

        var month = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/stats/month/2026-02?today=2026-03-02");
        Assert.Equal(200, month.GetProperty("figures").GetProperty("deficitKcal").GetInt32());   // 2000 - 1800, not 2500 - 1800
    }
}

public class TdeeEntriesEndpointTests_Sqlite : TdeeEntriesEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public TdeeEntriesEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class TdeeEntriesEndpointTests_Postgres : TdeeEntriesEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public TdeeEntriesEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
