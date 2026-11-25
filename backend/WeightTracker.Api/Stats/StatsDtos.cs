namespace WeightTracker.Api.Stats;

/// <summary>Statuses are serialized lowercase: under (green), balanced (yellow), over (red).</summary>
public record PeriodDto(
    string From,
    string To,
    int LoggedDays,
    int? AvgCalories,
    int? AvgTarget,
    int Gap,
    int DeficitKcal,
    double DeficitKg,
    double? AvgWeightKg,
    double? WeightChangeKg,
    int WeighIns,
    string? Status,
    bool InGoal);

public record PlannedDayDto(string Date, int Kcal, bool UserLogged);

public record BudgetDto(
    int TodayBudget,
    int PerDay,              // of the days to come, today included until it is logged
    int? EatenToday,
    int? TodayLeft,
    int? AverageSoFar,
    int? AvgTargetSoFar,
    int ExpectedAverage,
    int? ExpectedDeficitKcal,
    bool ReachesTarget,      // false when even the plan leaves the averaging window over target
    List<PlannedDayDto> Days,
    List<PlannedDayDto> FutureDays);

public record StreakRowDto(int Days, int? AvgCalories, int? AvgGap);

public record StreakDto(int Days, int? NextGap, List<StreakRowDto> Rows);

public record WeeklyDeficitDto(int Kcal, double Kg, int? ExpectedKcal, double? ExpectedKg, int LoggedDays);

public record OverallDeficitDto(int Kcal, double Kg, int GapKcal, int LoggedDays, int TotalDays, string Since, bool SinceGoalStart);

public record DeficitsDto(WeeklyDeficitDto Weekly, OverallDeficitDto Overall);

public record WeightDto(
    double? Kg,
    bool IsAverage,
    string? Date,
    double? PaceKgPerWeek,
    int PaceWeighIns,
    double? Bmi,
    double? BmiAtGoal);

public record DashboardStatsDto(
    string Today,
    string? FirstLog,        // the oldest day with anything logged
    int AveragingWeeks,
    int? CalorieTarget,
    int? Tdee,
    BudgetDto? Budget,
    PeriodDto Week,
    StreakDto Streak,
    DeficitsDto Deficits,
    WeightDto Weight,
    GoalStatusDto? Goal,
    List<WeightPointDto> WeightSeries,
    TargetComparisonDto TargetComparison);

/// <summary>
/// What the calorie target, the last four full weeks and the goal's schedule each mean against the TDEE in force.
/// A deficit becomes a pace the same way as everywhere else: kcal * 7 / 7700 kg a week.
/// </summary>
public record TargetComparisonDto(
    int? Target, int? Tdee,
    int? TargetDeficit, double? TargetKgPerWeek,
    int? Last4Avg, int? Last4Deficit, double? Last4KgPerWeek,
    int? OnTimeKcal);

/// <summary>A weigh-in with the 7-day average ending that day. Starts at the goal's start date (else 90 days back).</summary>
public record WeightPointDto(string Date, double Kg, double Avg7);

public record BudgetByWindowDto(int Weeks, int? PerDay);

public record CalendarDayDto(
    string Date,
    int? Calories,
    int? Target,
    string? Status,
    int? Planned,
    bool PlannedLogged,
    bool InGoal,
    double? WeightKg,
    bool IsCheatDay,
    string? Notes);

/// <summary>One day as the log sheet edits it. Everything the sheet shows for a week comes in one response.</summary>
/// <param name="CalorieTarget">The calorie target in force, whether or not the day is a cheat day.</param>
/// <param name="LastWeightKg">The latest weigh-in before this day, however long ago.</param>
public record LogDayDto(
    string Date,
    int? Calories,
    double? WeightKg,
    string? Notes,
    bool IsCheatDay,
    string? Status,
    int? Planned,
    int? CalorieTarget,
    int? Tdee,
    double? LastWeightKg);

/// <summary>What today's free days share, so the sheet can show the effect of a value while it is typed.</summary>
/// <param name="Pool">Kcal left for today and the other free days of this week. Not rounded, like the budget: the shares are.</param>
public record WeekPoolDto(double TodayBudget, double Pool, int OtherDays);

public record LogWeekDto(string Monday, List<LogDayDto> Days, WeekPoolDto? ThisWeek);

public record CalendarWeekDto(string Monday, int IsoWeek, int IsoYear, PeriodDto Figures);

public record CalendarMonthDto(string Month, PeriodDto Figures);

/// <param name="TodayDay">Today, also when it is outside the range.</param>
/// <param name="Months">Every month the range touches, with the figures of the whole month.</param>
public record CalendarDto(
    string Today, CalendarDayDto TodayDay, List<CalendarDayDto> Days, List<CalendarWeekDto> Weeks, List<CalendarMonthDto> Months);

public record MonthSummaryDto(
    string Month,
    PeriodDto Figures,
    int DaysInMonth,
    int CheatDays,
    double? PreviousAvgWeightKg,
    double? StartWeightKg,
    double? EndWeightKg,
    List<CalendarWeekDto> Weeks,
    TdeeEstimateDto? Tdee);
