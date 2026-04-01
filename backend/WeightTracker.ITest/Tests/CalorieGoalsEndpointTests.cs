using Microsoft.AspNetCore.Mvc.Testing;

namespace WeightTracker.ITest.Tests;

public abstract class CalorieGoalsEndpointTestsBase
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

        var response = await Client.GetAsync("/api/calorie-goals/active");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsEmptyArray_WhenNoGoals()
    {
        await ResetAsync();

        var goals = await Client.GetFromJsonAsync<List<CalorieGoalDto>>("/api/calorie-goals");

        Assert.NotNull(goals);
        Assert.Empty(goals);
    }

    [Fact]
    public async Task Post_CreatesGoal_AndReturns201()
    {
        await ResetAsync();

        var response = await Client.PostAsJsonAsync("/api/calorie-goals", new { TargetCalories = 1800 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var goal = await response.Content.ReadFromJsonAsync<CalorieGoalDto>();
        Assert.NotNull(goal);
        Assert.Equal(1800, goal.TargetCalories);
    }

    [Fact]
    public async Task GetActive_ReturnsNewest_WhenMultipleGoalsExist()
    {
        await ResetAsync();
        await SeedAsync(b => b
            .WithCalorieGoal(1600, DateTimeOffset.UtcNow.AddDays(-10))
            .WithCalorieGoal(2000, DateTimeOffset.UtcNow));

        var goal = await Client.GetFromJsonAsync<CalorieGoalDto>("/api/calorie-goals/active");

        Assert.Equal(2000, goal!.TargetCalories);
    }

    [Fact]
    public async Task Delete_Returns204_WhenGoalExists()
    {
        await ResetAsync();
        var createResponse = await Client.PostAsJsonAsync("/api/calorie-goals", new { TargetCalories = 1800 });
        var goal = await createResponse.Content.ReadFromJsonAsync<CalorieGoalDto>();

        var deleteResponse = await Client.DeleteAsync($"/api/calorie-goals/{goal!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_WhenGoalNotFound()
    {
        await ResetAsync();

        var response = await Client.DeleteAsync("/api/calorie-goals/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreatedAt_IsPreserved_AfterRoundTrip()
    {
        await ResetAsync();
        var fixedTime = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero);
        await SeedAsync(b => b.WithCalorieGoal(1800, fixedTime));

        var goal = await Client.GetFromJsonAsync<CalorieGoalDto>("/api/calorie-goals/active");

        Assert.NotNull(goal);
        // Allow 1 second tolerance for provider-specific precision differences
        Assert.True(Math.Abs((goal.CreatedAt - fixedTime).TotalSeconds) < 1);
    }
}

public class CalorieGoalsEndpointTests_Sqlite : CalorieGoalsEndpointTestsBase, IClassFixture<SqliteWebAppFactory>
{
    private readonly SqliteWebAppFactory _factory;
    public CalorieGoalsEndpointTests_Sqlite(SqliteWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}

public class CalorieGoalsEndpointTests_Postgres : CalorieGoalsEndpointTestsBase, IClassFixture<PostgresWebAppFactory>
{
    private readonly PostgresWebAppFactory _factory;
    public CalorieGoalsEndpointTests_Postgres(PostgresWebAppFactory factory) => _factory = factory;
    protected override WebApplicationFactory<Program> Factory => _factory;
}
