using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class GoalsEndpointTestsBase
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
    public async Task GetActive_Returns404_WhenEmpty()
    {
        await ResetAsync();

        var response = await Client.GetAsync("/api/goals/active");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyArray_WhenNoGoals()
    {
        await ResetAsync();

        var goals = await Client.GetFromJsonAsync<List<GoalDto>>("/api/goals");

        Assert.NotNull(goals);
        Assert.Empty(goals);
    }

    [Fact]
    public async Task Post_CreatesGoal_AndReturns201()
    {
        await ResetAsync();

        var body = new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null };
        var response = await Client.PostAsJsonAsync("/api/goals", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();
        Assert.NotNull(goal);
        Assert.Equal(75.0m, goal.TargetWeightKg);
    }

    [Fact]
    public async Task Post_SetsStartingWeightKg_FromLatestWeightLog()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-01-10", weightKg: 82.0m));

        var body = new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null };
        var response = await Client.PostAsJsonAsync("/api/goals", body);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();

        Assert.Equal(82.0m, goal!.StartingWeightKg);
    }

    [Fact]
    public async Task Post_StartingWeightKgIsNull_WhenNoWeightLogs()
    {
        await ResetAsync();

        var body = new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null };
        var response = await Client.PostAsJsonAsync("/api/goals", body);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();

        Assert.Null(goal!.StartingWeightKg);
    }

    [Fact]
    public async Task GetActive_ReturnsNewest_WhenMultipleGoalsExist()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithWeightGoal(80m, new DateOnly(2025, 6, 1), new DateOnly(2024, 1, 1), createdAt: DateTimeOffset.UtcNow.AddDays(-10))
            .WithWeightGoal(70m, new DateOnly(2025, 12, 1), new DateOnly(2024, 1, 1), createdAt: DateTimeOffset.UtcNow));

        var goal = await Client.GetFromJsonAsync<GoalDto>("/api/goals/active");

        Assert.Equal(70m, goal!.TargetWeightKg);
    }

    [Fact]
    public async Task Delete_Returns204_WhenGoalExists()
    {
        await ResetAsync();
        var createResponse = await Client.PostAsJsonAsync("/api/goals",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null });
        var goal = await createResponse.Content.ReadFromJsonAsync<GoalDto>();

        var deleteResponse = await Client.DeleteAsync($"/api/goals/{goal!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenGoalNotFound()
    {
        await ResetAsync();

        var response = await Client.DeleteAsync("/api/goals/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public class GoalsEndpointTests_Sqlite : GoalsEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public GoalsEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class GoalsEndpointTests_Postgres : GoalsEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public GoalsEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
