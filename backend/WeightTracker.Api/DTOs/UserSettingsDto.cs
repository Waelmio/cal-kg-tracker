using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record UserSettingsDto(decimal? HeightCm, string PreferredUnit, decimal? TdeeKcal);

public record UpdateUserSettingsDto(
    [Range(50, 300)] decimal? HeightCm,
    [RegularExpression("^(kg|lbs)$")] string PreferredUnit,
    [Range(1, 15000)] decimal? TdeeKcal);
