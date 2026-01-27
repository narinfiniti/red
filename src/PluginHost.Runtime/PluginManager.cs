using System.Collections.Concurrent;
using System.Reflection;
using PluginHost.Abstractions;

namespace PluginHost.Runtime;

public sealed class PluginManager : IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, PluginDescriptor> _plugins = new();
    private readonly string _pluginStoragePath;
    private bool _disposed;

    public PluginManager(string pluginStoragePath)
    {
        _pluginStoragePath = pluginStoragePath;
        Directory.CreateDirectory(_pluginStoragePath);
    }

    public IReadOnlyCollection<PluginMetadata> List()
        => _plugins.Values.Select(p => p.Metadata).ToArray();

    public async Task<PluginMetadata> LoadAsync(string pluginName, byte[] assemblyBytes, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(pluginName))
        {
            throw new ArgumentException("Plugin name is required.", nameof(pluginName));
        }

        if (assemblyBytes.Length == 0)
        {
            throw new InvalidOperationException("Assembly payload is empty.");
        }

        // Unload (and delete) any existing plugin with this name before writing the new DLL.
        await UnloadByNameAsync(pluginName, ct).ConfigureAwait(false);

        var pluginPath = Path.Combine(_pluginStoragePath, pluginName);
        Directory.CreateDirectory(pluginPath);
        var assemblyPath = Path.Combine(pluginPath, $"{pluginName}.dll");

        await File.WriteAllBytesAsync(assemblyPath, assemblyBytes, ct).ConfigureAwait(false);

        ValidateAssemblyFile(assemblyPath);

        var loadContext = new PluginLoadContext(assemblyPath);
        IPlugin? plugin;
        try
        {
            using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
            var assembly = loadContext.LoadFromStream(assemblyStream);
            var pluginType = FindPluginType(assembly);
            plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin is null)
            {
                throw new InvalidOperationException("Failed to create plugin instance.");
            }

            if (!string.Equals(plugin.Name, pluginName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Plugin name mismatch.");
            }
        }
        catch
        {
            loadContext.Dispose();
            throw;
        }

        var metadata = new PluginMetadata(
            plugin.Name,
            plugin.TargetOS,
            plugin.SupportedVersion,
            assemblyPath,
            DateTimeOffset.UtcNow);

        var key = BuildKey(plugin.Name, plugin.TargetOS, plugin.SupportedVersion);

        _plugins[key] = new PluginDescriptor(metadata, loadContext, plugin);
        return metadata;
    }

    public async Task<bool> UnloadAsync(string name, string targetOs, string version, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var key = BuildKey(name, targetOs, version);
        if (!_plugins.TryRemove(key, out var descriptor))
        {
            return false;
        }

        var assemblyPath = descriptor.Metadata.AssemblyPath;
        var pluginDirectory = Path.GetDirectoryName(assemblyPath);

        await descriptor.DisposeAsync().ConfigureAwait(false);

        await DeleteFileWithRetriesAsync(assemblyPath, ct).ConfigureAwait(false);
        TryDeleteEmptyDirectory(pluginDirectory);
        return true;
    }

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

    public async Task<string> ExecuteAsync(string targetOs, string version, string command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var match = _plugins.Values.FirstOrDefault(p =>
            string.Equals(p.Metadata.TargetOS, targetOs, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Metadata.SupportedVersion, version, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new InvalidOperationException("No compatible plugin found.");
        }

        try
        {
            return await match.Instance.ExecuteAsync(command, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException("Plugin execution failed.");
        }
    }

    private static Type FindPluginType(Assembly assembly)
    {
        var pluginType = assembly
            .GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass);

        if (pluginType is null)
        {
            throw new InvalidOperationException("No IPlugin implementation found.");
        }

        return pluginType;
    }

    private static string BuildKey(string name, string targetOs, string version)
        => $"{name}:{targetOs}:{version}".ToLowerInvariant();

    private static void ValidateAssemblyFile(string assemblyPath)
    {
        try
        {
            AssemblyName.GetAssemblyName(assemblyPath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Invalid plugin assembly.", ex);
        }
    }

    private static async Task DeleteFileWithRetriesAsync(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == maxAttempts)
                {
                    throw new InvalidOperationException($"Failed to delete plugin assembly file '{path}'.", ex);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), ct).ConfigureAwait(false);
            }
        }
    }

    private static void TryDeleteEmptyDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                return;
            }

            Directory.Delete(directoryPath, recursive: false);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private async Task UnloadByNameAsync(string pluginName, CancellationToken ct)
    {
        var matches = _plugins.Values
            .Where(p => string.Equals(p.Metadata.Name, pluginName, StringComparison.Ordinal))
            .Select(p => p.Metadata);

        foreach (var metadata in matches)
        {
            await UnloadAsync(metadata.Name, metadata.TargetOS, metadata.SupportedVersion, ct)
                .ConfigureAwait(false);
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_disposed)
        {
            return;
        }

        var descriptors = _plugins.Values.ToArray();
        _plugins.Clear();

        foreach (var descriptor in descriptors)
        {
            await using var _ = descriptor;
        }

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
            foreach (var descriptor in _plugins.Values)
            {
                using var _ = descriptor;
            }

            _plugins.Clear();
        }

        _disposed = true;
    }
}
