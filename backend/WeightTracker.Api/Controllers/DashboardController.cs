using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(IDashboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get() => Ok(await service.GetAsync());
}
