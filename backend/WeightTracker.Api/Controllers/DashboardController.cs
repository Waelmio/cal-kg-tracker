using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get([FromQuery] DateOnly? today)
    {
        var date = today ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        return Ok(await service.GetAsync(date));
    }
}
