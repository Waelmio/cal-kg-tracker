using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/daily-logs")]
public class DailyLogController(IDailyLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DailyLogDto>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? limit)
        => Ok(await service.GetAllAsync(from, to, limit));

    [HttpGet("{date}")]
    public async Task<ActionResult<DailyLogDto>> GetByDate(DateOnly date)
    {
        var log = await service.GetByDateAsync(date);
        return log is null ? NotFound() : Ok(log);
    }

    [HttpPut("{date}")]
    public async Task<ActionResult<DailyLogDto>> Upsert(DateOnly date, [FromBody] UpsertDailyLogDto dto)
    {
        var normalized = new UpsertDailyLogDto(date.ToString("yyyy-MM-dd"), dto.WeightKg, dto.CaloriesKcal, dto.Notes);
        return Ok(await service.UpsertAsync(normalized));
    }

    [HttpDelete("{date}")]
    public async Task<IActionResult> DeleteDay(DateOnly date)
    {
        var deleted = await service.DeleteDayAsync(date);
        return deleted ? NoContent() : NotFound();
    }

    [HttpDelete("{date}/weight")]
    public async Task<ActionResult<DailyLogDto?>> DeleteWeight(DateOnly date)
    {
        var log = await service.DeleteWeightAsync(date);
        return Ok(log);
    }

    [HttpDelete("{date}/calories")]
    public async Task<ActionResult<DailyLogDto?>> DeleteCalories(DateOnly date)
    {
        var log = await service.DeleteCaloriesAsync(date);
        return Ok(log);
    }
}
