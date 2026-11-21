namespace PCL.Navigation;

/// <summary>一个功能模块提供的页面描述和主入口描述。</summary>
public interface IPageModule
{
    IReadOnlyList<PageDescriptor> Pages { get; }

    IReadOnlyList<MainTabDescriptor> MainTabs => [];
}

/// <summary>页面目录快照：构建后只读，供路由、标题栏和左栏共同使用。</summary>
public sealed record PageCatalogSnapshot(
    IReadOnlyList<PageDescriptor> Pages,
    IReadOnlyList<MainTabDescriptor> MainTabs);