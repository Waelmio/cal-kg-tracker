using WeightTracker.Api.Models;

namespace WeightTracker.Api.Stats;

/// <summary>Weight goal states (PLAN.md 3.8). Pure: the caller decides which goal is current.</summary>
public static class GoalStatusCalculator
{
    /// <summary>Within this many days of the target date the pace counts as on track.</summary>
    private const int OnTrackDays = 7;

    public static GoalStatusDto Compute(Goal goal, StatsCalculator calc)
    {
        var today = calc.Today;
        var weight = calc.Weight();
        var current = calc.Average7(today) ?? weight?.Kg;
        var start = (double?)goal.StartingWeightKg;
        var target = (double)goal.TargetWeightKg;
        var pace = calc.Pace().KgPerDay;      // kg per day, negative while losing

        string state;
        string? paceState = null;
        double? progress = null, remaining = null, needed = null, projectedPaceWeek = pace is null ? null : pace * 7;
        DateOnly? projected = null;
        int? lateWeeks = null;

        var losing = start is null ? (current ?? target) > target : start > target;

        if (goal.EndedOn is not null)
        {
            state = "none";    // ended goals are history; the caller normally never asks for them
        }
        else
        {
            var reached = current is { } c && (losing ? c <= target : c >= target);
            if (goal.Kind == GoalKind.Maintain)
            {
                state = "active";
            }
            else if (reached)
            {
                state = "reached";
            }
            else if (goal.TargetDate is { } td && td < today)
            {
                state = "datePassed";
            }
            else
            {
                state = "active";
            }

            if (current is { } now)
            {
                remaining = losing ? now - target : target - now;
                if (remaining < 0) remaining = 0;
                if (start is { } s && s != target)
                    progress = Math.Clamp((s - now) / (s - target) * 100, 0, 100);
            }

            if (goal.Kind == GoalKind.Reach && goal.TargetDate is { } date && state != "reached")
            {
                if (remaining is { } rem && date > today)
                    needed = rem / ((date.DayNumber - today.DayNumber) / 7.0);

                var rightWay = pace is { } p && (losing ? p < 0 : p > 0);
                if (rightWay && remaining is { } r)
                {
                    projected = today.AddDays((int)Math.Ceiling(r / Math.Abs(pace!.Value)));
                    var lateDays = projected.Value.DayNumber - date.DayNumber;
                    lateWeeks = StatsCalculator.Rnd(lateDays / 7.0);
                    if (state == "active")
                        paceState = lateDays <= -OnTrackDays ? "ahead" : lateDays <= OnTrackDays ? "onTrack" : "behind";
                }
                else if (state == "active" && remaining is > 0)
                {
                    paceState = "behind";
                }
            }
        }

        return new GoalStatusDto(
            goal.Id, goal.Kind.ToString(), state, paceState,
            goal.TargetWeightKg, goal.StartingWeightKg,
            goal.StartDate.ToString("yyyy-MM-dd"), goal.TargetDate?.ToString("yyyy-MM-dd"),
            goal.EndedOn?.ToString("yyyy-MM-dd"), goal.EndReason?.ToString(),
            current, progress, remaining, needed, projectedPaceWeek, projected?.ToString("yyyy-MM-dd"), lateWeeks);
    }
}
