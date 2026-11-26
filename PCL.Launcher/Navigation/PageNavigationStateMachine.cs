namespace PCL.Navigation;

/// <summary>纯导航状态机：维护当前路由、二级页面返回栈和主 Tab 的历史位置。 不依赖 Avalonia 控件，便于在 headless 测试中验证导航行为。</summary>
public sealed class PageNavigationStateMachine
{
    private readonly List<PageRoute> _backStack = [];
    private readonly Dictionary<MainTab, PageRoute> _tabStates = [];

    public PageRoute Current { get; private set; } = new(PageIds.Launch);

    public IReadOnlyList<PageRoute> BackStack => _backStack;

    public bool CanGoBack => _backStack.Count > 0;

    public void Navigate(PageRoute next, bool isSecondary, MainTab? tab)
    {
        if (isSecondary)
        {
            if (_backStack.Contains(next))
                while (_backStack.Contains(next))
                    _backStack.RemoveAt(0);
            // 同页同参的子页切换不入栈，参数不同的仍然保留
            else if (!Current.Equals(next) && (Current.Page != next.Page || !Equals(Current.Parameter, next.Parameter)))
                _backStack.Insert(0, Current);
        }
        else
        {
            _backStack.Clear();
        }

        Current = next;
        if (tab is { } activeTab)
            _tabStates[activeTab] = next;
    }

    public PageRoute? GetTabState(MainTab tab)
    {
        return _tabStates.GetValueOrDefault(tab);
    }

    public bool Back(out PageRoute target)
    {
        if (_backStack.Count > 0)
        {
            target = _backStack[0];
            return true;
        }

        target = new PageRoute(PageIds.Launch);
        return false;
    }
}