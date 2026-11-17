using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

internal sealed class PageDownloadCompPage : MyPageRight, IPageView, IRefreshable
{
    private readonly PageComp _comp;
    private CompSearchContext? _appliedContext;

    public PageDownloadCompPage(Comp.CompType type)
    {
        _comp = new PageComp
        {
            PageIds = type,
            SupportModrinth = type != Comp.CompType.World
        };
        _comp.SetCategoryTags(CompCategoryTags.For(type));
        _comp.CompDetailRequested += OnCompDetailRequested;
        Child = _comp;
        PanScroll = _comp.ScrollViewer;
        ShowAlwaysWhileLoading = false;

        PageLoaderInit(
            _comp.LoadingIndicator,
            _comp.LoadingCard,
            _comp.ContentPanel,
            _comp.AlwaysPanel,
            _comp.PageLoader,
            inputInvoke: () => _comp.BuildLoaderInput(),
            autoRun: false);
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        _comp.ScrollViewer.ScrollToHome();
        _comp.RefreshFavoriteStatuses();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Refresh()
    {
        _comp.ScrollViewer.ScrollToHome();
        _comp.RefreshFavoriteStatuses();
        _comp.RefreshSearch();
    }

    public void PrepareNavigation(PageParameter? parameter)
    {
        var context = parameter is CompSearchContext searchContext ? searchContext : null;
        if (_comp.IsInitialized)
        {
            if (context is not null && context != _appliedContext)
            {
                _appliedContext = context;
                _comp.ApplySearchContext(context.TargetGameVersion, context.TargetModLoader);
            }

            return;
        }

        _appliedContext = context;
        _comp.InitializeForLoad(context);
    }

    private static void OnCompDetailRequested(CompDetailParameter context)
    {
        try
        {
            if (!PageRegistry.IsRegistered(PageIds.CompDetail))
            {
                LauncherCommon.Log($"资源详情页未注册，无法打开 {context.Project?.Slug}");
                return;
            }

            PageRouter.Navigate(PageIds.CompDetail, null, context);
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