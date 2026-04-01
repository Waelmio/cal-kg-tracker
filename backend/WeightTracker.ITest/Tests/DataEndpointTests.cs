using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class DataEndpointTestsBase
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
    public async Task Export_ReturnsEmptyCollections_WhenNoData()
    {
        await ResetAsync();

        var export = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");

        Assert.NotNull(export);
        Assert.Empty(export.DailyLogs);
        Assert.Empty(export.Goals);
        Assert.Empty(export.CalorieGoals);
        Assert.NotNull(export.Settings);
        Assert.Equal("kg", export.Settings.PreferredUnit);
    }

    [Fact]
    public async Task Import_ThenExport_RoundTrip()
    {
        await ResetAsync();

        // Seed some data directly via seed builder
        await SeedAsync(b => b
            .WithDailyLog("2024-03-10", weightKg: 80m, calories: 2000)
            .WithCalorieGoal(1800)
            .WithWeightGoal(70m, new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1)));

        // Export
        var export = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");
        Assert.NotNull(export);
        Assert.Single(export.DailyLogs);
        Assert.Single(export.CalorieGoals);
        Assert.Single(export.Goals);

        // Reset and re-import
        await ResetAsync();
        var importResponse = await Client.PostAsJsonAsync("/api/data/import", export);
        Assert.Equal(HttpStatusCode.NoContent, importResponse.StatusCode);

        // Export again and compare
        var exportAfterImport = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");
        Assert.NotNull(exportAfterImport);
        Assert.Single(exportAfterImport.DailyLogs);
        Assert.Equal("2024-03-10", exportAfterImport.DailyLogs[0].Date);
        Assert.Equal(80m, exportAfterImport.DailyLogs[0].WeightKg);
        Assert.Equal(2000, exportAfterImport.DailyLogs[0].CaloriesKcal);
        Assert.Single(exportAfterImport.CalorieGoals);
        Assert.Equal(1800, exportAfterImport.CalorieGoals[0].TargetCalories);
        Assert.Single(exportAfterImport.Goals);
        Assert.Equal(70m, exportAfterImport.Goals[0].TargetWeightKg);
    }

    [Fact]
    public async Task Import_ReplacesExistingData()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-01-01", weightKg: 90m));

        var firstExport = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");
        Assert.Single(firstExport!.DailyLogs);

        // Seed different data and export
        await ResetAsync();
        await SeedAsync(b => b
            .WithDailyLog("2024-06-01", weightKg: 75m)
            .WithDailyLog("2024-06-02", weightKg: 74.5m));
        var secondExport = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");

        // Import the first export (should replace second)
        await Client.PostAsJsonAsync("/api/data/import", firstExport);
        var afterImport = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");

        Assert.NotNull(afterImport);
        Assert.Single(afterImport.DailyLogs);
        Assert.Equal("2024-01-01", afterImport.DailyLogs[0].Date);
    }

    [Fact]
    public async Task DateTimeOffset_SurvivesRoundTrip_OnBothProviders()
    {
        await ResetAsync();
        var fixedTime = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
        await SeedAsync(b => b.WithCalorieGoal(1800, fixedTime));

        // Export → reset → import → export
        var export = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");
        await ResetAsync();
        await Client.PostAsJsonAsync("/api/data/import", export!);
        var afterImport = await Client.GetFromJsonAsync<ExportImportDto>("/api/data/export");

        Assert.NotNull(afterImport);
        Assert.Single(afterImport.CalorieGoals);
        var roundTripped = afterImport.CalorieGoals[0].CreatedAt;
        Assert.True(Math.Abs((roundTripped - fixedTime).TotalSeconds) < 1,
            $"Expected ~{fixedTime:O} but got {roundTripped:O}");
    }
}

public class DataEndpointTests_Sqlite : DataEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public DataEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class DataEndpointTests_Postgres : DataEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public DataEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
