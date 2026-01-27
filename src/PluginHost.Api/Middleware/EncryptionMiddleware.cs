using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PluginHost.Security;

namespace PluginHost.Api.Middleware;

public sealed class EncryptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;

    public EncryptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context, ISessionStore sessionStore, AesGcmEnvelopeCrypto crypto, SecurityOptions options)
    {
        if (!options.RequireEncryption)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (IsUnencryptedEndpoint(context))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var payload = await ReadEncryptedPayloadAsync(context.Request, context.RequestAborted).ConfigureAwait(false);
        if (payload is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var session = await sessionStore
            .TryGetAsync(payload.ClientFingerprint, payload.SessionId, context.RequestAborted)
            .ConfigureAwait(false);

        if (session is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        byte[] plaintext;
        try
        {
            plaintext = crypto.Decrypt(payload, session.AesKey);
        }
        catch (CryptographicException)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await using var plaintextStream = new MemoryStream(plaintext, writable: false);
        context.Request.Body = plaintextStream;
        context.Items["Security.Session"] = payload;

        var originalResponseBody = context.Response.Body;
        await using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }

        var responseBytes = responseBuffer.ToArray();
        var encryptedResponse = crypto
            .Encrypt(payload.SessionId, payload.ClientFingerprint, session.AesKey, responseBytes);

        if (context.Response.StatusCode == StatusCodes.Status204NoContent)
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
        }

        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(encryptedResponse, JsonOptions);
        await context.Response.WriteAsync(json, Encoding.UTF8, context.RequestAborted).ConfigureAwait(false);
    }

    private static bool IsUnencryptedEndpoint(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        return endpoint?.Metadata.GetMetadata<AllowUnencryptedAttribute>() is not null;
    }

    private static async Task<EncryptedPayload?> ReadEncryptedPayloadAsync(HttpRequest request, CancellationToken ct)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        return JsonSerializer.Deserialize<EncryptedPayload>(body, JsonOptions);
    }
}
