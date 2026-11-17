namespace PCL.Navigation;

/// <summary>由组合根创建页面宿主。页面描述只声明工厂，不直接依赖服务容器。</summary>
public sealed class PageHostFactory(IServiceProvider services)
{
    public IPageHost Create(PageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return descriptor.CreateHost?.Invoke(services, descriptor)
            ?? throw new InvalidOperationException($"页面没有 Host 工厂：{descriptor.Id}");
    }
}