using System.ComponentModel.DataAnnotations;
using WeightTracker.Api.Models;

namespace WeightTracker.Api.DTOs;

public record DailyLogDto(
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Id,
    string Date,
    decimal? WeightKg,
    int? CaloriesKcal,
    string? Notes,
    bool IsCheatDay,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateOnly? CaloriesSetOn = null,
    LogSource? CaloriesSource = null,
    LogSource? WeightSource = null);

/// <summary>
/// Full replace of a day: a null field clears it, 0 kcal is kept (a fasting day), cheat day and note included.
/// <c>Date</c> is taken from the URL.
/// </summary>
public record UpsertDailyLogDto(
    string Date,
    [Range(1, 700)] decimal? WeightKg,
    [Range(0, 30000)] int? CaloriesKcal,
    [MaxLength(500)] string? Notes,
    bool IsCheatDay = false);
