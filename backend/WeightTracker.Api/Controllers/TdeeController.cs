using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/tdee")]
public class TdeeController(ITdeeComputationService service) : ControllerBase
{
    [HttpGet("computed")]
    public async Task<IActionResult> GetComputed([FromQuery] int days = 90)
    {
        if (days < 14 || days > 365)
            return BadRequest("days must be between 14 and 365.");

        var result = await service.ComputeAsync(days);
        if (result is null)
            return UnprocessableEntity(new { error = "Not enough data. Need at least 7 calorie entries and 3 weight entries in the period." });

        return Ok(result);
    }
}
