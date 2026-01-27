using System.Net;
using PluginHost.Api.Models;
using WindowsPlugin;

namespace PluginHost.Tests;

public sealed class PluginLifecycleTests : IClassFixture<PluginHostWebFactory>
{
    private readonly PluginHostWebFactory _factory;

    public PluginLifecycleTests(PluginHostWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Plugin_Can_Be_Loaded_And_Unloaded()
    {
        using var client = _factory.CreateClient();
        await using var encryptedClient = new EncryptedClient(client, "client-lifecycle");
        await encryptedClient.HandshakeAsync(CancellationToken.None);

        var assemblyPath = typeof(WindowsEchoPlugin).Assembly.Location;
        var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath);

        var uploadRequest = new PluginUploadRequest("WindowsEcho", Convert.ToBase64String(assemblyBytes));
        var uploadResponse = await encryptedClient.PostEncryptedAsync<PluginUploadRequest, PluginHost.Runtime.PluginMetadata>(
            "/plugins", uploadRequest, CancellationToken.None);

        Assert.NotNull(uploadResponse);
        Assert.Equal("WindowsEcho", uploadResponse!.Name);

        var deleteResponse = await encryptedClient.DeleteEncryptedAsync(
            $"/plugins/{uploadResponse.Name}/{uploadResponse.TargetOS}/{uploadResponse.SupportedVersion}",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
