using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.Stats;

namespace WeightTracker.Api.Controllers;

/// <summary>Every statistic is computed here, server-side. The client always sends its local date as <c>today</c>.</summary>
[ApiController]
[Route("api/stats")]
public class StatsController(IStatsService stats) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsDto>> Dashboard([FromQuery] DateOnly? today)
        => today is null ? NoToday() : Ok(await stats.DashboardAsync(today.Value));

    [HttpGet("calendar")]
    public async Task<ActionResult<CalendarDto>> Calendar([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] DateOnly? today)
    {
        if (today is null) return NoToday();
        if (to < from) return Problem(title: "'to' must not be before 'from'.", statusCode: 400);
        if (to.DayNumber - from.DayNumber > 400) return Problem(title: "The range is limited to 400 days.", statusCode: 400);
        return Ok(await stats.CalendarAsync(from, to, today.Value));
    }

    [HttpGet("periods")]
    public async Task<ActionResult<List<PeriodDto>>> Periods([FromQuery] string unit = "week", [FromQuery] int count = 12, [FromQuery] DateOnly? today = null)
    {
        if (today is null) return NoToday();
        if (unit is not ("week" or "month")) return Problem(title: "unit must be 'week' or 'month'.", statusCode: 400);
        return Ok(await stats.PeriodsAsync(unit, count, today.Value));
    }

    [HttpGet("month/{month}")]
    public async Task<ActionResult<MonthSummaryDto>> Month(string month, [FromQuery] DateOnly? today)
    {
        if (today is null) return NoToday();
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first))
            return Problem(title: "month must look like 2026-10.", statusCode: 400);
        return Ok(await stats.MonthAsync(first, today.Value));
    }

    [HttpGet("tdee")]
    public async Task<ActionResult<TdeeStatsDto>> Tdee([FromQuery] int days = 90, [FromQuery] DateOnly? today = null)
    {
        if (today is null) return NoToday();
        if (days is not (30 or 60 or 90)) return Problem(title: "days must be 30, 60 or 90.", statusCode: 400);
        return Ok(await stats.TdeeAsync(days, today.Value));
    }

    private ObjectResult NoToday() => Problem(title: "today is required (yyyy-MM-dd, the client's local date).", statusCode: 400);
}
