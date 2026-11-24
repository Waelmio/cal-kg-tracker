using WeightTracker.Api.Models;
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
        var response = await Client.PostAsJsonAsync("/api/goals?today=2024-06-01", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();
        Assert.NotNull(goal);
        Assert.Equal(75.0m, goal.TargetWeightKg);
    }

    [Fact]
    public async Task Post_SetsStartingWeightKg_FromLatestWeightLog()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-05-30", weightKg: 82.0m));

        var body = new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null };
        var response = await Client.PostAsJsonAsync("/api/goals?today=2024-06-01", body);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();

        Assert.Equal(82.0m, goal!.StartingWeightKg);
    }

    [Fact]
    public async Task Post_StartingWeightKgIsNull_WhenNoWeightLogs()
    {
        await ResetAsync();

        var body = new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null };
        var response = await Client.PostAsJsonAsync("/api/goals?today=2024-06-01", body);
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
        var createResponse = await Client.PostAsJsonAsync("/api/goals?today=2024-06-01",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null });
        var goal = await createResponse.Content.ReadFromJsonAsync<GoalDto>();

        var deleteResponse = await Client.DeleteAsync($"/api/goals/{goal!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task End_MakesTheGoalHistory_AndNothingBecomesActive()
    {
        await ResetAsync();
        var created = await (await Client.PostAsJsonAsync("/api/goals?today=2024-06-01",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null })).Content.ReadFromJsonAsync<GoalDto>();

        var end = await Client.PostAsJsonAsync($"/api/goals/{created!.Id}/end?today=2024-07-01", new { Reason = "Reached" });

        Assert.Equal(HttpStatusCode.OK, end.StatusCode);
        var ended = await end.Content.ReadFromJsonAsync<GoalDto>();
        Assert.Equal(GoalEndReason.Reached, ended!.EndReason);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("/api/goals/active")).StatusCode);
        Assert.Single(await Client.GetFromJsonAsync<List<GoalDto>>("/api/goals") ?? []);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PostAsJsonAsync($"/api/goals/{created.Id}/end?today=2024-07-02", new { Reason = "Ended" })).StatusCode);
    }

    [Fact]
    public async Task Post_ReplacesTheActiveGoal_AndPut_EditsInPlace()
    {
        await ResetAsync();
        var first = await (await Client.PostAsJsonAsync("/api/goals?today=2024-06-01",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null })).Content.ReadFromJsonAsync<GoalDto>();
        var second = await (await Client.PostAsJsonAsync("/api/goals?today=2024-07-01",
            new { TargetWeightKg = 72.0m, TargetDate = new DateOnly(2025, 3, 1), StartDate = (DateOnly?)null, Notes = (string?)null })).Content.ReadFromJsonAsync<GoalDto>();

        var all = await Client.GetFromJsonAsync<List<GoalDto>>("/api/goals");
        Assert.Equal(GoalEndReason.Replaced, all!.Single(g => g.Id == first!.Id).EndReason);

        var put = await Client.PutAsJsonAsync($"/api/goals/{second!.Id}",
            new { TargetWeightKg = 71.0m, TargetDate = new DateOnly(2025, 4, 1), StartDate = new DateOnly(2024, 7, 1), StartingWeightKg = 80.0m, Notes = "edited" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(71.0m, (await put.Content.ReadFromJsonAsync<GoalDto>())!.TargetWeightKg);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.PutAsJsonAsync($"/api/goals/{first!.Id}",
            new { TargetWeightKg = 71.0m, TargetDate = new DateOnly(2025, 4, 1), StartDate = new DateOnly(2024, 7, 1), StartingWeightKg = 80.0m, Notes = (string?)null })).StatusCode);
    }

    [Fact]
    public async Task Post_Returns400WithReadableTitle_WhenTargetDateIsNotAfterStart()
    {
        await ResetAsync();
        var response = await Client.PostAsJsonAsync("/api/goals?today=2024-06-01",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2024, 6, 1), StartDate = (DateOnly?)null, Notes = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("target date", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Maintain_EndsTheGoal_AndSetsTheCalorieTarget()
    {
        await ResetAsync();
        await SeedAsync(b => b.WithDailyLog("2024-05-30", weightKg: 80.0m));
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TdeeEntries.Add(new WeightTracker.Api.Models.TdeeEntry { Kcal = 2300, EffectiveFrom = new DateOnly(2024, 1, 1) });
            await db.SaveChangesAsync();
        }
        await Client.PostAsJsonAsync("/api/goals?today=2024-06-01",
            new { TargetWeightKg = 75.0m, TargetDate = new DateOnly(2025, 1, 1), StartDate = (DateOnly?)null, Notes = (string?)null });

        var response = await Client.PostAsync("/api/goals/maintain?today=2024-06-02", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var goal = await response.Content.ReadFromJsonAsync<GoalDto>();
        Assert.Equal(GoalKind.Maintain, goal!.Kind);
        var active = await Client.GetFromJsonAsync<GoalDto>("/api/goals/active");
        Assert.Equal(goal.Id, active!.Id);
        var target = await Client.GetFromJsonAsync<CalorieGoalDto>("/api/calorie-goals/active");
        Assert.Equal(2300, target!.TargetCalories);
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
