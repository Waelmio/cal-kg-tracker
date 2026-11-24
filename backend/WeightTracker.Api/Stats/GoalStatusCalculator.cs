using WeightTracker.Api.Models;

namespace WeightTracker.Api.Stats;

/// <summary>Weight goal states (PLAN.md 3.8). Pure: the caller decides which goal is current.</summary>
public static class GoalStatusCalculator
{
    /// <summary>Within this many days of the target date the pace counts as on track.</summary>
    private const int OnTrackDays = 7;

    /// <summary>A maintenance goal keeps the 7-day average within this distance of its target.</summary>
    public const double MaintainBandKg = 1;

    public static GoalStatusDto Compute(Goal goal, StatsCalculator calc)
    {
        var today = calc.Today;
        var average = calc.LatestAverage7(today);       // "reached" is about the 7-day average, not one weigh-in
        var current = calc.Weight()?.Kg;
        var start = (double?)goal.StartingWeightKg;
        var target = (double)goal.TargetWeightKg;
        var pace = calc.Pace().KgPerDay;                // kg per day, negative while losing
        var losing = (start ?? current ?? target) > target;

        bool Crossed(double kg) => losing ? kg <= target : kg >= target;

        var state =
            goal.EndedOn is not null ? "none"           // ended goals are history; the caller normally never asks for them
            : goal.Kind == GoalKind.Maintain ? "active"
            : average is { } a && Crossed(a) ? "reached"
            : goal.TargetDate < today ? "datePassed"
            : "active";

        double? remaining = null, progress = null, needed = null, remainingAtDate = null;
        DateOnly? projected = null, reachedOn = null;
        int? lateWeeks = null;
        string? paceState = null;

        if (state != "none" && current is { } now)
        {
            remaining = Math.Max(0, losing ? now - target : target - now);
            if (start is { } s && s != target) progress = Math.Clamp((s - now) / (s - target) * 100, 0, 100);
        }

        if (state == "reached")
            reachedOn = StatsCalculator.Range(goal.StartDate, today).Cast<DateOnly?>()
                .FirstOrDefault(d => calc.Average7(d!.Value) is { } avg && Crossed(avg));

        if (state == "datePassed" && calc.LatestAverage7(goal.TargetDate!.Value) is { } then)
            remainingAtDate = Math.Max(0, losing ? then - target : target - then);

        if (state is "active" or "datePassed" && goal.Kind == GoalKind.Reach && goal.TargetDate is { } date && remaining is { } left)
        {
            if (date > today) needed = left / ((date.DayNumber - today.DayNumber) / 7.0);

            if (pace is { } p)
            {
                var rightWay = losing ? p < 0 : p > 0;
                if (rightWay)
                {
                    // A very slow pace would land centuries away: no date then.
                    var days = Math.Ceiling(left / Math.Abs(p));
                    if (days <= 3650) projected = today.AddDays((int)days);
                }

                if (projected is { } end)
                {
                    var lateDays = end.DayNumber - date.DayNumber;
                    lateWeeks = StatsCalculator.Rnd(lateDays / 7.0);
                    if (state == "active")
                        paceState = lateDays < -OnTrackDays ? "ahead" : lateDays <= OnTrackDays ? "onTrack" : "behind";
                }
                else if (state == "active" && left > 0)
                {
                    paceState = "behind";   // moving away from the goal, or hardly at all
                }
            }
            // No pace: not enough weigh-ins to say anything, which is not the same as being behind.
        }

        bool? withinBand = goal.Kind == GoalKind.Maintain && current is { } kg ? Math.Abs(kg - target) <= MaintainBandKg : null;

        return new GoalStatusDto(
            goal.Id, goal.Kind.ToString(), state, paceState, losing,
            goal.TargetWeightKg, goal.StartingWeightKg,
            Fmt(goal.StartDate)!, Fmt(goal.TargetDate), Fmt(goal.EndedOn), goal.EndReason?.ToString(),
            current, progress, remaining, needed, pace * 7, Fmt(projected), lateWeeks,
            Fmt(reachedOn), remainingAtDate, MaintainBandKg, withinBand);
    }

    private static string? Fmt(DateOnly? d) => d?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
