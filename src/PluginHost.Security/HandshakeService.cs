using System.Security.Cryptography;
using System.Text;

namespace PluginHost.Security;

public sealed class HandshakeService
{
    private readonly IEcdhKeyProvider _keyProvider;
    private readonly SecurityOptions _options;

    public HandshakeService(IEcdhKeyProvider keyProvider, SecurityOptions options)
    {
        _keyProvider = keyProvider;
        _options = options;
    }

    public async ValueTask<(HandshakeResponse Response, SessionContext Context)> CreateHandshakeAsync(
        HandshakeRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (request.ClientPublicKeyBase64.Length > 4096)
        {
            throw new CryptographicException("Client public key is too large.");
        }

        byte[] clientPublicKey;
        try
        {
            clientPublicKey = Convert.FromBase64String(request.ClientPublicKeyBase64);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("Invalid client public key encoding.", ex);
        }

        if (clientPublicKey.Length == 0)
        {
            throw new CryptographicException("Client public key is empty.");
        }

        if (clientPublicKey.Length > 512)
        {
            throw new CryptographicException("Client public key is too large.");
        }

        await using var keyMaterial = _keyProvider.CreateKeyMaterial();
        using var clientEcdh = ECDiffieHellman.Create();
        try
        {
            clientEcdh.ImportSubjectPublicKeyInfo(clientPublicKey, out _);
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("Invalid client public key.", ex);
        }

        var sharedSecret = keyMaterial.Ecdh.DeriveKeyFromHash(clientEcdh.PublicKey, HashAlgorithmName.SHA256);
        try
        {
            var salt = SHA256.HashData(Combine(clientPublicKey, keyMaterial.PublicKey));
            var info = Encoding.UTF8.GetBytes(SecurityConstants.HkdfInfo);
            var aesKey = HkdfSha256.DeriveKey(sharedSecret, salt, info, SecurityConstants.AesKeySizeBytes);

            var expiresAt = DateTimeOffset.UtcNow.Add(_options.SessionTtl);
            var sessionId = Guid.NewGuid().ToString("N");
            var context = new SessionContext(aesKey, clientPublicKey, expiresAt);

            var response = new HandshakeResponse(
                sessionId,
                Convert.ToBase64String(keyMaterial.PublicKey),
                expiresAt);

            return (response, context);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }
}
