using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Instance;

/// <summary>实例设置页宿主：左栏为设置子页导航，右栏按子页种类缓存页面。</summary>
public sealed class InstanceSetupHost : IPageHost
{
    private readonly Dictionary<PageSubType, Control> _rightPages = [];
    private PageInstanceLeft? _left;

    public PageType Page => PageType.InstanceSetup;

    public MainTab? Tab => null;

    public double LeftWidth => 210d;

    public string GetTitle(PageStackData stack)
    {
        var name = (stack.Parameter as McInstance)?.Name
                   ?? PageInstanceLeft.ResolveInstance()?.Name
                   ?? InstanceList.McMcInstanceSelected?.Name
                   ?? string.Empty;
        return string.IsNullOrEmpty(name)
            ? Lang.Text("Main.Title.InstanceSelect")
            : Lang.Text("Main.Title.InstanceSetup", name);
    }

    public Control? GetLeft(PageStackData stack) => _left ??= new PageInstanceLeft();

    public Control GetRight(PageStackData stack)
    {
        var subType = stack.SubType;
        if (_rightPages.TryGetValue(subType, out var cached))
            return cached;

        var page = CreatePage(subType, stack.Parameter as McInstance);
        _rightPages[subType] = page;
        return page;
    }

    private static Control CreatePage(PageSubType subType, McInstance? instance)
    {
        switch (subType)
        {
            case PageSubType.VersionOverall:
                return new PageInstanceOverall();
            case PageSubType.VersionSetup:
                return new PageInstanceSetup();
            case PageSubType.VersionMod:
                return new PageInstanceCompResource(Comp.CompType.Mod);
            case PageSubType.VersionResourcePack:
                return new PageInstanceCompResource(Comp.CompType.ResourcePack);
            case PageSubType.VersionShader:
                return new PageInstanceCompResource(Comp.CompType.Shader);
            case PageSubType.VersionSchematic:
                return new PageInstanceCompResource(Comp.CompType.Schematic);
            case PageSubType.VersionModDisabled:
                return new PageInstanceModDisabled();
            case PageSubType.VersionWorld:
                return new PageInstanceSaves();
            case PageSubType.VersionModJarInJar:
                return new PageInstanceCompJarInJar();
            case PageSubType.VersionInstall:
                return new PageInstanceInstall();
            case PageSubType.VersionExport:
                return new PageInstanceExport();
            case PageSubType.VersionScreenshot:
                return new PageInstanceScreenshot();
            case PageSubType.VersionServer:
                return new PageInstanceServer();
            default:
                throw new ArgumentOutOfRangeException(nameof(subType), subType, null);
        }
    }
}
