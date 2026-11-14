using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Log;

/// <summary>游戏实时日志页宿主：左栏为已打开的游戏日志列表，右栏为选中日志的正文。</summary>
public sealed class GameLogHost : IPageHost
{
    private readonly PageLogLeft _left = new();
    private PageLogRight? _right;

    public PageType Page => PageType.GameLog;

    public MainTab? Tab => null;

    public double LeftWidth => 300d;

    public string GetTitle(PageStackData stack)
    {
        return Lang.Text("Main.Title.GameLog");
    }

    public Control? GetLeft(PageStackData stack)
    {
        return _left;
    }

    public Control GetRight(PageStackData stack)
    {
        return _right ??= new PageLogRight();
    }
}