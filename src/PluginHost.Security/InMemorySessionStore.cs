using System.Collections.Concurrent;

namespace PluginHost.Security;

public sealed class InMemorySessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, SessionContext> _sessions = new();

    public async ValueTask StoreAsync(string clientFingerprint, string sessionId, SessionContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = ComposeKey(clientFingerprint, sessionId);

        if (_sessions.TryGetValue(key, out var existing))
        {
            await existing.DisposeAsync().ConfigureAwait(false);
        }

        _sessions[key] = context;
    }

    public async ValueTask<SessionContext?> TryGetAsync(string clientFingerprint, string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = ComposeKey(clientFingerprint, sessionId);

        if (!_sessions.TryGetValue(key, out var context))
        {
            return null;
        }

        if (context.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(key, out _);
            await context.DisposeAsync();
            return null;
        }

        return context;
    }

    public async ValueTask RemoveAsync(string clientFingerprint, string sessionId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var key = ComposeKey(clientFingerprint, sessionId);
        if (_sessions.TryRemove(key, out var context))
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask CleanupExpiredAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _sessions)
        {
            if (entry.Value.ExpiresAt <= now && _sessions.TryRemove(entry.Key, out var context))
            {
                await context.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static string ComposeKey(string clientFingerprint, string sessionId) => $"{clientFingerprint}:{sessionId}".ToLowerInvariant();
}
