using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Download;

/// <summary>
/// 资源详情页宿主：二级页面，无左栏，右页复用同一份 <see cref="PageDownloadCompDetail" /> 实例。 导航参数约定为
/// <see cref="Comp.CompProject" /> 或 <see cref="CompDetailParameter" />。
/// </summary>
public sealed class CompDetailHost(PageDownloadCompDetail right) : IPageHost
{
    public PageId Page => PageIds.CompDetail;

    public MainTab? Tab => null;

    public double LeftWidth => 0d;

    public string GetTitle(PageRoute stack)
    {
        return stack.Parameter switch
        {
            Comp.CompProject project => GetDetailTitle(project),
            CompDetailParameter context => GetDetailTitle(context.Project),
            _ => Lang.Text("Download.Left.CommunityResources")
        };
    }

    public Control? GetLeft(PageRoute stack)
    {
        return null;
    }

    public Control GetRight(PageRoute stack)
    {
        right.PrepareNavigation(stack.Parameter);
        return right;
    }

    private static string GetDetailTitle(Comp.CompProject project)
    {
        return Lang.Text("Main.Title.ResourceDownload", project.TranslatedName);
    }
}