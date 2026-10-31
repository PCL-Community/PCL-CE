using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;
using PCL.Network;

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

    [RelayCommand]
    private void CreateFolder()
    {
        if (NetFacade.HasDownloadingTask())
        {
            HintService.Hint(Lang.Text("Select.Folder.CannotCreateWhileDownloading"), HintType.Error);
            return;
        }

        if (!Directory.Exists(Common.ExePath + @".minecraft\"))
        {
            Directory.CreateDirectory(Common.ExePath + @".minecraft\");
            Directory.CreateDirectory(Common.ExePath + @".minecraft\versions\");
            States.Game.SelectedFolder = @"$.minecraft\";
            GameFolders.McFolderLauncherProfilesJsonCreate(Common.ExePath + @".minecraft\");
            HintService.Hint(Lang.Text("Select.Folder.CreateSuccess"), HintType.Success);
        }

        GameFolders.McFolderListLoader.Start(isForceRestart: true);
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var newFolder = "";
        if (NetFacade.HasDownloadingTask())
        {
            HintService.Hint(Lang.Text("Select.Folder.CannotAddWhileDownloading"), HintType.Error);
            return;
        }

        try
        {
            newFolder = SystemDialogs.SelectFolder();
            if (string.IsNullOrEmpty(newFolder))
                return;
            if (newFolder.Contains('!') || newFolder.Contains(';'))
            {
                HintService.Hint(Lang.Text("Select.Folder.InvalidPathChars"), HintType.Error);
                return;
            }

            var splitedNames = newFolder.TrimEnd('\\').Split(@"\");
            var defaultName = splitedNames.Last() == ".minecraft"
                ? splitedNames.Length >= 3 ? splitedNames[^2] : ""
                : splitedNames.Last();
            if (defaultName.Length > 40)
                defaultName = defaultName[..39];
            var newNameDialog = await Dialog.ShowAsync(new InputDialogRequest
            {
                Title = Lang.Text("Select.Folder.InputDisplayName.Title"),
                Content = Lang.Text("Select.Folder.InputDisplayName.Message"),
                DefaultValue = defaultName,
                ValidateRules =
                [
                    new NullOrWhiteSpaceValidator(), new StringLengthValidator(), new BlacklistValidator([">", "|"])
                ],
                HighLight = false
            });
            var newName = newNameDialog.IsConfirmed ? newNameDialog.InputText : null;
            if (string.IsNullOrWhiteSpace(newName))
                return;
            PageSelectLeft.AddFolder(newFolder, newName, true);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Select.Folder.Error.Add", newFolder),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Select.Folder.Error.Add", newFolder));
        }
    }

    [RelayCommand]
    private void InstallModpack()
    {
        Modpack.ModpackInstall();
    }
}
