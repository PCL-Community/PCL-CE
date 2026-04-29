using System.Windows;
using Microsoft.VisualBasic;
using PCL.Core.IO;
using PCL.Core.UI;
using PCL.Core.Utils.VersionControl;

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
                    Logo = ModBase.Logo.IconPlayGame,
                    ToolTip = "回到到此快照"
                };

                btnApply.Click += (_, _) =>
                {
                    try
                    {
                        if (ModMain.MyMsgBox("确定要应用此备份吗？请确保当前的存档已完成备份或者十分确定不再使用！", Button1: "确定", Button2: "取消") == 2)
                            return;
                        ModMain.Hint("应用快照中，请勿执行其他操作！");
                        var loaders = new List<ModLoader.LoaderBase>();
                        loaders.Add(new ModLoader.LoaderTask<int, int>("搜寻并应用文件", load =>
                        {
                            load.Progress = 0.2d;
                            load.Progress = 1d;
                        }));
                        var loader = new ModLoader.LoaderCombo<int>($"{item.Name} - 备份应用", loaders)
                            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                        loader.Start(1);
                        ModLoader.LoaderTaskbarAdd(loader);
                        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                        ModMain.FrmMain.BtnExtraDownload.Ribble();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "应用快照过程中出现错误", ModBase.LogLevel.Msgbox);
                    }
                };

                var btnExport = new MyIconButton
                {
                    Logo = ModBase.Logo.IconButtonSave,
                    ToolTip = "导出到压缩包"
                };

                btnExport.Click += (_, _) =>
                {
                    try
                    {
                        var savePath = SystemDialogs.SelectSaveFile("选择保存备份导出的位置", $"{item.Name}.zip",
                            "压缩文件(*.zip)|*.zip", ModBase.ExePath);
                        if (string.IsNullOrEmpty(savePath))
                            return;
                        ModMain.Hint("快照导出中，请勿执行其他操作！");
                        var loaders = new List<ModLoader.LoaderBase>();
                        loaders.Add(new ModLoader.LoaderTask<int, int>("制作压缩包", load =>
                        {
                            load.Progress = 0.2d;
                            ;
                            load.Progress = 1d;
                        }));
                        var loader = new ModLoader.LoaderCombo<int>($"{item.Name} - 导出备份", loaders)
                            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
                        loader.Start(1);
                        ModLoader.LoaderTaskbarAdd(loader);
                        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                        ModMain.FrmMain.BtnExtraDownload.Ribble();
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "备份导出过程中出现错误", ModBase.LogLevel.Msgbox);
                    }
                };

                var btnDelete = new MyIconButton
                {
                    Logo = ModBase.Logo.IconButtonDelete,
                    ToolTip = "删除"
                };

                btnDelete.Click += (_, _) =>
                {
                    try
                    {
                        if (ModMain.MyMsgBox(
                                $"你确定要删除备份 {item.Name} 吗？{"\r\n"}描述：{item.Desc}{"\r\n"}创建时间：{item.Created}",
                                "删除确认", "确认", "取消") == 2) return;
                        using (var snap = new SnapLiteVersionControl(PageInstanceSavesLeft.CurrentSave))
                        {
                            snap.DeleteVersion(item.NodeId);
                        }

                        RefreshList();
                        ModMain.Hint("已删除！", ModMain.HintType.Finish);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "执行删除任务失败");
                    }
                };

                var btnInfo = new MyIconButton
                {
                    Logo = ModBase.Logo.IconButtonInfo,
                    ToolTip = "信息"
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
                        ModMain.MyMsgBox($@"描述: {item.Desc}
                            创建时间: {item.Created}
                            存档大小: {ByteStream.GetReadableLength(totalSize)} ({data.Count} 个对象)", item.Name);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "执行删除任务失败");
                    }
                };
                newItem.Buttons = [btnDelete, btnExport, btnInfo, btnApply];

                PanList.Children.Add(newItem);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取备份信息失败", ModBase.LogLevel.Msgbox);
        }
    }

    private void BtnCreate_Click()
    {
        try
        {
            var input = ModMain.MyMsgBoxInput("请输入名称", DefaultInput: $"{DateTime.Now:yyyy/MM/dd-HH:mm:ss}");
            if (input is null)
                return;
            if (string.IsNullOrWhiteSpace(input))
                input = null;
            if (ModMain.MyMsgBox("备份功能不具备热备份功能，请确你没有在使用存档内的任何文件！", "请注意！", "继续", "返回") == 2)
                return;
            BtnCreate.IsEnabled = false;
            ModMain.Hint("开始备份任务，请勿执行其他操作！");
            var loaders = new List<ModLoader.LoaderBase>();
            loaders.Add(new ModLoader.LoaderTask<int, int>("搜寻并制作备份", load =>
            {
                load.Progress = 0.2d;

                load.Progress = 1d;
                ModBase.RunInUi(() => RefreshList());
            }));
            var loader = new ModLoader.LoaderCombo<int>($"{input} - 制作备份", loaders)
                { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
            loader.Start(1);
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
            BtnCreate.IsEnabled = true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "备份过程中出现错误", ModBase.LogLevel.Msgbox);
        }
    }

    private void BtnClean_Click()
    {
        if (ModMain.MyMsgBox("此功能可以清理备份文件中已不再需要的文件，建议在发生备份删除后使用。", "确定使用吗？", "确定", "返回") == 2)
            return;
        var loaders = new List<ModLoader.LoaderBase>
        {
            new ModLoader.LoaderTask<int, int>("寻找并清理备份文件", load =>
            {
                load.Progress = 0.2d;
                ;
                load.Progress = 1d;
            })
        };
        var loader =
            new ModLoader.LoaderCombo<int>($"{ModBase.GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave)} - 备份清理",
                loaders) { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        loader.Start(1);
        ModLoader.LoaderTaskbarAdd(loader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
    }
}