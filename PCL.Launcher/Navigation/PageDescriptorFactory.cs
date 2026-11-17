using Avalonia.Controls;

namespace PCL.Navigation;

/// <summary>页面描述工厂：让功能模块共享创建逻辑，但不需要依赖中央 <see cref="PageCatalog" />。</summary>
public static class PageDescriptorFactory
{
    public static SubPageDescriptor Sub(
        PageId parent,
        SubPageId id,
        string titleKey,
        string? iconKey,
        string? groupKey,
        int order,
        Func<IServiceProvider, PageRoute, Control> create,
        Func<bool>? visible = null,
        bool refresh = false,
        string? reset = null,
        double logoScale = 1d)
    {
        return id.Parent != parent
            ? throw new ArgumentException("子页面 ID 不属于父页面", nameof(id))
            : new SubPageDescriptor(
                id,
                titleKey,
                iconKey,
                groupKey,
                order,
                create,
                visible,
                refresh,
                reset,
                logoScale);
    }
}