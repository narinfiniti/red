namespace PluginHost.Security;

public sealed class SecurityOptions
{
    /// <summary>
    /// When true (default), all endpoints except those explicitly marked with AllowUnencrypted
    /// require an EncryptedPayload envelope and are handled by EncryptionMiddleware.
    /// Set to false only for local development/testing.
    /// </summary>
    public bool RequireEncryption { get; init; } = true;

    public TimeSpan SessionTtl { get; init; } = TimeSpan.FromMinutes(5);

    public string? ServerEcdhPrivateKeyDBase64 { get; init; }
    public string? ServerEcdhPublicKeyXBase64 { get; init; }
    public string? ServerEcdhPublicKeyYBase64 { get; init; }
}
