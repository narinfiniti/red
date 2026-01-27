﻿namespace PluginHost.Abstractions;

public interface IPlugin
{
	string Name { get; }
	string TargetOS { get; }
	string SupportedVersion { get; }

	Task<string> ExecuteAsync(string command, CancellationToken ct);
}
