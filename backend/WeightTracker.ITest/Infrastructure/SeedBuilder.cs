using WeightTracker.Api.Models;

namespace WeightTracker.ITest.Infrastructure;

public class SeedBuilder(AppDbContext db)
{
    public SeedBuilder WithDailyLog(
        string date,
        decimal? weightKg = null,
        int? calories = null,
        bool isCheatDay = false,
        string? notes = null,
        DateTimeOffset? createdAt = null)
    {
        db.DailyLogs.Add(new DailyLog
        {
            Date = DateOnly.Parse(date),
            WeightKg = weightKg,
            CaloriesKcal = calories,
            IsCheatDay = isCheatDay,
            Notes = notes,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        return this;
    }

    public SeedBuilder WithCalorieGoal(int targetCalories, DateTimeOffset? createdAt = null)
    {
        db.CalorieGoals.Add(new CalorieGoal
        {
            TargetCalories = targetCalories,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        });
        return this;
    }

    public SeedBuilder WithWeightGoal(
        decimal targetWeightKg,
        DateOnly targetDate,
        DateOnly startDate,
        decimal? startingWeightKg = null,
        DateTimeOffset? createdAt = null)
    {
        db.Goals.Add(new Goal
        {
            TargetWeightKg = targetWeightKg,
            TargetDate = targetDate,
            StartDate = startDate,
            StartingWeightKg = startingWeightKg,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        });
        return this;
    }

    public Task SaveAsync() => db.SaveChangesAsync();
}
