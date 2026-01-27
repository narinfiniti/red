namespace PluginHost.Api.Models;

public sealed record PluginUnloadRequest(string Name, string TargetOs, string Version);
