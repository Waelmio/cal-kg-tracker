using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
    public async Task<ActionResult<GoalDto>> Create([FromBody] CreateGoalDto dto, [FromQuery, BindRequired] DateOnly today)
    {
        try
        {
            var goal = await service.CreateAsync(dto, today);
            return CreatedAtAction(nameof(GetActive), goal);
        }
        catch (GoalException e) { return Problem(title: e.Message, statusCode: e.Status); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GoalDto>> Update(int id, [FromBody] UpdateGoalDto dto)
    {
        try
        {
            var goal = await service.UpdateAsync(id, dto);
            return goal is null ? NotFound() : Ok(goal);
        }
        catch (GoalException e) { return Problem(title: e.Message, statusCode: e.Status); }
    }

    [HttpPost("{id:int}/end")]
    public async Task<ActionResult<GoalDto>> End(int id, [FromBody] EndGoalDto dto, [FromQuery, BindRequired] DateOnly today)
    {
        try
        {
            var goal = await service.EndAsync(id, dto, today);
            return goal is null ? NotFound() : Ok(goal);
        }
        catch (GoalException e) { return Problem(title: e.Message, statusCode: e.Status); }
    }

    [HttpPost("maintain")]
    public async Task<ActionResult<GoalDto>> Maintain([FromQuery, BindRequired] DateOnly today)
    {
        try
        {
            return Ok(await service.MaintainAsync(today));
        }
        catch (GoalException e) { return Problem(title: e.Message, statusCode: e.Status); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await service.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
