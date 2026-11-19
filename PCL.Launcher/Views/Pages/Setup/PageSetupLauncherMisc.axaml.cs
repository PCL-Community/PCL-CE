using Avalonia.Controls;
using Avalonia.Data;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.UI;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>
/// 启动器杂项设置页：公告显示、日志上限、网络代理与调试选项。 配置状态由 <see cref="PageSetupLauncherMiscViewModel" />
/// 持有，页面处理系统级文件操作与重启。
/// </summary>
public partial class PageSetupLauncherMisc : MyPageRight, ISetupResetPage
{
    private bool _loaded;
    private bool _reloading;

    public PageSetupLauncherMisc()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;

        SliderMaxLog.getHintText = new Func<object, object>(FormatMaxLogHint);
        SliderDebugAnim.getHintText = new Func<object, object>(FormatAnimationSpeedHint);
        ComboDebugLinuxProtocolAuto.Bind(
            ContentControl.ContentProperty,
            new Binding(nameof(ViewModel.LinuxProtocolAutoText))
            {
                Source = ViewModel
            });
        ComboDebugLinuxProtocolAuto.Bind(
            ToolTip.TipProperty,
            new Binding(nameof(ViewModel.LinuxProtocolAutoToolTip))
            {
                Source = ViewModel
            });

        ComboSystemActivity.SelectionChanged += async (_, _) => await OnAnnouncementChangedAsync();
        SliderMaxLog.Change += (_, _) =>
            WriteIfReady(() => Config.System.MaxGameLog = SliderMaxLog.Value);
        SliderDebugAnim.Change += (_, _) =>
            WriteIfReady(() => Config.Debug.AnimationSpeed = SliderDebugAnim.Value);

        var supportsProtocol = ApplicationRuntime.Current.Platform.Capabilities
            .SupportsDisplayProtocolSelection;
        ComboDebugLinuxProtocol.IsEnabled = supportsProtocol;
        ComboDebugLinuxProtocol.SetLocalizedValue(
            ToolTip.TipProperty,
            supportsProtocol
                ? "Setup.Misc.Debug.LinuxProtocol.ToolTip"
                : "Setup.Misc.Debug.LinuxProtocol.OnlyLinux");
        ToolTip.SetShowOnDisabled(ComboDebugLinuxProtocol, true);
        ComboDebugLinuxProtocol.SelectionChanged += (_, _) =>
        {
            if (_reloading) return;
            WriteIfReady(() =>
            {
                Config.Debug.LinuxProtocol = Math.Clamp(
                    ComboDebugLinuxProtocol.SelectedIndex,
                    0,
                    2);
                HintService.Show(
                    Hints.Info(
                        Lang.Text("Setup.Misc.Debug.LinuxProtocol.Restart"),
                        false));
            });
        };
        CheckSystemDisableHardwareAcceleration.Change += (_, _) => WriteIfReady(() =>
        {
            Config.System.DisableHardwareAcceleration =
                CheckSystemDisableHardwareAcceleration.Checked == true;
            HintService.Show(
                Hints.Info(Lang.Text("Setup.Misc.HardwareAcceleration.RestartNotice")));
        });
        CheckSystemTelemetry.Change += (_, _) =>
            WriteIfReady(() => Config.System.Telemetry = CheckSystemTelemetry.Checked == true);
        CheckNetDohEnable.Change += (_, _) =>
            WriteIfReady(() => Config.Network.EnableDoH = CheckNetDohEnable.Checked == true);
        CheckDebugSkipCopy.Change += (_, _) =>
            WriteIfReady(() => Config.Debug.DontCopy = CheckDebugSkipCopy.Checked == true);
        CheckDebugMode.Change += (_, _) => WriteIfReady(() =>
        {
            Config.Debug.Enabled = CheckDebugMode.Checked == true;
            HintService.Show(Hints.Info(Lang.Text("Setup.Misc.Debug.Mode.Hint"), false));
        });
        CheckDebugDelay.Change += (_, _) =>
            WriteIfReady(() => Config.Debug.AddRandomDelay = CheckDebugDelay.Checked == true);

        RadioHttpProxyType0.Check += (_, _) => WriteProxyType(0);
        RadioHttpProxyType1.Check += (_, _) => WriteProxyType(1);
        RadioHttpProxyType2.Check += (_, _) => WriteProxyType(2);

        BtnApplyHttpProxy.Click += (_, _) => ViewModel.ApplyHttpProxy();
        BtnSystemSettingExp.Click += (_, _) => ExportSettings();
        BtnSystemSettingImp.Click += async (_, _) => await ImportSettingsAsync();
        BtnSystemStopUsingPclCe.Click += async (_, _) => await StopUsingPclCeAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Reload();
        };
    }

    public PageSetupLauncherMiscViewModel ViewModel { get; } = new();

    public void Reset()
    {
        try
        {
            Config.Network.Reset();
            Config.Debug.Reset();
            Config.System.Reset();
            HintService.Show(Hints.Success(Lang.Text("Setup.Misc.Initialized"), false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Misc.Error.InitFailed"),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Misc.Error.InitFailed"));
        }

        Reload();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        PanBack.ScrollToHome();
        if (_loaded) Reload();
    }

    public override void OnNavigatedFrom()
    {
    }

    public void Reload()
    {
        _reloading = true;
        try
        {
            ViewModel.Reload();
            SliderMaxLog.Value = Config.System.MaxGameLog;
            CheckSystemDisableHardwareAcceleration.Checked =
                Config.System.DisableHardwareAcceleration;
            CheckSystemTelemetry.Checked =
                !ViewModel.IsBuildUntrusted && Config.System.Telemetry;

            CheckNetDohEnable.Checked = Config.Network.EnableDoH;
            var proxyType = Math.Clamp(Config.Network.HttpProxy.Type, 0, 2);
            var radios = new[]
            {
                RadioHttpProxyType0,
                RadioHttpProxyType1,
                RadioHttpProxyType2
            };
            radios[proxyType].SetChecked(true, false);
            PanHttpProxyCustom.IsVisible = proxyType == 2;

            SliderDebugAnim.Value = Config.Debug.AnimationSpeed;
            ComboDebugLinuxProtocol.SelectedIndex =
                Math.Clamp(Config.Debug.LinuxProtocol, 0, 2);
            CheckDebugSkipCopy.Checked = Config.Debug.DontCopy;
            CheckDebugMode.Checked = Config.Debug.Enabled;
            CheckDebugDelay.Checked = Config.Debug.AddRandomDelay;
        }
        finally
        {
            _reloading = false;
        }

        RefreshTelemetryToolTip();
    }

    private void RefreshTelemetryToolTip()
    {
        if (ViewModel.IsBuildUntrusted)
        {
            CheckSystemTelemetry.SetLocalizedValue(
                ToolTip.TipProperty,
                "Setup.Misc.System.Telemetry.Unavailable");
            ToolTip.SetShowOnDisabled(CheckSystemTelemetry, true);
        }
        else
        {
            CheckSystemTelemetry.SetLocalizedValue(
                ToolTip.TipProperty,
                "Setup.Misc.System.Telemetry.ToolTip");
            ToolTip.SetShowOnDisabled(CheckSystemTelemetry, false);
        }
    }

    private void WriteIfReady(Action action)
    {
        if (_reloading) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "写入杂项设置失败");
        }
    }

    private void WriteProxyType(int type)
    {
        if (_reloading) return;
        Config.Network.HttpProxy.Type = type;
        PanHttpProxyCustom.IsVisible = type == 2;
    }

    private async Task OnAnnouncementChangedAsync()
    {
        if (_reloading || ComboSystemActivity.SelectedIndex < 0) return;
        var index = ComboSystemActivity.SelectedIndex;
        if (index == 2)
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Setup.Misc.System.Announcement.Disabled.Warning.Message"),
                    Buttons = DialogButtons.Create(
                        "Setup.Misc.System.Announcement.Disabled.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (!result.IsConfirmed)
            {
                _reloading = true;
                try
                {
                    ComboSystemActivity.SelectedIndex =
                        Math.Clamp(States.System.AnnounceSolution, 0, 2);
                }
                finally
                {
                    _reloading = false;
                }

                return;
            }
        }

        States.System.AnnounceSolution = index;
        ViewModel.ActivityIndex = index;
    }

    private static async void ExportSettings()
    {
        var savePath = await SystemDialogs.SelectSaveFileAsync(
            Lang.Text("Setup.Misc.System.ExportSettings.SaveTitle"),
            "PCL CE 全局配置.json",
            Lang.Text("Setup.Misc.System.ExportSettings.Filter"),
            Basics.ExecutableDirectory);
        if (string.IsNullOrWhiteSpace(savePath)) return;
        File.Copy(ConfigService.SharedConfigPath, savePath, true);
        HintService.Show(Hints.Success(Lang.Text("Setup.Misc.System.ExportSettings.Success")));
        Basics.OpenPath(savePath);
    }

    private static async Task ImportSettingsAsync()
    {
        var sourcePath = await SystemDialogs.SelectFileAsync(
            Lang.Text("Setup.Misc.System.ExportSettings.Filter"),
            Lang.Text("Setup.Misc.System.ImportSettings.SelectTitle"));
        if (string.IsNullOrWhiteSpace(sourcePath)) return;
        try
        {
            File.Copy(sourcePath, ConfigService.SharedConfigPath, true);
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text("Setup.Misc.System.ImportSettings.Success.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(
                            Lang.Text("Setup.Misc.System.ImportSettings.Success.Restart")))
                });
            // 导入的配置需要重启后生效；与 WPF 一样立即重启启动器。
            UiInteraction.Restart();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "导入配置失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private static object FormatMaxLogHint(object value)
    {
        var tick = Convert.ToInt32(value);
        return tick switch
        {
            <= 5 => tick * 10 + 50,
            <= 13 => tick * 50 - 150,
            <= 28 => tick * 100 - 800,
            _ => Lang.Text("Setup.Misc.System.MaxLogLines.Unlimited")
        };
    }

    private static object FormatAnimationSpeedHint(object value)
    {
        var tick = Convert.ToInt32(value);
        if (tick > 29) return Lang.Text("Common.Action.Close");
        return Lang.Number(Math.Round(tick / 10d + 0.1d, 1), "N1") + "x";
    }

    #region 停止使用 PCL CE

    private static async Task StopUsingPclCeAsync()
    {
        try
        {
            var first = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Setup.Misc.System.StopUsingPclCe.Title"),
                    Content = Lang.Text("Setup.Misc.System.StopUsingPclCe.Message"),
                    Buttons = DialogButtons.Create(
                        "Common.Action.Continue",
                        "Setup.Misc.System.StopUsingPclCe.ContinueAndRemove",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            if (first.Button is < 1 or > 2) return;

            var second = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Setup.Misc.System.StopUsingPclCe.Message.Final"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
            if (!second.IsConfirmed) return;

            StopUsingPclCeCore(first.Button == 2);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "停止使用 PCL CE 失败",
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private static void StopUsingPclCeCore(bool removeMcResources)
    {
        if (removeMcResources && !string.IsNullOrWhiteSpace(States.Game.Folders))
            foreach (var folder in States.Game.Folders.Split('|'))
            {
                var parts = folder.Split('>');
                if (parts.Length < 2) continue;
                var realPath = parts[1];
                DeleteFiles([Path.Combine(realPath, "PCL.ini")]);

                var versionsPath = Path.Combine(realPath, "versions");
                if (!Directory.Exists(versionsPath)) continue;
                DeleteFiles(
                    Directory
                        .EnumerateDirectories(versionsPath)
                        .Select(p => Path.Combine(p, "PCL", "config.v1.yml")));
            }

        var folders = new[]
            {
                Paths.Data,
                Paths.OldSharedData,
                Paths.SharedData,
                Paths.SharedLocalData,
                Paths.Temp
            }
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .Distinct()
            .ToArray();

        ApplicationRuntime.Current.Platform.ProcessCleanup.DeleteFoldersAndRestart(folders);

        UiInteraction.Shutdown();
        return;

        static void DeleteFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (Exception)
                {
                    // ignored
                }
        }
    }

    #endregion
}