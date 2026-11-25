using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    public async Task<ActionResult<TdeeEntryDto>> Create([FromBody] CreateTdeeEntryDto dto, [FromQuery, BindRequired] DateOnly today)
    {
        var entry = await service.CreateAsync(dto, today);
        return CreatedAtAction(nameof(GetAll), entry);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id) => await service.DeleteAsync(id) ? NoContent() : NotFound();
}
