using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using PluginHost.Api.Middleware;
using PluginHost.Security;

namespace PluginHost.Api.Controllers;

[ApiController]
[Route("handshake")]
public sealed class HandshakeController : ControllerBase
{
    private readonly HandshakeService _handshakeService;
    private readonly ISessionStore _sessionStore;

    public HandshakeController(HandshakeService handshakeService, ISessionStore sessionStore)
    {
        _handshakeService = handshakeService;
        _sessionStore = sessionStore;
    }

    [HttpPost("init")]
    [AllowUnencrypted]
    public async Task<ActionResult<HandshakeResponse>> InitAsync([FromBody] HandshakeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientPublicKeyBase64) || string.IsNullOrWhiteSpace(request.ClientFingerprint))
        {
            return BadRequest();
        }

        try
        {
            var (response, context) = await _handshakeService.CreateHandshakeAsync(request, ct).ConfigureAwait(false);
            await _sessionStore.StoreAsync(request.ClientFingerprint, response.SessionId, context, ct).ConfigureAwait(false);
            return Ok(response);
        }
        catch (CryptographicException)
        {
            return BadRequest();
        }
        catch (Exception)
        {
            return BadRequest();
        }
    }
}
