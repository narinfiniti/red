using System.Security.Cryptography;

namespace PluginHost.Security;

public sealed class EcdhKeyProvider : IEcdhKeyProvider
{
    private readonly SecurityOptions _options;

    public EcdhKeyProvider(SecurityOptions options)
    {
        _options = options;
    }

    public EcdhKeyMaterial CreateKeyMaterial()
    {
        var ecdh = CreateEcdh();
        var publicKey = ecdh.PublicKey.ExportSubjectPublicKeyInfo();
        return new EcdhKeyMaterial(ecdh, publicKey);
    }

    private ECDiffieHellman CreateEcdh()
    {
        if (!string.IsNullOrWhiteSpace(_options.ServerEcdhPrivateKeyDBase64) &&
            !string.IsNullOrWhiteSpace(_options.ServerEcdhPublicKeyXBase64) &&
            !string.IsNullOrWhiteSpace(_options.ServerEcdhPublicKeyYBase64))
        {
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = Convert.FromBase64String(_options.ServerEcdhPrivateKeyDBase64),
                Q = new ECPoint
                {
                    X = Convert.FromBase64String(_options.ServerEcdhPublicKeyXBase64),
                    Y = Convert.FromBase64String(_options.ServerEcdhPublicKeyYBase64)
                }
            };

            var deterministic = ECDiffieHellman.Create();
            deterministic.ImportParameters(parameters);
            return deterministic;
        }

        return ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    }
}
