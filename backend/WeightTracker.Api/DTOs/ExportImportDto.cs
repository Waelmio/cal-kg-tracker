namespace WeightTracker.Api.DTOs;

public record ExportImportDto(
    UserSettingsDto Settings,
    List<DailyLogDto> DailyLogs,
    List<GoalDto> Goals,
    List<CalorieGoalDto> CalorieGoals);
