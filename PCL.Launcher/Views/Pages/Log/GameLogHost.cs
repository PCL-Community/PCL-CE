using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Log;

/// <summary>游戏实时日志页宿主：左栏为已打开的游戏日志列表，右栏为选中日志的正文。</summary>
public sealed class GameLogHost : IPageHost
{
    private readonly PageLogLeft _left;
    private readonly PageLogRight _right;

    public GameLogHost(PageLogLeft left, PageLogRight right)
    {
        _left = left;
        _right = right;
    }

    public PageId Page => PageIds.GameLog;

    public MainTab? Tab => null;

    public double LeftWidth => 300d;

    public string GetTitle(PageRoute stack)
    {
        return Lang.Text("Main.Title.GameLog");
    }

    public Control? GetLeft(PageRoute stack)
    {
        return _left;
    }

    public Control GetRight(PageRoute stack)
    {
        return _right;
    }
}