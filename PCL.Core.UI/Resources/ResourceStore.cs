using System.Reflection;
using Avalonia.Platform;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于界面框架资源加载器（<c>avares://</c>）的打包资源访问实现。</summary>
public sealed class ResourceStore : IResourceStore
{
    /// <summary>入口程序集资源清单名的固定前缀，与入口 csproj 的 LogicalName 约定一致。</summary>
    private const string ManifestRoot = "PCL.";

    private const string AssetRoot = "Assets/";

    public string GetImageRootUri()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "PCL.Launcher";
        return $"avares://{assemblyName}/{AssetRoot}Images/";
    }

    public Stream? GetResourceStream(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return null;

        var normalized = path.Replace('\\', '/').TrimStart('/');

        // 入口程序集按 <ManifestRoot><相对路径> 的清单名打包启动链资源（见 PCL.Launcher.csproj）
        if (Assembly.GetEntryAssembly() is { } entryAssembly)
        {
            var candidate = ManifestRoot + normalized.Replace('/', '.');
            if (entryAssembly.GetManifestResourceStream(candidate) is { } stream)
                return stream;
        }

        if (TryOpenAvares(assemblyName, normalized) is { } direct)
            return direct;
        if (!normalized.StartsWith(AssetRoot, StringComparison.OrdinalIgnoreCase)
            && TryOpenAvares(assemblyName, AssetRoot + normalized) is { } asset)
            return asset;

        return null;
    }

    private static Stream? TryOpenAvares(string assemblyName, string path)
    {
        try
        {
            var uri = new Uri($"avares://{assemblyName}/{path}");
            return AssetLoader.Exists(uri) ? AssetLoader.Open(uri) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}