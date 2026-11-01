using Avalonia.Controls;
using PCL.Avalonia.Views.Pages;

namespace PCL.Avalonia.Navigation;

/// <summary>
///     页面键。命名与 WPF 端 PageXxx 类名一一对应（去掉 Page 前缀），
///     迁移期间可直接按类名找到对应 WPF 页面。
/// </summary>
public static class PageKeys
{
    public const string Home = "Home";
    public const string Loading = "Loading";
    public const string SpeedRight = "SpeedRight";
    public const string LogLeft = "LogLeft";
    public const string SelectLeft = "SelectLeft";
    public const string HelpDetail = "HelpDetail";
    public const string DownloadClient = "DownloadClient";
}

/// <summary>
///     页面键 → 页面工厂的注册表。路由层只依赖键，不直接构造页面，
///     后续迁移的新页面在此登记即可被导航到。
/// </summary>
public static class PageRegistry
{
    private static readonly Dictionary<string, Func<Control>> Factories =
        new(StringComparer.Ordinal);

    static PageRegistry()
    {
        Register(PageKeys.Home, () => new PageHome());
        Register(PageKeys.Loading, () => new PageLoading());
        Register(PageKeys.SpeedRight, () => new PageSpeedRight());
        Register(PageKeys.LogLeft, () => new PageLogLeft());
        Register(PageKeys.SelectLeft, () => new PageSelectLeft());
        Register(PageKeys.HelpDetail, () => new PageHelpDetail());
        Register(PageKeys.DownloadClient, () => new PageDownloadClient());
    }

    /// <summary>当前已注册的全部页面键。</summary>
    public static IReadOnlyCollection<string> Keys => Factories.Keys;

    public static void Register(string key, Func<Control> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        Factories[key] = factory;
    }

    public static bool IsRegistered(string key)
    {
        return Factories.ContainsKey(key);
    }

    public static bool TryCreate(string key, out Control page)
    {
        if (!Factories.TryGetValue(key, out var factory))
        {
            page = null!;
            return false;
        }

        page = factory();
        return true;
    }

    public static Control Create(string key)
    {
        return TryCreate(key, out var page)
            ? page
            : throw new KeyNotFoundException($"未注册的页面键：{key}");
    }
}