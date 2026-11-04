using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     存档管理页宿主（<see cref="PageType.VersionSaves" />）：左栏为存档信息/数据包切换，
///     右栏按子页种类缓存；导航参数为存档文件夹绝对路径。
/// </summary>
public sealed class VersionSavesHost : IPageHost
{
    private readonly Dictionary<PageSubType, Control> _rightPages = [];
    private PageInstanceSavesLeft? _left;

    public PageType Page => PageType.VersionSaves;

    public MainTab? Tab => null;

    public double LeftWidth => 210d;

    public string GetTitle(PageStackData stack)
    {
        var saveFolder = stack.Parameter as string
                         ?? PageInstanceSavesLeft.currentSave;
        var name = GetSaveName(saveFolder);
        return string.IsNullOrEmpty(name)
            ? Lang.Text("Main.Title.SaveManagement", string.Empty)
            : Lang.Text("Main.Title.SaveManagement", name);
    }

    public Control? GetLeft(PageStackData stack)
    {
        ApplySaveFolder(stack);
        return _left ??= new PageInstanceSavesLeft();
    }

    public Control GetRight(PageStackData stack)
    {
        ApplySaveFolder(stack);
        var subType = stack.SubType;
        if (_rightPages.TryGetValue(subType, out var cached))
            return cached;

        Control page = subType switch
        {
            PageSubType.VersionSavesDatapack => new PageInstanceSavesDatapack(),
            _ => new PageInstanceSavesInfo()
        };
        _rightPages[subType] = page;
        return page;
    }

    private static void ApplySaveFolder(PageStackData stack)
    {
        if (stack.Parameter is string { Length: > 0 } saveFolder)
            PageInstanceSavesLeft.currentSave = saveFolder;
    }

    private static string GetSaveName(string? saveFolder)
    {
        if (string.IsNullOrWhiteSpace(saveFolder))
            return string.Empty;
        return Path.GetFileName(saveFolder.TrimEnd(Path.DirectorySeparatorChar));
    }
}
