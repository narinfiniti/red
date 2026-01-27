﻿using PluginHost.Abstractions;

namespace WindowsPlugin;

public sealed class WindowsEchoPlugin : IPlugin
{
	public string Name => "WindowsEcho";
	public string TargetOS => "windows";
	public string SupportedVersion => "1.0.0";

	public Task<string> ExecuteAsync(string command, CancellationToken ct)
	{
		ct.ThrowIfCancellationRequested();
		return Task.FromResult($"windows:{command}");
	}
}
