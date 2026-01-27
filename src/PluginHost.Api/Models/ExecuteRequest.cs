namespace PluginHost.Api.Models;

public sealed record ExecuteRequest(string TargetOS, string SupportedVersion, string Command);
