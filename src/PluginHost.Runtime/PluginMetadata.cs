﻿namespace PluginHost.Runtime;

public sealed record PluginMetadata(
	string Name,
	string TargetOS,
	string SupportedVersion,
	string AssemblyPath,
	DateTimeOffset LoadedAtUtc);
