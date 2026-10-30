using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>存档左侧栏页的视图模型。</summary>
public partial class PageInstanceSavesLeftViewModel : ViewModelBase
{
    [ObservableProperty] private bool _showDatapackTab = true;

    public static string GetUnknownSubPageMessage(FormMain.PageSubType id)
    {
        return Lang.Text("Instance.Saves.Left.UnknownSubPage", (int)id);
    }

    public static string GetSwitchFailedMessage(FormMain.PageSubType id)
    {
        return Lang.Text("Instance.Saves.Left.SwitchFailed", (int)id);
    }

    public static void OpenSaveFolder(string savePath)
    {
        Common.OpenExplorer($@"{savePath}\");
    }

    public static void HintRefreshing()
    {
        HintService.Hint(Lang.Text("Instance.Saves.Left.Refreshing"));
    }
}
