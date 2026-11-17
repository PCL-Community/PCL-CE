using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>实例设置页宿主：左栏为设置子页导航，右栏按子页描述缓存页面。</summary>
public sealed class InstanceSetupHost : IPageHost
{
    private readonly PageDescriptor _descriptor = PageCatalog.RequirePage(PageIds.InstanceSetup);
    private readonly Dictionary<SubPageId, Control> _rightPages = [];
    private PageInstanceLeft? _left;

    public PageId Page => PageIds.InstanceSetup;

    public MainTab? Tab => null;

    public double LeftWidth => _descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        var name = (stack.Parameter as McInstance)?.Name
            ?? PageInstanceLeft.ResolveInstance()?.Name
            ?? InstanceList.McMcInstanceSelected?.Name
            ?? string.Empty;
        return string.IsNullOrEmpty(name)
            ? Lang.Text("Main.Title.InstanceSelect")
            : Lang.Text("Main.Title.InstanceSetup", name);
    }

    public Control? GetLeft(PageRoute stack)
    {
        return _left ??= new PageInstanceLeft();
    }

    public Control GetRight(PageRoute stack)
    {
        var subType = _descriptor.NormalizeSubPage(stack.SubPage);
        if (_rightPages.TryGetValue(subType, out var cached))
            return cached;

        var subDescriptor = _descriptor.FindSubPage(subType);
        if (subDescriptor is null)
            throw new ArgumentOutOfRangeException(nameof(stack), subType, null);
        var page = subDescriptor.CreateRight(stack);
        _rightPages[subType] = page;
        return page;
    }
}