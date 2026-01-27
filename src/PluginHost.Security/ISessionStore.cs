namespace PluginHost.Security;

public interface ISessionStore
{
    ValueTask StoreAsync(string clientFingerprint, string sessionId, SessionContext context, CancellationToken ct);
    ValueTask<SessionContext?> TryGetAsync(string clientFingerprint, string sessionId, CancellationToken ct);
    ValueTask RemoveAsync(string clientFingerprint, string sessionId, CancellationToken ct);
    ValueTask CleanupExpiredAsync(CancellationToken ct);
}
