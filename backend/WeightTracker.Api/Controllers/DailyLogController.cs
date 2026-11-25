using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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

    /// <summary>Full replace of the day. <c>today</c> is the client's local date.</summary>
    [HttpPut("{date}")]
    public async Task<ActionResult<DailyLogDto>> Upsert(DateOnly date, [FromBody] UpsertDailyLogDto dto, [FromQuery, BindRequired] DateOnly today)
    {
        var normalized = dto with { Date = date.ToString("yyyy-MM-dd") };
        var saved = await service.UpsertAsync(normalized, today);
        return saved is null ? NoContent() : Ok(saved);
    }

    /// <summary>Delete entry: the whole day, note and cheat mark included.</summary>
    [HttpDelete("{date}")]
    public async Task<IActionResult> DeleteDay(DateOnly date)
    {
        var deleted = await service.DeleteDayAsync(date);
        return deleted ? NoContent() : NotFound();
    }
}
