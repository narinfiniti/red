namespace PluginHost.Api.Models;

public sealed record PluginUploadRequest(string PluginName, string AssemblyBase64);
