using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

internal sealed class PageDownloadCompPage : MyPageRight, IPageView, IRefreshable
{
    private readonly PageComp _comp;
    private readonly Comp.CompType _type;

    public PageDownloadCompPage(Comp.CompType type)
    {
        _type = type;
        _comp = new PageComp
        {
            PageType = type,
            SupportModrinth = type != Comp.CompType.World
        };
        _comp.SetCategoryTags(CompCategoryTags.For(type));
        _comp.CompDetailRequested += OnCompDetailRequested;
        Child = _comp;
        PanScroll = _comp.ScrollViewer;
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _comp.ScrollViewer.ScrollToHome();
        _comp.RefreshFavoriteStatuses();
        if (parameter is CompSearchContext context)
            _comp.ApplySearchContext(context.TargetGameVersion, context.TargetModLoader);
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        _comp.RefreshSearch();
    }

    private void OnCompDetailRequested(CompDetailParameter context)
    {
        try
        {
            if (!PageRegistry.IsRegistered(PageType.CompDetail))
            {
                LauncherCommon.Log($"资源详情页未注册，无法打开 {context.Project?.Slug}");
                return;
            }

            PageRouter.Navigate(PageType.CompDetail, PageSubType.Default, context);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开资源详情失败");
        }
    }
}

/// <summary>从实例页等处进入资源搜索页时携带的筛选上下文。</summary>
/// <param name="TargetGameVersion">目标 Minecraft 版本；为空表示不限制。</param>
/// <param name="TargetModLoader">目标模组加载器。</param>
public sealed record CompSearchContext(
    string? TargetGameVersion,
    Comp.CompLoaderType TargetModLoader);