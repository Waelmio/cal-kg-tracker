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
    int? EatenToday,
    int? TodayLeft,
    int? AverageSoFar,
    int? AvgTargetSoFar,
    int ExpectedAverage,
    int? ExpectedDeficitKcal,
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

public record BudgetByWindowDto(int Weeks, int? TodayBudget);

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
    bool HasNote);

public record CalendarWeekDto(string Monday, int IsoWeek, int IsoYear, PeriodDto Figures);

public record CalendarDto(string Today, List<CalendarDayDto> Days, List<CalendarWeekDto> Weeks);

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
