using Avalonia.Controls;
using PCL.Navigation;

namespace PCL.Views.Pages.Help;

/// <summary>帮助详情页宿主：二级页面，无固定左栏，标题取导航给出的标题覆盖或内容的标题。</summary>
public sealed class HelpDetailHost : IPageHost
{
    private PageHelpDetail? _right;

    public PageType Page => PageType.HelpDetail;

    public MainTab? Tab => null;

    public double LeftWidth => 0d;

    public string GetTitle(PageStackData stack)
    {
        if (!string.IsNullOrWhiteSpace(stack.TitleOverride))
            return stack.TitleOverride!;
        return _right?.Title ?? string.Empty;
    }

    public Control? GetLeft(PageStackData stack)
    {
        return null;
    }

    public Control GetRight(PageStackData stack)
    {
        return _right ??= new PageHelpDetail();
    }
}