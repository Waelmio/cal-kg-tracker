using System.ComponentModel.DataAnnotations;

namespace WeightTracker.Api.DTOs;

public record UserSettingsDto(int? HeightCm, string PreferredUnit, int AveragingWeeks = 2);

public record UpdateUserSettingsDto(
    [Range(50, 300)] int? HeightCm,
    [RegularExpression("^(kg|lbs)$")] string PreferredUnit,
    [Range(1, 5)] int? AveragingWeeks = null);
