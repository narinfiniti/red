using Microsoft.AspNetCore.Mvc;
using PluginHost.Api.Middleware;
using PluginHost.Api.Models;
using PluginHost.Runtime;

namespace PluginHost.Api.Controllers;

[ApiController]
[Route("plugins")]
public sealed class PluginsController : ControllerBase
{
    private readonly PluginManager _pluginManager;

    public PluginsController(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    [HttpPost]
    public async Task<ActionResult<PluginMetadata>> UploadAsync([FromBody] PluginUploadRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PluginName) || string.IsNullOrWhiteSpace(request.AssemblyBase64))
        {
            return BadRequest();
        }

        var assemblyBytes = Convert.FromBase64String(request.AssemblyBase64);
        var metadata = await _pluginManager
            .LoadAsync(request.PluginName, assemblyBytes, ct).ConfigureAwait(false);
        return Ok(metadata);
    }

    [HttpGet]
    [AllowUnencrypted]
    public ActionResult<IReadOnlyCollection<PluginMetadata>> List()
    {
        return Ok(_pluginManager.List());
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAsync([FromBody] PluginUnloadRequest request, CancellationToken ct)
    {
        var removed = await _pluginManager
            .UnloadAsync(request.Name, request.TargetOs, request.Version, ct).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }
}
