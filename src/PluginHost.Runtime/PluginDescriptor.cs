using PluginHost.Abstractions;

namespace PluginHost.Runtime;

public sealed class PluginDescriptor : IAsyncDisposable, IDisposable
{
    public PluginDescriptor(PluginMetadata metadata, PluginLoadContext loadContext, IPlugin instance)
    {
        Metadata = metadata;
        LoadContext = loadContext;
        Instance = instance;
    }

    public PluginMetadata Metadata { get; }
    public PluginLoadContext LoadContext { get; }
    public IPlugin Instance { get; }
    private bool _disposed;

    public void Dispose()
    {
        DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        switch (Instance)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        LoadContext.Dispose();
        _disposed = true;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            if (Instance is IDisposable disposable)
            {
                disposable.Dispose();
            }

            LoadContext.Dispose();
        }

        _disposed = true;
    }
}
