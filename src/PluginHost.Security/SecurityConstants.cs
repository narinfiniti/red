﻿namespace PluginHost.Security;

public static class SecurityConstants
{
	public const int AesKeySizeBytes = 32;
	public const int NonceSizeBytes = 12;
	public const int TagSizeBytes = 16;
	public const int PayloadVersion = 1;
	public const string HkdfInfo = "PluginHost-AES-256-GCM";
}
