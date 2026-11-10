using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.OS;
using PCL.Core.ViewModel;
using System.IO;

namespace PCL;

/// <summary>实例独立设置页的视图模型。</summary>
public partial class PageInstanceSetupViewModel : ViewModelBase
{
    [ObservableProperty] private string _advanceClasspathHeadText = "";

    [ObservableProperty] private string _advanceGameText = "";

    [ObservableProperty] private string _advanceJvmText = "";

    [ObservableProperty] private string _advanceRunText = "";

    [ObservableProperty] private string _argumentInfoText = "";

    [ObservableProperty] private bool _checkAdvanceRunWaitVisible;

    [ObservableProperty] private bool _debugLog4j2Enabled = true;

    [ObservableProperty] private bool _disableJlwEnabled = true;

    [ObservableProperty] private bool _indieV2Enabled = true;

    [ObservableProperty] private bool _ramCustomEnabled = true;

    [ObservableProperty] private string _ramGameText = "2.5 GiB";

    [ObservableProperty] private bool _ramInitialCustomEnabled = true;

    [ObservableProperty] private bool _ramTooHighVisible;

    [ObservableProperty] private string _ramTotalText = " / 7.9 GiB";

    [ObservableProperty] private string _ramUsedText = "4.7 GiB";

    [ObservableProperty] private bool _ramWarnVisible = true;

    [ObservableProperty] private bool _rendererEnabled = true;

    [ObservableProperty] private bool _serverAuthEditable = true;

    [ObservableProperty] private bool _serverAuthLockEnabled;

    [ObservableProperty] private bool _serverAuthLockVisible = true;

    [ObservableProperty] private string _serverAuthNameText = "";

    [ObservableProperty] private string _serverAuthRegisterText = "";

    [ObservableProperty] private string _serverAuthServerText = "";

    [ObservableProperty] private bool _serverAuthVisible;

    [ObservableProperty] private string _serverEnterText = "";

    [ObservableProperty] private bool _serverHttpWarningVisible;

    [ObservableProperty] private bool _serverLoginLockVisible;

    [ObservableProperty] private bool _serverNewProfileVisible = true;

    [ObservableProperty] private bool _serverSslWarningVisible;

    [ObservableProperty] private bool _titleEmptyHintVisible = true;

    [ObservableProperty] private string _titleHintText = "";

    public double RamEmpty { get; private set; }

    public double RamGame { get; private set; }

    public double RamGameActual { get; private set; }

    public int RamMaxValue { get; private set; }

    public double RamTotal { get; private set; }

    public double RamUsed { get; private set; }

    public static double? GetInitialRam(McInstance version, bool? is32BitJava = default)
    {
        if (Config.Launch.LockMemory)
            return null;
        if (PageSetupLaunch.HasCustomXms(version))
            return null;
        var instancePath = version?.PathInstance;
        switch (Config.Instance.MemorySolution[instancePath])
        {
            case 2:
                return PageSetupLaunch.GetInitialRam(version, true, is32BitJava);
            case 1:
                var initialTick = Config.Instance.CustomInitialMemorySize[instancePath];
                if (initialTick <= 0)
                    return null;
                return Math.Min(PageSetupLaunch.GetRamFromTick(initialTick), GetRam(version, is32BitJava));
            default:
                return null;
        }
    }

    public static double GetRam(McInstance version, bool? is32BitJava = default)
    {
        var instancePath = version?.PathInstance;
        if (Config.Instance.MemorySolution[instancePath] == 2)
            return PageSetupLaunch.GetRam(version, true, is32BitJava);

        var ramGive = default(double);
        if (Config.Instance.MemorySolution[instancePath] == 0)
        {
            var ramAvailable =
                Math.Round((double)(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10)) / 10;
            double ramMininum;
            double ramTarget1;
            double ramTarget2;
            double ramTarget3;
            if (version is not null && !version.IsLoaded)
                version.Load();
            if (version is not null && version.Modable)
            {
                var modDir = new DirectoryInfo(version.PathIndie + @"mods\");
                var modCount = modDir.Exists ? modDir.GetFiles().Length : 0;
                ramMininum = 0.5d + modCount / 150d;
                ramTarget1 = 1.5d + modCount / 90d;
                ramTarget2 = 2.7d + modCount / 50d;
                ramTarget3 = 4.5d + modCount / 25d;
            }
            else if (version is not null && version.Info.HasOptiFine)
            {
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 3d;
                ramTarget3 = 5d;
            }
            else
            {
                ramMininum = 0.5d;
                ramTarget1 = 1.5d;
                ramTarget2 = 2.5d;
                ramTarget3 = 4d;
            }

            double ramDelta;
            ramDelta = ramTarget1;
            ramGive += Math.Min(ramAvailable, ramDelta);
            ramAvailable -= ramDelta;
            if (ramAvailable >= 0.1d)
            {
                ramDelta = ramTarget2 - ramTarget1;
                ramGive += Math.Min(ramAvailable * 0.7d, ramDelta);
                ramAvailable -= ramDelta / 0.7d;
                if (ramAvailable >= 0.1d)
                {
                    ramDelta = ramTarget3 - ramTarget2;
                    ramGive += Math.Min(ramAvailable * 0.4d, ramDelta);
                    ramAvailable -= ramDelta / 0.4d;
                    if (ramAvailable >= 0.1d)
                    {
                        ramDelta = ramTarget3;
                        ramGive += Math.Min(ramAvailable * 0.15d, ramDelta);
                        ramAvailable -= ramDelta / 0.15d;
                    }
                }
            }

            ramGive = Math.Round(Math.Max(ramGive, ramMininum), 1);
        }
        else
        {
            var value = Config.Instance.CustomMemorySize[instancePath];
            ramGive = PageSetupLaunch.GetRamFromTick(value);
        }

        if (is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(PageInstanceLeft.McInstance))
            ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    public void RamType(int type)
    {
        RamCustomEnabled = type == 1;
        RamInitialCustomEnabled = type == 1 && !Config.Launch.LockMemory;
    }

    public void RefreshRam(McInstance instance)
    {
        RamGame = Math.Round(GetRam(instance), 5);
        var phyRam = KernelInterop.GetPhysicalMemoryBytes();
        RamTotal = Math.Round((double)(phyRam.Total / 1024 / 1024 / 1024), 1);
        var ramAvailable = Math.Round((double)(phyRam.Available / 1024 / 1024 / 1024), 1);
        RamGameActual = Math.Round(Math.Min(RamGame, ramAvailable), 5);
        RamUsed = Math.Round(RamTotal - ramAvailable, 5);
        RamEmpty = Math.Round(Common.MathClamp(RamTotal - RamUsed - RamGame, 0d, 1000d), 1);
        if (RamTotal <= 1.5d)
            RamMaxValue = (int)Math.Round(Math.Max(Math.Floor((RamTotal - 0.3d) / 0.1d), 1d));
        else if (RamTotal <= 8d)
            RamMaxValue = (int)Math.Round(Math.Floor((RamTotal - 1.5d) / 0.5d) + 12d);
        else if (RamTotal <= 16d)
            RamMaxValue = (int)Math.Round(Math.Floor((RamTotal - 8d) / 1d) + 25d);
        else
            RamMaxValue = (int)Math.Round(Math.Floor((RamTotal - 16d) / 2d) + 33d);
        var ramInitial = GetInitialRam(instance);
        string suffixText;
        if (ramInitial.HasValue && RamGame != RamGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixBoth", Lang.Number(ramInitial.Value, "N1"),
                Lang.Number(RamGameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixInitial", Lang.Number(ramInitial.Value, "N1"));
        else if (RamGame != RamGameActual)
            suffixText = Lang.Text("Setup.Launch.Memory.SuffixAvailable", Lang.Number(RamGameActual, "N1"));
        else
            suffixText = "";
        RamGameText = $"{Lang.Number(RamGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : "")}";
        RamUsedText = $"{Lang.Number(RamUsed, "N1")} GiB";
        RamTotalText = $" / {Lang.Number(RamTotal, "N1")} GiB";
        RamWarnVisible = RamGame == 1d && !JavaRuntime.IsGameSet64BitJava(instance) && !SystemInfo.Is32BitSystem &&
                         JavaRuntime.Javas.ExistAnyJava();
        RamTooHighVisible = RamGame / RamTotal > 0.75d;
    }

    public void RefreshRunWait(string command)
    {
        CheckAdvanceRunWaitVisible = !string.IsNullOrEmpty(command);
    }

    public void RefreshServerLogin(int type, string authServerAddress)
    {
        var authVisible = type == 2 || type == 3;
        ServerAuthVisible = authVisible;
        ServerNewProfileVisible = authVisible && ProfileUi.CanCreateOtherProfile();
        ServerAuthLockVisible = type != 0 && type != 1;
        ServerAuthEditable = !Config.InstanceAuth.AuthLocked[PageInstanceLeft.McInstance.PathInstance];
        ServerLoginLockVisible = !ServerAuthEditable;
        if (!authVisible)
        {
            ServerHttpWarningVisible = false;
            ServerSslWarningVisible = false;
        }
        else if (authServerAddress.StartsWithF("https://"))
        {
            ServerHttpWarningVisible = false;
            ServerSslWarningVisible = true;
        }
        else if (authServerAddress.StartsWithF("http://"))
        {
            ServerHttpWarningVisible = true;
            ServerSslWarningVisible = false;
        }
        else
        {
            ServerHttpWarningVisible = false;
            ServerSslWarningVisible = false;
        }
    }

    public void RefreshTitleEmptyHint(string title)
    {
        TitleEmptyHintVisible = title.Length == 0;
    }

    public void RefreshTitleHint(bool useDefault)
    {
        TitleHintText = useDefault
            ? Lang.Text("Common.Option.Default")
            : Lang.Text("Instance.Setup.FollowGlobal");
    }

    [RelayCommand]
    private void SetRamType(string? tag)
    {
        if (Animations.AniExitsAnyControl())
            return;
        if (tag is null)
            return;

        var slash = tag.IndexOf('/');
        if (slash < 0)
            return;

        ConfigService.TrySetValue(tag[..slash], int.Parse(tag[(slash + 1)..]), PageInstanceLeft.McInstance.PathInstance);
    }

    public void ResetSettings()
    {
        if (!Config.InstanceAuth.AuthLocked[PageInstanceLeft.McInstance.PathInstance])
            Config.InstanceAuth.Reset(PageInstanceLeft.McInstance.PathInstance);
        Config.Instance.Reset(PageInstanceLeft.McInstance.PathInstance);
        Common.Log("[Setup] 已初始化实例独立设置");
        HintService.Show(Hints.Success(Lang.Text("Instance.Setup.Initialize.Success"), log: false));
    }

    [RelayCommand]
    private async Task ApplyLittleSkin()
    {
        try
        {
            if (!string.IsNullOrEmpty(ServerAuthServerText) &&
                ServerAuthServerText != "https://littleskin.cn/api/yggdrasil")
            {
                var overrideResult = await Dialog.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Title"),
                    Content = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Instance.Setup.Server.LittleSkin.Override.Continue")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel")))
                }).ConfigureAwait(true);
                if (overrideResult.Button == 2)
                    return;
            }
            ServerAuthServerText = "https://littleskin.cn/api/yggdrasil";
            ServerAuthRegisterText = "https://littleskin.cn/auth/register";
            ServerAuthNameText = Lang.Text("Instance.Setup.Server.LittleSkin.Name");
        }
        catch (Exception ex)
        {
            Common.Log(ex, "应用 LittleSkin 登录设置失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
    }

    [RelayCommand]
    private void CreateServerProfile()
    {
        if (!ProfileUi.CanCreateOtherProfile())
            return;

        Launcher.frmMain.PageChange(new FormMain.PageStackData { page = FormMain.PageType.Launch });
        PageLoginAuth.draggedAuthServer = ServerAuthServerText;
        Common.RunInNewThread(() =>
        {
            Thread.Sleep(150);
            Common.RunInUi(() => Launcher.frmLaunchLeft.RefreshPage(true, GameLaunch.McLoginType.Auth));
        });
    }

    [RelayCommand]
    private async Task LockServerAuth()
    {
        ServerAuthLockEnabled = false;
        try
        {
            var lockResult = await Dialog.ShowAsync(new TextDialogRequest
            {
                Title = Lang.Text("Instance.Setup.Server.LockLoginMethod.Title"),
                Content = Lang.Text("Instance.Setup.Server.LockLoginMethod.Message"),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            }).ConfigureAwait(true);
            if (lockResult.Button == 1)
            {
                Config.InstanceAuth.AuthLocked[PageInstanceLeft.McInstance.PathInstance] = true;
                Launcher.frmInstanceSetup?.Reload();
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "锁定登录方式失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        }
        finally
        {
            ServerAuthLockEnabled = true;
        }
    }

    [RelayCommand]
    private void OpenGlobalSettings()
    {
        Launcher.frmMain.PageChange(FormMain.PageType.Setup);
    }
}
