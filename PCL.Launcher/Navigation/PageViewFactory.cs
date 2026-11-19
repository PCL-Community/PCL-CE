using Avalonia.Controls;

namespace PCL.Navigation;

/// <summary>通过组合根创建页面控件；路由数据由调用方显式传入。</summary>
public sealed class PageViewFactory(IServiceProvider services)
{
    public Control Create(SubPageDescriptor descriptor, PageRoute route)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.CreateRight(services, route);
    }
}