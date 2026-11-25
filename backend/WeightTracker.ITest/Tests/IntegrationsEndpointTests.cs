using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Api.Integrations;
using WeightTracker.Api.Integrations.FatSecret;
using WeightTracker.Api.Models;

namespace WeightTracker.ITest.Tests;

public abstract class IntegrationsEndpointTestsBase : IAsyncLifetime
{
    private const string Today = "today=2026-11-24";

    protected abstract WebApplicationFactory<Program> BaseFactory { get; }

    private readonly FakeFatSecretHandler _fatSecret = new();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private WebApplicationFactory<Program> Configured(Action<IWebHostBuilder>? more = null) => BaseFactory.WithWebHostBuilder(b =>
    {
        b.UseSetting("FATSECRET_CONSUMER_KEY", "plain-consumer-key");
        b.UseSetting("FATSECRET_CONSUMER_SECRET", "plain-consumer-secret");
        b.ConfigureTestServices(s => s.AddHttpClient<FatSecretClient>().ConfigurePrimaryHttpMessageHandler(() => _fatSecret));
        more?.Invoke(b);
    });

    private static HttpClient NoRedirects(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task InitializeAsync()
    {
        _factory = Configured();
        _client = NoRedirects(_factory);
        using var scope = _factory.Services.CreateScope();
        await DbFixture.ResetDatabaseAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private async Task<T> Db<T>(Func<AppDbContext, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task ConnectAsync()
    {
        (await _client.PostAsJsonAsync("/api/integrations/fatsecret/connect", new { })).EnsureSuccessStatusCode();
        var back = await _client.GetAsync("/api/integrations/fatsecret/callback?oauth_token=request-token&oauth_verifier=1234");
        Assert.EndsWith("/settings?integration=fatsecret&result=connected", back.Headers.Location!.ToString());
    }

    private async Task<JsonElement> Status() => (await _client.GetFromJsonAsync<JsonElement>("/api/integrations"))[0];
    private static async Task<string> Title(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString()!;

    [Fact]
    public async Task WithoutKeys_ItSaysWhichToSet()
    {
        var client = BaseFactory.CreateClient();
        var status = (await client.GetFromJsonAsync<JsonElement>("/api/integrations"))[0];
        Assert.False(status.GetProperty("configured").GetBoolean());
        Assert.Equal("FATSECRET_CONSUMER_KEY", status.GetProperty("requiredSettings")[0].GetString());

        var response = await client.PostAsJsonAsync("/api/integrations/fatsecret/connect", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FATSECRET_CONSUMER_KEY", await Title(response));
    }

    [Fact]
    public async Task UnknownProvider_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/integrations/other/connect", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/integrations/other/sync?{Today}", null)).StatusCode);
    }

    [Fact]
    public async Task Connect_SendsTheBrowserBackToTheAddressItCameFrom()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/integrations/fatsecret/connect") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Origin", "http://192.168.1.20:8080");
        var connect = await (await _client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("https://authentication.fatsecret.com/oauth/authorize?oauth_token=request-token", connect.GetProperty("authorizeUrl").GetString());
        Assert.Equal("http://192.168.1.20:8080/api/integrations/fatsecret/callback", _fatSecret.Callback);

        var back = await _client.GetAsync("/api/integrations/fatsecret/callback?oauth_token=request-token&oauth_verifier=1234");
        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.Equal("http://192.168.1.20:8080/settings?integration=fatsecret&result=connected", back.Headers.Location!.ToString());
        Assert.Equal("Connected", (await Status()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task PublicBaseUrl_Wins()
    {
        await using var factory = Configured(b => b.UseSetting("PUBLIC_BASE_URL", "https://weight.example.org/"));
        (await factory.CreateClient().PostAsJsonAsync("/api/integrations/fatsecret/connect", new { })).EnsureSuccessStatusCode();
        Assert.Equal("https://weight.example.org/api/integrations/fatsecret/callback", _fatSecret.Callback);
    }

    [Fact]
    public async Task Callback_Unknown_GoesBackToSettings()
    {
        var back = await _client.GetAsync("/api/integrations/fatsecret/callback?oauth_token=x&oauth_verifier=1234");
        Assert.Equal("/settings?integration=fatsecret&result=expired", back.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Pin_Connects()
    {
        (await _client.PostAsJsonAsync("/api/integrations/fatsecret/connect", new { usePin = true })).EnsureSuccessStatusCode();
        Assert.Equal("oob", _fatSecret.Callback);
        Assert.Equal(HttpStatusCode.BadGateway, (await _client.PostAsJsonAsync("/api/integrations/fatsecret/verify", new { verifier = "9" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync("/api/integrations/fatsecret/verify", new { verifier = "1234" })).StatusCode);
        Assert.Equal("Connected", (await Status()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sync_Imports_AndNothingEverShowsASecret()
    {
        await ConnectAsync();
        _fatSecret.Calories[new(2026, 11, 20)] = 1850;
        _fatSecret.Calories[new(2026, 12, 13)] = 900;        // two weeks ahead
        _fatSecret.Calories[new(2026, 12, 14)] = 900;        // after the window
        _fatSecret.Calories[new(2026, 11, 1)] = 900;         // before the window
        _fatSecret.Weights[new(2026, 11, 20)] = 82.345m;

        var response = await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<SyncResultDto>())!;
        Assert.Equal((2, 1), (result.Calories.Written, result.Weight.Written));

        var all = await Db(db => db.DailyLogs.OrderBy(l => l.Date).ToListAsync());
        Assert.Equal([new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 13)], all.Select(l => l.Date));
        var log = all[0];
        Assert.Equal((1850, 82.35m, LogSource.FatSecret), (log.CaloriesKcal, log.WeightKg, log.CaloriesSource));

        var status = await Status();
        Assert.Equal("Succeeded", status.GetProperty("lastSyncStatus").GetString());
        Assert.Equal("Powered by fatsecret", status.GetProperty("attribution").GetProperty("text").GetString());

        // As stored, and as shown by everything that could show it.
        var connection = await Db(db => db.IntegrationConnections.SingleAsync());
        var shown = status.ToString()
            + await _client.GetStringAsync("/api/integrations") + await _client.GetStringAsync("/api/settings") + await _client.GetStringAsync("/api/data/export")
            + await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("plain-", shown);
        Assert.DoesNotContain(connection.AccessToken, shown);
        Assert.DoesNotContain("plain-", connection.AccessToken + connection.AccessTokenSecret);
    }

    [Fact]
    public async Task Sync_StillWorksAfterARestart()
    {
        await ConnectAsync();
        _fatSecret.Calories[new(2026, 11, 20)] = 1850;
        await using var restarted = Configured();
        Assert.Equal(HttpStatusCode.OK, (await restarted.CreateClient().PostAsync($"/api/integrations/fatsecret/sync?{Today}", null)).StatusCode);
    }

    [Fact]
    public async Task Sync_NeedsTodayAndAConnection()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/integrations/fatsecret/sync", null)).StatusCode);
        var response = await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("FatSecret is not connected.", await Title(response));
    }

    [Fact]
    public async Task Sync_ErrorsAreReadable_AndARevokedTokenNeedsReconnect()
    {
        await ConnectAsync();
        _fatSecret.Error = """{"error":{"code":21,"message":"Invalid IP address detected: '1.2.3.4'"}}""";
        var response = await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("1.2.3.4", await Title(response));
        Assert.Equal("Connected", (await Status()).GetProperty("status").GetString());

        _fatSecret.Error = """{"error":{"code":9,"message":"Invalid access token"}}""";
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null)).StatusCode);
        Assert.Equal("NeedsReconnect", (await Status()).GetProperty("status").GetString());

        _fatSecret.Error = null;
        await ConnectAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null)).StatusCode);
    }

    [Fact]
    public async Task Preview_ThenApply_WritesOnlyWhatWasChosen()
    {
        await ConnectAsync();
        (await _client.PutAsJsonAsync("/api/daily-logs/2026-11-19?today=2026-11-19", new { date = "2026-11-19", caloriesKcal = 2000, notes = "mine" })).EnsureSuccessStatusCode();
        (await _client.PutAsJsonAsync("/api/daily-logs/2026-11-20?today=2026-11-20", new { date = "2026-11-20", caloriesKcal = 2100, weightKg = 82.34 })).EnsureSuccessStatusCode();
        _fatSecret.Calories[new(2026, 11, 19)] = 1900;
        _fatSecret.Calories[new(2026, 11, 20)] = 1950;
        _fatSecret.Weights[new(2026, 11, 20)] = 82.3m;

        var preview = (await (await _client.PostAsJsonAsync($"/api/integrations/fatsecret/sync/preview?{Today}", new { from = "2026-11-01", to = "2026-11-30" }))
            .Content.ReadFromJsonAsync<PreviewDto>())!;
        Assert.Equal((2, 1, 0, "2026-11-24"), (preview.Rows.Count, preview.EqualCount, preview.MissingCount, preview.To.ToString("yyyy-MM-dd")));
        Assert.All(preview.Rows, r => Assert.Equal((SyncRowKind.Conflict, SyncChoice.Keep), (r.Kind, r.Suggested)));
        Assert.Equal(2000, (await Db(db => db.DailyLogs.SingleAsync(l => l.Date == new DateOnly(2026, 11, 19)))).CaloriesKcal);

        var body = new { previewId = preview.PreviewId, decisions = new[] { new { date = "2026-11-19", field = "Calories", choice = "Replace" } } };
        var result = await (await _client.PostAsJsonAsync($"/api/integrations/fatsecret/sync/apply?{Today}", body)).Content.ReadFromJsonAsync<ApplyResultDto>();
        Assert.Equal(new ApplyResultDto(0, 1, 1, 0), result);

        var logs = await Db(db => db.DailyLogs.OrderBy(l => l.Date).ToListAsync());
        Assert.Equal((1900, "mine"), (logs[0].CaloriesKcal, logs[0].Notes));
        Assert.Equal((2100, 82.34m), (logs[1].CaloriesKcal, logs[1].WeightKg));

        var again = await _client.PostAsJsonAsync($"/api/integrations/fatsecret/sync/apply?{Today}", body);
        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
    }

    [Fact]
    public async Task Preview_OfTooLongAPeriod_Is400()
    {
        await ConnectAsync();
        var response = await _client.PostAsJsonAsync($"/api/integrations/fatsecret/sync/preview?{Today}", new { from = "2025-01-01", to = "2026-11-01" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("12 months", await Title(response));
    }

    [Fact]
    public async Task Disconnect_ForgetsTheConnection_AndKeepsTheLogs()
    {
        await ConnectAsync();
        _fatSecret.Calories[new(2026, 11, 20)] = 1850;
        await _client.PostAsync($"/api/integrations/fatsecret/sync?{Today}", null);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/integrations/fatsecret")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/integrations/fatsecret")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await Status()).GetProperty("status").ValueKind);
        Assert.Equal(1850, (await Db(db => db.DailyLogs.SingleAsync())).CaloriesKcal);
        Assert.Empty(await Db(db => db.IntegrationConnections.ToListAsync()));
    }

    [Fact]
    public async Task BackgroundRun_SyncsConnectedProviders_AndNeverThrows()
    {
        var service = _factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<IntegrationSyncBackgroundService>().Single();
        await service.RunAsync(default);                                     // nothing connected

        await ConnectAsync();
        var today = DateOnly.FromDateTime(DateTime.Now);
        _fatSecret.Calories[today.AddDays(-1)] = 1850;
        await service.RunAsync(default);
        Assert.Equal(1850, (await Db(db => db.DailyLogs.SingleAsync())).CaloriesKcal);

        _fatSecret.Error = """{"error":{"code":9,"message":"Invalid access token"}}""";
        await service.RunAsync(default);
        Assert.Equal("NeedsReconnect", (await Status()).GetProperty("status").GetString());
        await service.RunAsync(default);                                     // skipped from now on
    }

    [Fact]
    public async Task AfterARestart_TheNextSyncIsCountedFromTheLastOne()
    {
        // On, unlike in the other tests, but the test ends before its first sync.
        await using var factory = Configured(b => b.UseSetting("Integrations:SyncIntervalHours", "12"));
        var service = factory.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().OfType<IntegrationSyncBackgroundService>().Single();
        var soon = TimeSpan.FromSeconds(30);
        Assert.Equal(soon, await service.FirstDelayAsync(default));          // nothing connected

        await ConnectAsync();
        Assert.Equal(soon, await service.FirstDelayAsync(default));          // never synced

        async Task<TimeSpan> After(double hours, SyncStatus status)
        {
            await Db(async db =>
            {
                var c = await db.IntegrationConnections.SingleAsync();
                (c.LastSyncAt, c.LastSyncStatus) = (DateTimeOffset.UtcNow.AddHours(-hours), status);
                return await db.SaveChangesAsync();
            });
            return await service.FirstDelayAsync(default);
        }
        Assert.InRange(await After(2, SyncStatus.Succeeded), TimeSpan.FromHours(9.9), TimeSpan.FromHours(10));
        Assert.Equal(soon, await After(13, SyncStatus.Succeeded));
        Assert.Equal(soon, await After(2, SyncStatus.Failed));
        Assert.Equal(TimeSpan.FromHours(12), await After(-50, SyncStatus.Succeeded));
    }
}

public class IntegrationsEndpointTests_Sqlite(SqliteWebAppFactory factory) : IntegrationsEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    protected override WebApplicationFactory<Program> BaseFactory => factory;
}

public class IntegrationsEndpointTests_Postgres(PostgresWebAppFactory factory) : IntegrationsEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    protected override WebApplicationFactory<Program> BaseFactory => factory;
}
