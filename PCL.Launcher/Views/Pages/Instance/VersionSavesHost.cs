using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>存档管理页宿主：左栏为存档信息/数据包切换，右栏按子页描述缓存； 导航参数为存档文件夹绝对路径。</summary>
public sealed class VersionSavesHost : IPageHost
{
    private readonly PageDescriptor _descriptor = PageCatalog.RequirePage(PageIds.VersionSaves);
    private readonly Dictionary<SubPageId, Control> _rightPages = [];
    private PageInstanceSavesLeft? _left;

    public PageId Page => PageIds.VersionSaves;

    public MainTab? Tab => null;

    public double LeftWidth => _descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        var saveFolder = stack.Parameter as string
            ?? PageInstanceSavesLeft.CurrentSave;
        var name = GetSaveName(saveFolder);
        return string.IsNullOrEmpty(name)
            ? Lang.Text("Main.Title.SaveManagement", string.Empty)
            : Lang.Text("Main.Title.SaveManagement", name);
    }

    public Control? GetLeft(PageRoute stack)
    {
        ApplySaveFolder(stack);
        return _left ??= new PageInstanceSavesLeft();
    }

    public Control GetRight(PageRoute stack)
    {
        ApplySaveFolder(stack);
        var subType = _descriptor.NormalizeSubPage(stack.SubPage);
        if (_rightPages.TryGetValue(subType, out var cached))
            return cached;

        var descriptor = _descriptor.FindSubPage(subType);
        if (descriptor is null)
            throw new ArgumentOutOfRangeException(nameof(stack), subType, null);
        var page = descriptor.CreateRight(stack);
        _rightPages[subType] = page;
        return page;
    }

    private static void ApplySaveFolder(PageRoute stack)
    {
        if (stack.Parameter is string { Length: > 0 } saveFolder)
            PageInstanceSavesLeft.CurrentSave = saveFolder;
    }

    private static string GetSaveName(string? saveFolder)
    {
        return string.IsNullOrWhiteSpace(saveFolder)
            ? string.Empty
            : Path.GetFileName(saveFolder.TrimEnd(Path.DirectorySeparatorChar));
    }
}