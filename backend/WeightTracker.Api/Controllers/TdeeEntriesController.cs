using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/tdee-entries")]
public class TdeeEntriesController(ITdeeEntryService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TdeeEntryDto>>> GetAll() => Ok(await service.GetAllAsync());

    [HttpPost]
    public async Task<ActionResult<TdeeEntryDto>> Create([FromBody] CreateTdeeEntryDto dto, [FromQuery] DateOnly? today)
    {
        if (today is null) return Problem(title: "today is required (yyyy-MM-dd, the client's local date).", statusCode: 400);
        var entry = await service.CreateAsync(dto, today.Value);
        return CreatedAtAction(nameof(GetAll), entry);
    }
}
