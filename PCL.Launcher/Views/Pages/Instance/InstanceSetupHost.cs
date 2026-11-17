using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>实例设置页宿主：左栏为设置子页导航，右栏按子页描述缓存页面。</summary>
public sealed class InstanceSetupHost(
    PageDescriptor descriptor,
    PageInstanceLeft left,
    PageViewFactory pageViews) : IPageHost
{
    private readonly Dictionary<SubPageId, Control> _rightPages = [];

    public PageId Page => PageIds.InstanceSetup;

    public MainTab? Tab => null;

    public double LeftWidth => descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        var name = (stack.Parameter is McInstance instance ? instance.Name : null)
            ?? PageInstanceLeft.ResolveInstance()?.Name
            ?? InstanceList.McMcInstanceSelected?.Name
            ?? string.Empty;
        return string.IsNullOrEmpty(name)
            ? Lang.Text("Main.Title.InstanceSelect")
            : Lang.Text("Main.Title.InstanceSetup", name);
    }

    public Control? GetLeft(PageRoute stack)
    {
        return left;
    }

    public Control GetRight(PageRoute stack)
    {
        var subType = descriptor.NormalizeSubPage(stack.SubPage);
        if (_rightPages.TryGetValue(subType, out var cached))
            return cached;

        var subDescriptor = descriptor.FindSubPage(subType);
        if (subDescriptor is null)
            throw new ArgumentOutOfRangeException(nameof(stack), subType, null);
        var page = pageViews.Create(subDescriptor, stack);
        _rightPages[subType] = page;
        return page;
    }
}