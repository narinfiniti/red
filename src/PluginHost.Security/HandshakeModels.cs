namespace PluginHost.Security;

public sealed record HandshakeRequest(string ClientPublicKeyBase64, string ClientFingerprint);

public sealed record HandshakeResponse(string SessionId, string ServerPublicKeyBase64, DateTimeOffset ExpiresAtUtc);
