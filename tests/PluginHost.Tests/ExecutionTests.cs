using PluginHost.Api.Models;
using LinuxPlugin;
using WindowsPlugin;

namespace PluginHost.Tests;

public sealed class ExecutionTests : IClassFixture<PluginHostWebFactory>
{
    private readonly PluginHostWebFactory _factory;

    public ExecutionTests(PluginHostWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Execute_Routes_By_TargetOS_And_Version()
    {
        using var client = _factory.CreateClient();
        await using var encryptedClient = new EncryptedClient(client, "client-exec");
        await encryptedClient.HandshakeAsync(CancellationToken.None);

        await UploadPluginAsync(encryptedClient, typeof(WindowsEchoPlugin).Assembly.Location, "WindowsEcho");
        await UploadPluginAsync(encryptedClient, typeof(LinuxEchoPlugin).Assembly.Location, "LinuxEcho");

        var executeRequest = new ExecuteRequest("linux", "1.0.0", "ping");
        var response = await encryptedClient.PostEncryptedAsync<ExecuteRequest, ExecuteResponse>(
            "/execute", executeRequest, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("linux:ping", response!.Output);
    }

    private static async Task UploadPluginAsync(EncryptedClient client, string assemblyPath, string name)
    {
        var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath);
        var request = new PluginUploadRequest(name, Convert.ToBase64String(assemblyBytes));
        var response = await client.PostEncryptedAsync<PluginUploadRequest, PluginHost.Runtime.PluginMetadata>(
            "/plugins", request, CancellationToken.None);

        Assert.NotNull(response);
    }
}
