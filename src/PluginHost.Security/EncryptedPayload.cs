namespace PluginHost.Security;

public sealed record EncryptedPayload(
    string SessionId,
    string ClientFingerprint,
    string NonceBase64,
    string CiphertextBase64,
    string TagBase64,
    int Version = SecurityConstants.PayloadVersion
);
