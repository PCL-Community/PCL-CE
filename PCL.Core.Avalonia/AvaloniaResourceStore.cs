using System.Reflection;
using Avalonia.Platform;
using PCL.Core.Platform;

namespace PCL.Core.Avalonia;

/// <summary>基于 Avalonia 资源加载器（<c>avares://</c>）的打包资源访问实现。</summary>
public sealed class AvaloniaResourceStore : IResourceStore
{
    /// <summary>图片资源根 URI，Avalonia 使用 <c>avares://</c> 方案。</summary>
    public string GetImageRootUri()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "PCL.Launcher.Avalonia";
        return $"avares://{assemblyName}/Images/";
    }

    public Stream? GetResourceStream(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return null;

        var normalized = path.Replace('\\', '/').TrimStart('/');

        // WPF 头直接使用 pack:// 清单资源；Avalonia 头按 PCL.Resources.xxx 的清单名打包
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly is not null)
        {
            var manifestPath = normalized.Replace('/', '.');
            foreach (var candidate in new[]
                     {
                         $"PCL.{manifestPath}",
                         $"{assemblyName}.{manifestPath}",
                         manifestPath,
                         normalized
                     })
                if (entryAssembly.GetManifestResourceStream(candidate) is { } stream)
                    return stream;
        }

        try
        {
            var uri = new Uri($"avares://{assemblyName}/{normalized}");
            return AssetLoader.Exists(uri) ? AssetLoader.Open(uri) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}