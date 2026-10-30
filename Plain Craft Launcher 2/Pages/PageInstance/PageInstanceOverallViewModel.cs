using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.UI;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例总览页的视图模型。</summary>
public partial class PageInstanceOverallViewModel : ViewModelBase
{
    [ObservableProperty] private string _infoLoadingText = Lang.Text("Instance.Overall.Info.Loading");

    [ObservableProperty] private bool _infoLoadingVisible = true;

    [ObservableProperty] private bool _modsFolderVisible = true;

    [ObservableProperty] private bool _modpackVisible;

    [ObservableProperty] private string _starText = Lang.Text("Instance.Overall.Favorite");

    [ObservableProperty] private bool _cardTypeComboEnabled = true;

    [ObservableProperty] private bool _descriptionButtonEnabled = true;

    [ObservableProperty] private bool _renameButtonEnabled = true;

    [ObservableProperty] private bool _restoreButtonEnabled = true;

    [ObservableProperty] private bool _deleteButtonEnabled = true;

    [ObservableProperty] private bool _patchButtonEnabled = true;

    public ObservableCollection<InstanceInfoRow> InfoRows { get; } = [];

    public void RefreshInfoRows(McInstance instance)
    {
        InfoRows.Clear();
        foreach (var row in BuildInfoRows(instance))
            InfoRows.Add(row);
    }

    public static IReadOnlyList<InstanceInfoRow> BuildInfoRows(McInstance instance)
    {
        var instanceInfo = instance.Info;
        List<InstanceInfoRow> rows = [];
        var launchCount = States.Instance.LaunchCount[instance.PathInstance];
        rows.Add(launchCount == 0
            ? new InstanceInfoRow(Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                Lang.Text("Instance.Overall.Info.LaunchCount.Never"),
                "pack://application:,,,/images/Blocks/RedstoneLampOff.png")
            : new InstanceInfoRow(Lang.Text("Instance.Overall.Info.LaunchCount.Title"),
                Lang.Text("Instance.Overall.Info.LaunchCount.Count", launchCount),
                "pack://application:,,,/images/Blocks/RedstoneLampOn.png"));
        if (!string.IsNullOrWhiteSpace(States.Instance.ModpackVersion[instance.PathInstance]))
            rows.Add(new InstanceInfoRow(Lang.Text("Instance.Overall.Info.ModpackVersion"),
                States.Instance.ModpackVersion[instance.PathInstance],
                "pack://application:,,,/images/Blocks/CommandBlock.png"));
        rows.Add(new InstanceInfoRow("Minecraft", instanceInfo.VanillaName,
            "pack://application:,,,/images/Blocks/Grass.png"));
        if (instanceInfo.HasForge)
            rows.Add(new InstanceInfoRow("Forge", instanceInfo.Forge, "pack://application:,,,/images/Blocks/Anvil.png"));
        if (instanceInfo.HasNeoForge)
            rows.Add(new InstanceInfoRow("NeoForge", instanceInfo.NeoForge,
                "pack://application:,,,/images/Blocks/NeoForge.png"));
        if (instanceInfo.HasCleanroom)
            rows.Add(new InstanceInfoRow("Cleanroom", instanceInfo.Cleanroom,
                "pack://application:,,,/images/Blocks/Cleanroom.png"));
        if (instanceInfo.HasFabric)
            rows.Add(new InstanceInfoRow("Fabric", instanceInfo.Fabric,
                "pack://application:,,,/images/Blocks/Fabric.png"));
        if (instanceInfo.HasQuilt)
            rows.Add(new InstanceInfoRow("Quilt", instanceInfo.Quilt, "pack://application:,,,/images/Blocks/Quilt.png"));
        if (instanceInfo.HasOptiFine)
            rows.Add(new InstanceInfoRow("OptiFine", instanceInfo.OptiFine,
                "pack://application:,,,/images/Blocks/GrassPath.png"));
        if (instanceInfo.HasLiteLoader)
            rows.Add(new InstanceInfoRow("LiteLoader", Lang.Text("Instance.Overall.Info.Installed"),
                "pack://application:,,,/images/Blocks/Egg.png"));
        if (instanceInfo.HasLegacyFabric)
            rows.Add(new InstanceInfoRow("Legacy Fabric", instanceInfo.LegacyFabric,
                "pack://application:,,,/images/Blocks/Fabric.png"));
        if (instanceInfo.HasLabyMod)
            rows.Add(new InstanceInfoRow("LabyMod", instanceInfo.LabyMod,
                "pack://application:,,,/images/Blocks/LabyMod.png"));
        return rows;
    }

    public static void OpenVersionFolder(McInstance version)
    {
        Common.OpenExplorer(version.PathInstance);
    }

    public void RefreshDisplay(McInstance instance)
    {
        StarText = instance.IsStar
            ? Lang.Text("Instance.Overall.Unfavorite")
            : Lang.Text("Instance.Overall.Favorite");
        ModsFolderVisible = instance.Modable;
    }

    // 实例文件夹
    [RelayCommand]
    private void OpenInstanceFolder()
    {
        OpenVersionFolder(PageInstanceLeft.McInstance);
    }

    // 存档文件夹
    [RelayCommand]
    private void OpenSavesFolder()
    {
        var folderPath = PageInstanceLeft.McInstance.PathIndie + @"saves\";
        Directory.CreateDirectory(folderPath);
        Common.OpenExplorer(folderPath);
    }

    // Mod 文件夹
    [RelayCommand]
    private void OpenModsFolder()
    {
        var folderPath = PageInstanceLeft.McInstance.PathIndie + @"mods\";
        Directory.CreateDirectory(folderPath);
        Common.OpenExplorer(folderPath);
    }

    // 导出启动脚本
    [RelayCommand]
    private void ExportLaunchScript()
    {
        try
        {
            // 弹窗要求指定脚本的保存位置
            var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Overall.Script.SelectSaveTitle"), "启动 " + PageInstanceLeft.McInstance.Name + ".bat",
                Lang.Text("Instance.Overall.Script.FileFilter"));
            if (string.IsNullOrEmpty(savePath))
                return;
            // 检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            if (GameLaunch.mcLaunchLoader.State == Common.LoadState.Loading)
            {
                HintService.Hint(Lang.Text("Instance.Overall.Script.WaitForLaunchTask"), HintType.Error);
                return;
            }

            // 生成脚本
            if (GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                    { SaveBatch = savePath, instance = PageInstanceLeft.McInstance }))
            {
                if (ProfileService.Current?.ProfileType == PCL.Core.Minecraft.Profile.Models.ProfileType.Offline)
                    HintService.Hint(Lang.Text("Instance.Overall.Script.Exporting"));
                else
                    HintService.Hint(Lang.Text("Instance.Overall.Script.ExportingWarning"));
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"导出启动脚本失败（{PageInstanceLeft.McInstance.Name}）",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 补全文件
    [RelayCommand]
    private void CheckFiles()
    {
        if (!RestrictedFeaturePolicy.Check("minecraft-repair")) return;
        try
        {
            // 忽略文件检查提示
            if ((bool)Libraries.ShouldIgnoreFileCheck(PageInstanceLeft.McInstance))
            {
                HintService.Hint(Lang.Text("Instance.Overall.Repair.DisableVerificationHint"));
                return;
            }

            // 重复任务检查
            var taskName = PageInstanceLeft.McInstance.Name + " " + Lang.Text("Instance.Overall.Repair.TaskName");
            foreach (var OngoingLoader in Loading.loaderTaskbar)
            {
                if ((OngoingLoader.name ?? "") != (taskName ?? ""))
                    continue;
                HintService.Hint(Lang.Text("Instance.Overall.Repair.Processing"), HintType.Error);
                return;
            }

            // 启动
            var loader = new Loading.LoaderCombo<string>(taskName,
                GameDownload.DlClientFix(PageInstanceLeft.McInstance, true,
                    GameDownload.AssetsIndexExistsBehaviour.AlwaysDownload));
            loader.OnStateChanged = _ =>
            {
                switch (loader.State)
                {
                    case Common.LoadState.Finished:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Success.WithTaskName", taskName), HintType.Success);
                        break;
                    }
                    case Common.LoadState.Failed:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Failed.WithDetail", taskName, loader.Error.ToString()),
                            HintType.Error);
                        break;
                    }
                    case Common.LoadState.Aborted:
                    {
                        HintService.Hint(
                            Lang.Text("Instance.Overall.Repair.Cancelled.WithTaskName", taskName));
                        break;
                    }
                }
            };
            loader.Start(PageInstanceLeft.McInstance.Name);
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"尝试补全文件失败（{PageInstanceLeft.McInstance.Name}）",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }

    // 测试游戏
    [RelayCommand]
    private void TestGame()
    {
        try
        {
            GameLaunch.McLaunchStart(new GameLaunch.McLaunchOptions
                { instance = PageInstanceLeft.McInstance, IsTest = true });
            Launcher.frmMain.PageChange(FormMain.PageType.Launch);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "测试游戏失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Overall.Error.OperationFailed"));
        }
    }
}

/// <summary>实例信息表中的一行。</summary>
public class InstanceInfoRow(string title, string info, string logo)
{
    public string Title { get; } = title;

    public string Info { get; } = info;

    public string Logo { get; } = logo;
}
