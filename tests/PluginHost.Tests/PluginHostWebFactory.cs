using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PluginHost.Tests;

public sealed class PluginHostWebFactory : WebApplicationFactory<Program>
{
    public ECParameters ServerParameters { get; }

    public PluginHostWebFactory()
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        ServerParameters = ecdh.ExportParameters(true);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configBuilder =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Security:SessionTtl"] = "00:05:00",
                ["Security:ServerEcdhPrivateKeyDBase64"] = Convert.ToBase64String(ServerParameters.D!),
                ["Security:ServerEcdhPublicKeyXBase64"] = Convert.ToBase64String(ServerParameters.Q.X!),
                ["Security:ServerEcdhPublicKeyYBase64"] = Convert.ToBase64String(ServerParameters.Q.Y!)
            };

            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
