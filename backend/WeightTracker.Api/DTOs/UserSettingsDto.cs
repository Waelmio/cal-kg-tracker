using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record UserSettingsDto(int? HeightCm, string PreferredUnit, int? TdeeKcal);

public record UpdateUserSettingsDto(
    [Range(50, 300)] int? HeightCm,
    [RegularExpression("^(kg|lbs)$")] string PreferredUnit,
    [Range(1, 15000)] int? TdeeKcal);
