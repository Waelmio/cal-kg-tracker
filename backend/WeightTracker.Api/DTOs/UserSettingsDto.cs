namespace WeightTracker.Api.DTOs;

public record UserSettingsDto(decimal? HeightCm, string PreferredUnit, decimal? TdeeKcal);

public record UpdateUserSettingsDto(decimal? HeightCm, string PreferredUnit, decimal? TdeeKcal);
