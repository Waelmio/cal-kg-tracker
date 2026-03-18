using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/daily-logs")]
public class DailyLogController(IDailyLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] int? limit)
    {
        DateOnly? fromDate = from is not null ? DateOnly.Parse(from) : null;
        DateOnly? toDate = to is not null ? DateOnly.Parse(to) : null;
        return Ok(await service.GetAllAsync(fromDate, toDate, limit));
    }

    [HttpGet("{date}")]
    public async Task<IActionResult> GetByDate(string date)
    {
        var log = await service.GetByDateAsync(DateOnly.Parse(date));
        return log is null ? NotFound() : Ok(log);
    }

    [HttpPut("{date}")]
    public async Task<IActionResult> Upsert(string date, [FromBody] UpsertDailyLogDto dto)
    {
        // Ensure route date matches body date
        var normalized = new UpsertDailyLogDto(date, dto.WeightKg, dto.CaloriesKcal, dto.Notes);
        return Ok(await service.UpsertAsync(normalized));
    }

    [HttpDelete("{date}")]
    public async Task<IActionResult> DeleteDay(string date)
    {
        var deleted = await service.DeleteDayAsync(DateOnly.Parse(date));
        return deleted ? NoContent() : NotFound();
    }

    [HttpDelete("{date}/weight")]
    public async Task<IActionResult> DeleteWeight(string date)
    {
        var log = await service.DeleteWeightAsync(DateOnly.Parse(date));
        // null means row was cleaned up entirely
        return Ok(log);
    }

    [HttpDelete("{date}/calories")]
    public async Task<IActionResult> DeleteCalories(string date)
    {
        var log = await service.DeleteCaloriesAsync(DateOnly.Parse(date));
        return Ok(log);
    }
}
