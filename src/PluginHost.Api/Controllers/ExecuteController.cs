using Microsoft.AspNetCore.Mvc;
using PluginHost.Api.Models;
using PluginHost.Runtime;

namespace PluginHost.Api.Controllers;

[ApiController]
[Route("execute")]
public sealed class ExecuteController : ControllerBase
{
    private readonly PluginManager _pluginManager;

    public ExecuteController(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    [HttpPost]
    public async Task<ActionResult<ExecuteResponse>> ExecuteAsync([FromBody] ExecuteRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TargetOS) ||
            string.IsNullOrWhiteSpace(request.SupportedVersion) ||
            string.IsNullOrWhiteSpace(request.Command))
        {
            return BadRequest();
        }

        try
        {
            var output = await _pluginManager
                .ExecuteAsync(request.TargetOS, request.SupportedVersion, request.Command, ct)
                .ConfigureAwait(false);
            return Ok(new ExecuteResponse(output));
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "No compatible plugin found.", StringComparison.Ordinal))
        {
            return NotFound(new { error = "No compatible plugin found." });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}
