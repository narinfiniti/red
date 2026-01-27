using System.Security.Cryptography;

namespace PluginHost.Security;

public sealed class SessionContext : IAsyncDisposable
{
    public SessionContext(byte[] aesKey, byte[] clientPublicKey, DateTimeOffset expiresAt)
    {
        AesKey = aesKey;
        ClientPublicKey = clientPublicKey;
        ExpiresAt = expiresAt;
    }

    public byte[] AesKey { get; }
    public byte[] ClientPublicKey { get; }
    public DateTimeOffset ExpiresAt { get; }

    public ValueTask DisposeAsync()
    {
        CryptographicOperations.ZeroMemory(AesKey);
        CryptographicOperations.ZeroMemory(ClientPublicKey);
        return ValueTask.CompletedTask;
    }
}
