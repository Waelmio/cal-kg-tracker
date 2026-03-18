using Microsoft.AspNetCore.Mvc;
using WeightTracker.Api.DTOs;
using WeightTracker.Api.Services;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserSettingsDto>> Get() => Ok(await service.GetAsync());

    [HttpPut]
    public async Task<ActionResult<UserSettingsDto>> Update([FromBody] UpdateUserSettingsDto dto) =>
        Ok(await service.UpdateAsync(dto));
}
