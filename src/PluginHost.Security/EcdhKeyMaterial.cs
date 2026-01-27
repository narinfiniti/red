using System.Security.Cryptography;

namespace PluginHost.Security;

public sealed class EcdhKeyMaterial : IAsyncDisposable
{
    public EcdhKeyMaterial(ECDiffieHellman ecdh, byte[] publicKey)
    {
        Ecdh = ecdh;
        PublicKey = publicKey;
    }

    public ECDiffieHellman Ecdh { get; }
    public byte[] PublicKey { get; }

    public ValueTask DisposeAsync()
    {
        Ecdh.Dispose();
        return ValueTask.CompletedTask;
    }
}
