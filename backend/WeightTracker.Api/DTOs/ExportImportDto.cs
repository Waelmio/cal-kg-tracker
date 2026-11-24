using WeightTracker.Api.Models;

namespace WeightTracker.Api.DTOs;

public record TdeeEntryDto(int Id, int Kcal, DateOnly EffectiveFrom, TdeeSource Source, DateTimeOffset CreatedAt);

/// <summary>Export format. <c>Version</c> is 2 from the redesign on; absent (null) in older exports.</summary>
public record ExportImportDto(
    UserSettingsDto Settings,
    List<DailyLogDto> DailyLogs,
    List<GoalDto> Goals,
    List<CalorieGoalDto> CalorieGoals,
    int? Version = null,
    List<TdeeEntryDto>? TdeeEntries = null);
