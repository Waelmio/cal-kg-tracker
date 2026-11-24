using WeightTracker.Api.Models;

namespace WeightTracker.Api.DTOs;

public record TdeeEntryDto([property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] int Id, int Kcal, DateOnly EffectiveFrom, TdeeSource Source, DateTimeOffset CreatedAt);

public record CreateTdeeEntryDto(
    [System.ComponentModel.DataAnnotations.Range(500, 15000)] int Kcal,
    DateOnly? EffectiveFrom = null,
    TdeeSource? Source = null);

/// <summary>Export format. <c>Version</c> is 2 from the redesign on; absent (null) in older exports.</summary>
public record ExportImportDto(
    UserSettingsDto Settings,
    List<DailyLogDto> DailyLogs,
    List<GoalDto> Goals,
    List<CalorieGoalDto> CalorieGoals,
    int? Version = null,
    List<TdeeEntryDto>? TdeeEntries = null);
