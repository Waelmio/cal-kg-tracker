namespace WeightTracker.Api.Stats;

public record GoalStatusDto(
    int Id,
    string Kind,
    string State,            // none | active | reached | datePassed
    string? Pace,            // ahead | onTrack | behind (active reach goals with enough weigh-ins)
    bool Losing,
    decimal TargetWeightKg,
    decimal? StartingWeightKg,
    string StartDate,
    string? TargetDate,
    string? EndedOn,
    string? EndReason,
    double? CurrentKg,
    double? ProgressPercent,
    double? RemainingKg,
    double? NeededKgPerWeek,
    double? PaceKgPerWeek,
    string? ProjectedDate,
    int? LateWeeks,
    string? ReachedOn,       // first day the 7-day average was at or past the target
    double? RemainingAtTargetDateKg,
    double MaintainBandKg,
    bool? WithinBand);       // maintenance goals only
