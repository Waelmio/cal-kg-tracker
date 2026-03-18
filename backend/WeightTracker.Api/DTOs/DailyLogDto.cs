using System.ComponentModel.DataAnnotations;

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
    [Range(1, 700)] decimal? WeightKg,
    [Range(0, 30000)] int? CaloriesKcal,
    [MaxLength(500)] string? Notes);
