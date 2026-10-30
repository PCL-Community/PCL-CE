using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>Minecraft 文件夹选择页的视图模型，承载首次加载标记与文件夹菜单的判定逻辑。</summary>
public partial class PageSelectLeftViewModel : ViewModelBase
{
    [ObservableProperty] private bool _isFirstLoad = true;

    public static bool IsFirstItem(int index)
    {
        return index == 0;
    }

    public static bool IsLastItem(int index, int count)
    {
        return index == count - 1;
    }

    public static string GetDeleteMenuText(GameFolders.McFolder folder, int folderCount)
    {
        return folderCount == 1 && folder.Location == Path.Combine(Common.ExePath, ".minecraft") + @"\"
            ? Lang.Text("Select.Folder.Clear")
            : Lang.Text("Common.Action.Delete");
    }

    public static bool IsClearingDefaultFolder(GameFolders.McFolder folder, int folderCount)
    {
        return folder.Type is GameFolders.McFolder.Types.Original or GameFolders.McFolder.Types.RenamedOriginal
               && folder.Location == Common.ExePath + @".minecraft\"
               && folderCount == 1;
    }
}
