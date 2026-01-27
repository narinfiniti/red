using PluginHost.Security;

namespace PluginHost.Api.Services;

public sealed class SessionCleanupHostedService : BackgroundService
{
    private readonly ISessionStore _sessionStore;

    public SessionCleanupHostedService(ISessionStore sessionStore)
    {
        _sessionStore = sessionStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _sessionStore.CleanupExpiredAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }
}
