using WeightTracker.Api.DTOs;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.Services;

/// <summary>
/// The rules that bring data from before the redesign (exports without a version) up to the current model.
/// The database migrations apply the same rules in SQL; keep both in step.
/// Nothing stored is ever cleared or deleted by an upgrade.
/// </summary>
public static class LegacyUpgrade
{
    public static DateOnly DateOf(DateTimeOffset t) => DateOnly.FromDateTime(t.UtcDateTime);
}
