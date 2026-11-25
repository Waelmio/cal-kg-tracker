using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WeightTracker.Api.Integrations;

namespace WeightTracker.Api.Controllers;

[ApiController]
[Route("api/integrations")]
[IntegrationErrors]
public class IntegrationsController(IntegrationService service, IntegrationSyncService sync, IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<IntegrationDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));

    /// <summary>Starts a connection: the browser must go to the returned address.</summary>
    [HttpPost("{provider}/connect")]
    public async Task<ActionResult<ConnectResultDto>> Connect(string provider, [FromBody] ConnectDto dto, CancellationToken ct)
        => Ok(new ConnectResultDto(await service.ConnectAsync(provider, BaseUrl(), dto.UsePin, ct)));

    /// <summary>The provider sends the browser here after the user approved.</summary>
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, [FromQuery(Name = "oauth_token")] string? token, [FromQuery(Name = "oauth_verifier")] string? verifier, CancellationToken ct)
    {
        var (outcome, returnBase) = await service.CompleteCallbackAsync(provider, token, verifier, ct);
        return Redirect($"{returnBase}/settings?integration={Uri.EscapeDataString(provider)}&result={outcome.ToString().ToLowerInvariant()}");
    }

    /// <summary>For when the provider can't send the browser back: the user types the code it shows.</summary>
    [HttpPost("{provider}/verify")]
    public async Task<IActionResult> Verify(string provider, [FromBody] VerifyDto dto, CancellationToken ct)
    {
        await service.VerifyPinAsync(provider, dto.Verifier, ct);
        return NoContent();
    }

    [HttpPost("{provider}/sync")]
    public async Task<ActionResult<SyncResultDto>> Sync(string provider, [FromQuery, BindRequired] DateOnly today, CancellationToken ct)
        => Ok(await sync.SyncAsync(provider, today, ct));

    /// <summary>The differences over a period, to decide case by case. Writes nothing.</summary>
    [HttpPost("{provider}/sync/preview")]
    public async Task<ActionResult<PreviewDto>> Preview(string provider, [FromBody] PreviewRequestDto dto, [FromQuery, BindRequired] DateOnly today, CancellationToken ct)
        => Ok(await sync.PreviewAsync(provider, dto.From, dto.To, today, ct));

    [HttpPost("{provider}/sync/apply")]
    public async Task<ActionResult<ApplyResultDto>> Apply(string provider, [FromBody] ApplyRequestDto dto, [FromQuery, BindRequired] DateOnly today, CancellationToken ct)
        => Ok(await sync.ApplyAsync(provider, dto, today, ct));

    /// <summary>Forgets the connection. Imported values stay.</summary>
    [HttpDelete("{provider}")]
    public async Task<IActionResult> Disconnect(string provider, CancellationToken ct)
        => await service.DisconnectAsync(provider, ct) ? NoContent() : NotFound();

    /// <summary>
    /// The address the browser uses to reach the app, which the provider must send it back to. Browsers send it along as
    /// Origin, which stays right behind a proxy and with the Vite dev server.
    /// </summary>
    private string BaseUrl()
    {
        if (config["PUBLIC_BASE_URL"] is { Length: > 0 } configured) return configured.Trim().TrimEnd('/');
        if (Uri.TryCreate(Request.Headers.Origin.ToString(), UriKind.Absolute, out var origin) && origin.Scheme is "http" or "https")
            return origin.GetLeftPart(UriPartial.Authority);
        return $"{Request.Scheme}://{Request.Host}";
    }
}

/// <summary>Errors of the integrations as ProblemDetails with a readable title.</summary>
public class IntegrationErrorsAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not IntegrationException e) return;
        context.Result = new ObjectResult(new ProblemDetails { Title = e.Message, Status = e.Status }) { StatusCode = e.Status };
        context.ExceptionHandled = true;
    }
}
