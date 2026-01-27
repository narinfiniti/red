﻿using PluginHost.Abstractions;

namespace LinuxPlugin;

public sealed class LinuxEchoPlugin : IPlugin
{
	public string Name => "LinuxEcho";
	public string TargetOS => "linux";
	public string SupportedVersion => "1.0.0";

	public Task<string> ExecuteAsync(string command, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		return Task.FromResult($"linux:{command}");
	}
}
