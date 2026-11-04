using Avalonia.Controls;
using PCL.Navigation;

namespace PCL.Views.Pages.Setup;

public sealed class PageSetupHost : IPageHost
{
    private readonly PageSetupLeft _left;
    private readonly Dictionary<PageSubType, Control> _rightPages = new();

    public PageSetupHost()
    {
        _left = new PageSetupLeft();
        _left.PageSelected += sub => PageRouter.Navigate(PageType.Setup, sub);
        _left.AttachPageProvider(GetOrCreatePage);
    }

    public PageType Page => PageType.Setup;

    public MainTab? Tab => MainTab.Setup;

    public double LeftWidth => 152d;

    public string GetTitle(PageStackData stack)
    {
        return string.Empty;
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
        return GetOrCreatePage(sub)!;
    }

    private Control? GetOrCreatePage(PageSubType sub)
    {
        sub = NormalizeSubPage(sub);
        if (_rightPages.TryGetValue(sub, out var cached))
            return cached;

        var page = CreatePage(sub);
        _rightPages[sub] = page;
        return page;
    }

    private static PageSubType NormalizeSubPage(PageSubType sub)
    {
        if (sub == PageSubType.Default)
            sub = PageSubType.SetupLaunch;
        if (PageSetupLeft.IsSubPageHidden(sub))
            return PageSetupLeft.FirstVisible();
        return sub;
    }

    private static Control CreatePage(PageSubType sub)
    {
        return sub switch
        {
            PageSubType.SetupUI => new PageSetupUI(),
            PageSubType.SetupGameManage => new PageSetupGameManage(),
            PageSubType.SetupAbout => new PageSetupAbout(),
            PageSubType.SetupLog => new PageSetupLog(),
            PageSubType.SetupFeedback => new PageSetupFeedback(),
            PageSubType.SetupGameLink => new PageSetupGameLink(),
            PageSubType.SetupUpdate => new PageSetupUpdate(),
            PageSubType.SetupJava => new PageSetupJava(),
            PageSubType.SetupLauncherMisc => new PageSetupLauncherMisc(),
            PageSubType.SetupLauncherLanguage => new PageSetupLauncherLanguage(),
            _ => new PageSetupLaunch()
        };
    }
}