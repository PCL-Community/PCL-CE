using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Select;

/// <summary>实例选择页宿主：左栏为 Minecraft 文件夹列表，右栏为实例卡片列表。</summary>
public sealed class InstanceSelectHost(
    PageSelectLeft left,
    PageSelectRight right) : IPageHost
{
    public PageId Page => PageIds.InstanceSelect;

    public MainTab? Tab => null;

    public double LeftWidth => 300d;

    public string GetTitle(PageRoute stack)
    {
        return Lang.Text("Main.Title.InstanceSelect");
    }

    public Control? GetLeft(PageRoute stack)
    {
        return left;
    }

    public Control GetRight(PageRoute stack)
    {
        return right;
    }
}