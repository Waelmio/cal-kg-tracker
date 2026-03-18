using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/calorie-goals")]
public class CalorieGoalsController(ICalorieGoalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CalorieGoalDto>>> GetAll()
        => Ok(await service.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<CalorieGoalDto>> GetActive()
    {
        var goal = await service.GetActiveAsync();
        return goal is null ? NotFound() : Ok(goal);
    }

    [HttpPost]
    public async Task<ActionResult<CalorieGoalDto>> Create([FromBody] CreateCalorieGoalDto dto)
    {
        var goal = await service.CreateAsync(dto);
        return CreatedAtAction(nameof(GetActive), goal);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
