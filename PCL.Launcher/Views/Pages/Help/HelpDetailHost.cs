using Avalonia.Controls;
using PCL.Navigation;

namespace PCL.Views.Pages.Help;

/// <summary>帮助详情页宿主：二级页面，无固定左栏，标题取导航给出的标题覆盖或内容的标题。</summary>
public sealed class HelpDetailHost : IPageHost
{
    private readonly PageHelpDetail _right;

    public HelpDetailHost(PageHelpDetail right)
    {
        _right = right;
        _right.TitleChanged += _ => PageRouter.RefreshState();
    }

    public PageId Page => PageIds.HelpDetail;

    public MainTab? Tab => null;

    public double LeftWidth => 0d;

    public string GetTitle(PageRoute stack)
    {
        return _right is { } page && !string.IsNullOrWhiteSpace(page.Title)
            ? page.Title
            : !string.IsNullOrWhiteSpace(stack.TitleOverride)
                ? stack.TitleOverride!
                : string.Empty;
    }

    public Control? GetLeft(PageRoute stack)
    {
        return null;
    }

    public Control GetRight(PageRoute stack)
    {
        return _right;
    }
}