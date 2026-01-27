using System.Net;

namespace PluginHost.Tests;

public sealed class UnauthorizedTests : IClassFixture<PluginHostWebFactory>
{
    private readonly PluginHostWebFactory _factory;

    public UnauthorizedTests(PluginHostWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Encrypted_Endpoints_Reject_Plaintext()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/plugins", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
