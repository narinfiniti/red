using System.Net;
using System.Net.Http.Json;
using PluginHost.Security;

namespace PluginHost.Tests;

public sealed class HandshakeTests : IClassFixture<PluginHostWebFactory>
{
    private readonly PluginHostWebFactory _factory;

    public HandshakeTests(PluginHostWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Handshake_Returns_Server_Public_Key_And_Session()
    {
        using var client = _factory.CreateClient();
        var encryptedClient = new EncryptedClient(client, "client-a");

        await encryptedClient.HandshakeAsync(CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(encryptedClient.SessionId));
        Assert.Equal(SecurityConstants.AesKeySizeBytes, encryptedClient.AesKey.Length);
    }

    [Fact]
    public async Task Handshake_Rejects_Invalid_Request()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/handshake/init", new HandshakeRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
