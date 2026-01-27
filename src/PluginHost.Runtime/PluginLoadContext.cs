using System.Reflection;
using System.Runtime.Loader;

namespace PluginHost.Runtime;

public sealed class PluginLoadContext : AssemblyLoadContext, IDisposable
{
    private readonly AssemblyDependencyResolver _resolver;
    private bool _disposed;

    public PluginLoadContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unload();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
