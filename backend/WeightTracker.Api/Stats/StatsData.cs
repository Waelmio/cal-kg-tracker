namespace WeightTracker.Api.Stats;

public sealed record DayData(DateOnly Date, double? WeightKg, int? Calories, DateOnly? CaloriesSetOn, bool IsCheatDay, bool HasNote);

/// <summary>The goal's span on the calendar, used only for the "lighter shade" outside the goal.</summary>
public sealed record GoalSpan(DateOnly Start, DateOnly? End);

/// <summary>Everything the calculations need, in memory. The calculator never touches the database.</summary>
public sealed class StatsData
{
    public required DateOnly Today { get; init; }

    /// <summary>Weeks the target is averaged over (1 to 5).</summary>
    public int Window { get; init; } = 2;

    public required IReadOnlyDictionary<DateOnly, DayData> Days { get; init; }

    /// <summary>Calorie targets by the day they take effect, ascending. A null value turns the target off.</summary>
    public IReadOnlyList<(DateOnly From, int? Kcal)> Targets { get; init; } = [];

    /// <summary>TDEE values by the day they take effect, ascending.</summary>
    public IReadOnlyList<(DateOnly From, int Kcal)> Tdees { get; init; } = [];

    public GoalSpan? Goal { get; init; }

    public StatsData WithWindow(int window) => new()
    {
        Today = Today, Window = window, Days = Days, Targets = Targets, Tdees = Tdees, Goal = Goal,
    };
}
