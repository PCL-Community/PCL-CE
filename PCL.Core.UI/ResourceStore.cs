using System.Reflection;
using Avalonia.Platform;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于界面框架资源加载器（<c>avares://</c>）的打包资源访问实现。</summary>
public sealed class ResourceStore : IResourceStore
{
    /// <summary>入口程序集资源清单名的固定前缀，与入口 csproj 的 LogicalName 约定一致。</summary>
    private const string ManifestRoot = "PCL.";

    /// <summary>图片资源根 URI，界面框架使用 <c>avares://</c> 方案。</summary>
    public string GetImageRootUri()
    {
        var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "PCL";
        return $"avares://{assemblyName}/Images/";
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