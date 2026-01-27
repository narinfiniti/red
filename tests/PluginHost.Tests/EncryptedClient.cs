using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using PluginHost.Security;

namespace PluginHost.Tests;

public sealed class EncryptedClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ECDiffieHellman _clientEcdh;
    private readonly AesGcmEnvelopeCrypto _crypto = new();
    private readonly string _clientFingerprint;

    public EncryptedClient(HttpClient httpClient, string clientFingerprint)
    {
        _httpClient = httpClient;
        _clientEcdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        _clientFingerprint = clientFingerprint;
    }

    public string SessionId { get; private set; } = string.Empty;
    public byte[] AesKey { get; private set; } = Array.Empty<byte>();

    public async Task HandshakeAsync(CancellationToken ct)
    {
        var request = new HandshakeRequest(
            Convert.ToBase64String(_clientEcdh.PublicKey.ExportSubjectPublicKeyInfo()),
            _clientFingerprint);

        var response = await _httpClient.PostAsJsonAsync("/handshake/init", request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<HandshakeResponse>(JsonOptions, ct).ConfigureAwait(false);
        if (payload is null)
        {
            throw new InvalidOperationException("Handshake failed.");
        }

        SessionId = payload.SessionId;

        using var serverEcdh = ECDiffieHellman.Create();
        serverEcdh.ImportSubjectPublicKeyInfo(Convert.FromBase64String(payload.ServerPublicKeyBase64), out _);

        var sharedSecret = _clientEcdh.DeriveKeyFromHash(serverEcdh.PublicKey, HashAlgorithmName.SHA256);
        try
        {
            var salt = SHA256.HashData(Combine(
                _clientEcdh.PublicKey.ExportSubjectPublicKeyInfo(),
                Convert.FromBase64String(payload.ServerPublicKeyBase64)));
            var info = System.Text.Encoding.UTF8.GetBytes(SecurityConstants.HkdfInfo);
            AesKey = HkdfSha256.DeriveKey(sharedSecret, salt, info, SecurityConstants.AesKeySizeBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    public async Task<TResponse?> PostEncryptedAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var envelope = _crypto.Encrypt(SessionId, _clientFingerprint, AesKey, plaintext);

        var response = await _httpClient.PostAsJsonAsync(path, envelope, JsonOptions, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var encrypted = await response.Content.ReadFromJsonAsync<EncryptedPayload>(JsonOptions, ct).ConfigureAwait(false);
        if (encrypted is null)
        {
            return default;
        }

        var decrypted = _crypto.Decrypt(encrypted, AesKey);
        return JsonSerializer.Deserialize<TResponse>(decrypted, JsonOptions);
    }

    public async Task<HttpResponseMessage> PostEncryptedRawAsync<TRequest>(string path, TRequest body, CancellationToken ct)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(body, JsonOptions);
        var envelope = _crypto.Encrypt(SessionId, _clientFingerprint, AesKey, plaintext);
        return await _httpClient.PostAsJsonAsync(path, envelope, JsonOptions, ct).ConfigureAwait(false);
    }

    public async Task<HttpResponseMessage> DeleteEncryptedAsync(string path, CancellationToken ct)
    {
        var plaintext = Array.Empty<byte>();
        var envelope = _crypto.Encrypt(SessionId, _clientFingerprint, AesKey, plaintext);
        var request = new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(envelope, options: JsonOptions)
        };

        return await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        _clientEcdh.Dispose();
        CryptographicOperations.ZeroMemory(AesKey);
        return ValueTask.CompletedTask;
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
}
