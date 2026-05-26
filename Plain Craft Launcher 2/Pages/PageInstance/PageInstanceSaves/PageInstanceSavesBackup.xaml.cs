using System.Globalization;
using System.Windows;
using Microsoft.VisualBasic;
using PCL.Core.IO;
using PCL.Core.UI;
using PCL.Core.Utils.VersionControl;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSavesBackup : IRefreshable
{
    private bool _loaded;

    public PageInstanceSavesBackup()
    {
        InitializeComponent();
        Loaded += (_, _) => Init();
        BtnCreate.Click += (_, _) => BtnCreate_Click();
        BtnClean.Click += (_, _) => BtnClean_Click();
    }

    void IRefreshable.Refresh()
    {
        IRefreshable_Refresh();
    }

    private void IRefreshable_Refresh()
    {
        Refresh();
    }

    public void Refresh()
    {
        RefreshList();
    }

    private void Init()
    {
        PanBack.ScrollToHome();

        RefreshList();

        _loaded = true;
        if (_loaded)
            return;
    }

    private void RefreshList()
    {
        try
        {
            PanList.Children.Clear();
            List<VersionData> versions;
            using (var snap = new SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave))
            {
                versions = snap.GetVersions();
                if (versions.Count == 0)
                {
                    PanDisplay.Visibility = Visibility.Collapsed;
                    PanEmpty.Visibility = Visibility.Visible;
                }
                else
                {
                    PanDisplay.Visibility = Visibility.Visible;
                    PanEmpty.Visibility = Visibility.Collapsed;
                }
            }

            if (versions.Count == 0) return;
            foreach (var item in versions)
            {
                var newItem = new MyListItem
                {
                    Title = item.Name,
                    Info = item.Desc,
                    Tags = new[] { item.Created }.ToList()
                };

                var btnApply = new MyIconButton
                {
                    Logo = Icon.IconPlayGame,
                    ToolTip = Lang.Text("Instance.Saves.Backup.RestoreToolTip")
                };

                btnApply.Click += (_, _) =>
                {
                    try
                    {
                        if (ModMain.MyMsgBox(Lang.Text("Instance.Saves.Backup.RestoreConfirm"), Button1: Lang.Text("Common.Action.Confirm"), Button2: Lang.Text("Common.Action.Cancel")) == 2)
                            return;
                        ModMain.Hint(Lang.Text("Instance.Saves.Backup.ApplyingSnapshot"));
                        var loaders = new List<ModLoader.LoaderBase>();
                        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Saves.Backup.SearchApply"), load =>
                        {
                            load.Progress = 0.2d;
                            load.Progress = 1d;
                        }));
                        var loader = new ModLoader.LoaderCombo<int>($"{item.Name} - {Lang.Text("Instance.Saves.Backup.ApplyTitle")}", loaders)
                            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                        loader.Start(1);
                        ModLoader.LoaderTaskbarAdd(loader);
                        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                        ModMain.FrmMain.BtnExtraDownload.Ribble();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.ApplyError"), ModBase.LogLevel.Msgbox);
                    }
                };

                var btnExport = new MyIconButton
                {
                    Logo = Icon.IconButtonSave,
                    ToolTip = Lang.Text("Instance.Saves.Backup.ExportToolTip")
                };

                btnExport.Click += (_, _) =>
                {
                    try
                    {
                        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Saves.Backup.SelectExportPath"), $"{item.Name}.zip",
                            "压缩文件(*.zip)|*.zip", ModBase.ExePath);
                        if (string.IsNullOrEmpty(savePath))
                            return;
                        ModMain.Hint(Lang.Text("Instance.Saves.Backup.ExportingSnapshot"));
                        var loaders = new List<ModLoader.LoaderBase>();
                        loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Saves.Backup.MakeArchive"), load =>
                        {
                            load.Progress = 0.2d;
                            ;
                            load.Progress = 1d;
                        }));
                        var loader = new ModLoader.LoaderCombo<int>($"{item.Name} - {Lang.Text("Instance.Saves.Backup.ExportTitle")}", loaders)
                            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                        loader.Start(1);
                        ModLoader.LoaderTaskbarAdd(loader);
                        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                        ModMain.FrmMain.BtnExtraDownload.Ribble();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.ExportError"), ModBase.LogLevel.Msgbox);
                    }
                };

                var btnDelete = new MyIconButton
                {
                    Logo = Icon.IconButtonDelete,
                    ToolTip = Lang.Text("Common.Action.Delete")
                };

                btnDelete.Click += (_, _) =>
                {
                    try
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Instance.Saves.Backup.DeleteConfirmMessage", item.Name, item.Desc,
                                    item.Created),
                                Lang.Text("Instance.Saves.Backup.DeleteConfirmTitle"),
                                Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2) return;
                        using (var snap = new SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave))
                        {
                            snap.DeleteVersion(item.NodeId);
                        }

                        RefreshList();
                        ModMain.Hint(Lang.Text("Instance.Saves.Backup.Deleted"), ModMain.HintType.Finish);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.DeleteFailed"));
                    }
                };

                var btnInfo = new MyIconButton
                {
                    Logo = Icon.IconButtonInfo,
                    ToolTip = Lang.Text("Instance.Saves.Backup.InfoToolTip")
                };


                btnInfo.Click += (_, _) =>
                {
                    try
                    {
                        List<FileVersionObjects> data;
                        using (var snap = new SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave))
                        {
                            data = snap.GetNodeObjects(item.NodeId);
                        }

                        var totalSize = data.Select(x => x.Length).Sum();
                        ModMain.MyMsgBox(
                            Lang.Text("Instance.Saves.Backup.InfoDialog", item.Desc, item.Created,
                                ByteStream.GetReadableLength(totalSize, provider: Lang.Culture), data.Count),
                            item.Name);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.DeleteFailed"));
                    }
                };
                newItem.Buttons = [btnDelete, btnExport, btnInfo, btnApply];

                PanList.Children.Add(newItem);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.LoadFailed"), ModBase.LogLevel.Msgbox);
        }
    }

    private void BtnCreate_Click()
    {
        try
        {
            var input = ModMain.MyMsgBoxInput(Lang.Text("Instance.Saves.Backup.NameInputPrompt"), DefaultInput: DateTime.Now.ToString("yyyy/MM/dd-HH:mm:ss", CultureInfo.InvariantCulture));
            if (input is null)
                return;
            if (string.IsNullOrWhiteSpace(input))
                input = null;
            if (ModMain.MyMsgBox(Lang.Text("Instance.Saves.Backup.NotHotBackup"), Lang.Text("Instance.Saves.Backup.PleaseNote"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Back")) == 2)
                return;
            BtnCreate.IsEnabled = false;
            ModMain.Hint(Lang.Text("Instance.Saves.Backup.StartingBackup"));
            var loaders = new List<ModLoader.LoaderBase>();
            loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Saves.Backup.SearchAndCreate"), load =>
            {
                load.Progress = 0.2d;

                load.Progress = 1d;
                ModBase.RunInUi(() => RefreshList());
            }));
            var loader = new ModLoader.LoaderCombo<int>($"{input} - {Lang.Text("Instance.Saves.Backup.CreateTitle")}", loaders)
                { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
            loader.Start(1);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            BtnCreate.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Saves.Backup.CreateError"), ModBase.LogLevel.Msgbox);
        }
    }

    private void BtnClean_Click()
    {
        if (ModMain.MyMsgBox(Lang.Text("Instance.Saves.Backup.CleanDescription"), Lang.Text("Instance.Saves.Backup.CleanConfirmTitle"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Back")) == 2)
            return;
        var loaders = new List<ModLoader.LoaderBase>
        {
            new ModLoader.LoaderTask<int, int>(Lang.Text("Instance.Saves.Backup.FindAndClean"), load =>
            {
                load.Progress = 0.2d;
                ;
                load.Progress = 1d;
            })
        };
        var loader =
            new ModLoader.LoaderCombo<int>($"{ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave)} - {Lang.Text("Instance.Saves.Backup.CleanTitle")}",
                loaders) { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        loader.Start(1);
        ModLoader.LoaderTaskbarAdd(loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
    }
}
