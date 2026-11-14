using System.Reflection;
using PCL.Core.Logging;

namespace PCL.Navigation;

/// <summary>
///     页面宿主注册表：在首次使用时扫描程序集中的 <see cref="IPageHost" /> 实现并实例化，
///     新增页面只需在页面目录实现宿主，不需要修改此文件。
/// </summary>
public static class PageRegistry
{
    private static readonly Dictionary<PageType, IPageHost> Hosts = new();
    private static readonly Lock Lock = new();
    private static bool _initialized;

    public static IReadOnlyList<IPageHost> All
    {
        get
        {
            lock (Lock)
            {
                EnsureInitializedCore();
                return Hosts.OrderBy(pair => (int)pair.Key).Select(pair => pair.Value).ToList();
            }
        }
    }

    public static void Register(IPageHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        lock (Lock)
        {
            EnsureInitializedCore();
            Hosts[host.Page] = host;
        }
    }

    public static IPageHost? GetHost(PageType page)
    {
        lock (Lock)
        {
            EnsureInitializedCore();
            return Hosts.GetValueOrDefault(page);
        }
    }

    public static bool IsRegistered(PageType page)
    {
        return GetHost(page) is not null;
    }

    public static PageType PageOfTab(MainTab tab)
    {
        return tab switch
        {
            MainTab.Launch => PageType.Launch,
            MainTab.Download => PageType.Download,
            MainTab.Setup => PageType.Setup,
            MainTab.Tools => PageType.Tools,
            _ => PageType.Launch
        };
    }

    public static MainTab? TabOfPage(PageType page)
    {
        return page switch
        {
            PageType.Launch => MainTab.Launch,
            PageType.Download => MainTab.Download,
            PageType.Setup => MainTab.Setup,
            PageType.Tools => MainTab.Tools,
            _ => null
        };
    }

    private static void EnsureInitializedCore()
    {
        if (_initialized) return;
        _initialized = true;

        var assembly = typeof(PageRegistry).Assembly;
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(type => type is not null).Cast<Type>().ToArray();
        }

        foreach (var type in types.OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            if (type.IsAbstract || type.IsInterface || !type.IsClass) continue;
            if (!typeof(IPageHost).IsAssignableFrom(type)) continue;
            if (type.GetConstructor(Type.EmptyTypes) is null) continue;
            try
            {
                var host = (IPageHost)Activator.CreateInstance(type)!;
                if (!Hosts.TryAdd(host.Page, host))
                    LogWrapper.Warn("UI", $"页面宿主重复注册：{host.Page}（{type.Name}）");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "UI", $"创建页面宿主失败：{type.FullName}");
            }
        }

        if (Hosts.Count == 0)
            LogWrapper.Error("UI", "未发现任何页面宿主，界面将无法导航");
    }
}