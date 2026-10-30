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

    public static string GetDeleteMenuText(ModFolder.McFolder folder, int folderCount)
    {
        return folderCount == 1 && folder.Location == Path.Combine(ModBase.exePath, ".minecraft") + @"\"
            ? Lang.Text("Select.Folder.Clear")
            : Lang.Text("Common.Action.Delete");
    }

    public static bool IsClearingDefaultFolder(ModFolder.McFolder folder, int folderCount)
    {
        return folder.type is ModFolder.McFolder.Types.Original or ModFolder.McFolder.Types.RenamedOriginal
               && folder.Location == ModBase.exePath + @".minecraft\"
               && folderCount == 1;
    }
}
