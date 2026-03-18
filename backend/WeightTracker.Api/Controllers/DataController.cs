using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/data")]
public class DataController(IDataService service) : ControllerBase
{
    [HttpGet("export")]
    public async Task<ActionResult<ExportImportDto>> Export() =>
        Ok(await service.ExportAsync());

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ExportImportDto dto)
    {
        await service.ImportAsync(dto);
        return NoContent();
    }
}
