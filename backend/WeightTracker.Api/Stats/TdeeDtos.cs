namespace WeightTracker.Api.Stats;

public record TdeeEstimateDto(
    int Kcal,
    int AvgCalories,
    double WeightTrendGramsPerDay,
    int Adjustment,
    int WeighIns,
    int LoggedDays,
    int SpanDays,
    string First,
    string Last,
    int PeriodDays);

public record DietRowDto(string Key, string Label, int Kcal);

public record DietEffectDto(double LostKg, List<DietRowDto> Rows, string Footer);

public record TdeeStatsDto(
    int Days,
    TdeeEstimateDto? Estimate,
    string? Missing,
    int WeighIns,
    int SpanDays,
    int LoggedDays,
    int? CurrentTdee,
    bool SuggestUpdate,
    string? Note,
    DietEffectDto? Diet);
