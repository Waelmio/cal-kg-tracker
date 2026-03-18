using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/goals")]
public class GoalsController(IGoalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<GoalDto>>> GetAll()
        => Ok(await service.GetAllAsync());

    [HttpGet("active")]
    public async Task<ActionResult<GoalDto>> GetActive()
    {
        var goal = await service.GetActiveAsync();
        return goal is null ? NotFound() : Ok(goal);
    }

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create([FromBody] CreateGoalDto dto)
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
