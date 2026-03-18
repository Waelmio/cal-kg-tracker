namespace WeightTracker.Api.DTOs;

public record DailyLogDto(
    int Id,
    string Date,
    decimal? WeightKg,
    int? CaloriesKcal,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record UpsertDailyLogDto(
    string Date,
    decimal? WeightKg,
    int? CaloriesKcal,
    string? Notes);
