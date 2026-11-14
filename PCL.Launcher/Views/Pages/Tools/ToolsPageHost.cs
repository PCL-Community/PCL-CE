using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Tools;

/// <summary>
///     联机与工具页宿主：左栏常驻一份，右栏按 <see cref="PageSubType" /> 缓存
///     <see cref="PageSubType.ToolsGameLink" /> 与 <see cref="PageSubType.ToolsTest" /> 两个子页实例，
///     往返切换不丢状态。默认定位为联机大厅。
/// </summary>
public sealed class ToolsPageHost : IPageHost
{
    private readonly PageToolsLeft _left;
    private readonly Dictionary<PageSubType, Control> _rightPages = new();

    public ToolsPageHost()
    {
        _left = new PageToolsLeft();
        _left.PageSelected += sub => PageRouter.Navigate(PageType.Tools, sub);
    }

    public PageType Page => PageType.Tools;

    public MainTab? Tab => MainTab.Tools;

    public double LeftWidth => 150d;

    public string GetTitle(PageStackData stack)
    {
        return NormalizeSubPage(stack.SubType) == PageSubType.ToolsTest
            ? Lang.Text("Tools.Left.Utilities.Title")
            : Lang.Text("Tools.Left.Lobby");
    }

    public Control? GetLeft(PageStackData stack)
    {
        var sub = NormalizeSubPage(stack.SubType);
        _left.Select(sub);
        return _left;
    }

    public Control GetRight(PageStackData stack)
    {
        var sub = NormalizeSubPage(stack.SubType);
        if (_rightPages.TryGetValue(sub, out var cached))
            return cached;

        Control page = sub switch
        {
            PageSubType.ToolsTest => new PageToolsTest(),
            _ => new PageToolsGameLink()
        };
        _rightPages[sub] = page;
        return page;
    }

    private static PageSubType NormalizeSubPage(PageSubType sub)
    {
        if (sub is not (PageSubType.ToolsGameLink or PageSubType.ToolsTest))
            sub = PageSubType.ToolsGameLink;
        return PageToolsLeft.IsSubPageHidden(sub)
            ? PageToolsLeft.FirstVisible()
            : sub;
    }
}