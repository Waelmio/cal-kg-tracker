using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class SettingsEndpointTestsBase
{
    protected abstract WebApplicationFactory<Program> Factory { get; }

    private HttpClient Client => Factory.CreateClient();

    private async Task ResetAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DbFixture.ResetDatabaseAsync(db);
    }

    [Fact]
    public async Task Get_ReturnsSeededDefaults()
    {
        await ResetAsync();

        var settings = await Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");

        Assert.NotNull(settings);
        Assert.Equal("kg", settings.PreferredUnit);
        Assert.Null(settings.HeightCm);
        Assert.Null(settings.TdeeKcal);
    }

    [Fact]
    public async Task Put_UpdatesAllFields()
    {
        await ResetAsync();

        var body = new { HeightCm = 175, PreferredUnit = "lbs", TdeeKcal = 2200 };
        var response = await Client.PutAsJsonAsync("/api/settings", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = await response.Content.ReadFromJsonAsync<UserSettingsDto>();
        Assert.NotNull(settings);
        Assert.Equal(175, settings.HeightCm);
        Assert.Equal("lbs", settings.PreferredUnit);
        Assert.Equal(2200, settings.TdeeKcal);
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsUpdatedValues()
    {
        await ResetAsync();

        await Client.PutAsJsonAsync("/api/settings", new { HeightCm = 180, PreferredUnit = "kg", TdeeKcal = (int?)null });
        var settings = await Client.GetFromJsonAsync<UserSettingsDto>("/api/settings");

        Assert.NotNull(settings);
        Assert.Equal(180, settings.HeightCm);
        Assert.Equal("kg", settings.PreferredUnit);
    }

    [Fact]
    public async Task Put_InvalidUnit_ReturnsBadRequest()
    {
        await ResetAsync();

        var response = await Client.PutAsJsonAsync("/api/settings", new { HeightCm = (int?)null, PreferredUnit = "stones", TdeeKcal = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class SettingsEndpointTests_Sqlite : SettingsEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public SettingsEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class SettingsEndpointTests_Postgres : SettingsEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public SettingsEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
