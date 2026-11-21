using PCL.Core.Logging;

namespace PCL.Navigation;

/// <summary>页面宿主注册表：由 <see cref="PageCatalog" /> 的页面描述创建宿主， 不再反射扫描程序集，也不再依赖无参构造函数。</summary>
public static class PageRegistry
{
    private static readonly Dictionary<PageId, IPageHost> Hosts = new();
    private static readonly Lock Lock = new();
    private static PageHostFactory? _factory;
    private static bool _initialized;

    public static IReadOnlyList<IPageHost> All
    {
        get
        {
            lock (Lock)
            {
                EnsureInitializedCore();
                return
                [
                    .. Hosts
                        .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                        .Select(pair => pair.Value)
                ];
            }
        }
    }

    /// <summary>由组合根注入页面宿主工厂。必须在首次页面导航之前调用。</summary>
    public static void Initialize(PageHostFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        lock (Lock)
        {
            _factory = factory;
            Hosts.Clear();
            _initialized = false;
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

    public static IPageHost? GetHost(PageId page)
    {
        lock (Lock)
        {
            EnsureInitializedCore();
            return Hosts.GetValueOrDefault(page);
        }
    }

    public static bool IsRegistered(PageId page)
    {
        return GetHost(page) is not null;
    }

    public static PageId PageOfTab(MainTab tab)
    {
        return PageCatalog.PageOfTab(tab);
    }

    public static MainTab? TabOfPage(PageId page)
    {
        return PageCatalog.TabOfPage(page);
    }

    private static void EnsureInitializedCore()
    {
        if (_initialized) return;
        _initialized = true;

        var factory = _factory
            ?? throw new InvalidOperationException("页面宿主工厂尚未初始化");
        foreach (var descriptor in PageCatalog.Pages)
            try
            {
                var host = factory.Create(descriptor);
                if (host is null)
                {
                    LogWrapper.Warn("UI", $"页面描述未提供宿主：{descriptor.Id}");
                    continue;
                }

                if (!Hosts.TryAdd(host.Page, host))
                    LogWrapper.Warn("UI", $"页面宿主重复注册：{host.Page}");
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "UI", $"创建页面宿主失败：{descriptor.Id}");
            }

        if (Hosts.Count == 0)
            LogWrapper.Error("UI", "页面目录中没有可用的页面宿主");
    }
}