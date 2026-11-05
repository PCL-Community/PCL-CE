using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Java.UserPreference;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.ViewModel;
using PCL.Navigation;
using PCL.Views.Pages.Launch;

namespace PCL;

/// <summary>实例独立设置页的视图模型。</summary>
public partial class PageInstanceSetupViewModel : ViewModelBase
{
    private const string DefaultAuthServer = "https://littleskin.cn/api/yggdrasil";

    [ObservableProperty] public partial bool IndieEnabled { get; set; } = true;

    [ObservableProperty] public partial string TitleText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool UseGlobalTitle { get; set; }

    [ObservableProperty] public partial string InfoText { get; set; } = string.Empty;

    [ObservableProperty] public partial int MemorySolution { get; set; }

    [ObservableProperty] public partial double CustomMemorySize { get; set; } = 13d;

    [ObservableProperty] public partial double CustomInitialMemorySize { get; set; }

    [ObservableProperty] public partial string ServerEnterText { get; set; } = string.Empty;

    [ObservableProperty] public partial int LoginRequirement { get; set; }

    [ObservableProperty] public partial string AuthServerAddress { get; set; } = string.Empty;

    [ObservableProperty] public partial string AuthRegisterAddress { get; set; } = string.Empty;

    [ObservableProperty] public partial string AuthDisplayName { get; set; } = string.Empty;

    [ObservableProperty] public partial int RendererIndex { get; set; }

    [ObservableProperty] public partial string ClasspathHead { get; set; } = string.Empty;

    [ObservableProperty] public partial string JvmArgs { get; set; } = string.Empty;

    [ObservableProperty] public partial string GameArgs { get; set; } = string.Empty;

    [ObservableProperty] public partial string PreLaunchCommand { get; set; } = string.Empty;

    [ObservableProperty] public partial bool PreLaunchCommandWait { get; set; }

    [ObservableProperty] public partial bool DisableLwjglUnsafeAgent { get; set; }

    [ObservableProperty] public partial bool DisableModUpdate { get; set; }

    [ObservableProperty] public partial bool DisableAssetVerify { get; set; }

    [ObservableProperty] public partial bool UseProxy { get; set; }

    [ObservableProperty] public partial bool IgnoreJavaCompatibility { get; set; }

    [ObservableProperty] public partial bool DisableJlw { get; set; }

    [ObservableProperty] public partial bool UseDebugLog4j { get; set; }

    [ObservableProperty] public partial bool DisableLf { get; set; }

    [ObservableProperty] public partial string TitleHintText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool TitleDefaultVisible { get; set; }

    [ObservableProperty] public partial bool RamCustomEnabled { get; set; } = true;

    [ObservableProperty] public partial bool RamInitialCustomEnabled { get; set; } = true;

    [ObservableProperty] public partial double RamMaxValue { get; set; } = 13d;

    [ObservableProperty] public partial string RamUsedText { get; set; } = "0 GiB";

    [ObservableProperty] public partial string RamTotalText { get; set; } = " / 0 GiB";

    [ObservableProperty] public partial string RamGameText { get; set; } = "0 GiB";

    [ObservableProperty] public partial double RamUsedWeight { get; set; } = 1d;

    [ObservableProperty] public partial double RamGameWeight { get; set; } = 1d;

    [ObservableProperty] public partial double RamEmptyWeight { get; set; } = 1d;

    [ObservableProperty] public partial bool Ram32WarningVisible { get; set; }

    [ObservableProperty] public partial bool RamHighWarningVisible { get; set; }

    [ObservableProperty] public partial bool AuthVisible { get; set; }

    [ObservableProperty] public partial bool AuthEditable { get; set; } = true;

    [ObservableProperty] public partial bool AuthLockVisible { get; set; }

    [ObservableProperty] public partial bool AuthLockEnabled { get; set; } = true;

    [ObservableProperty] public partial bool NewProfileVisible { get; set; } = true;

    [ObservableProperty] public partial bool LoginLockVisible { get; set; }

    [ObservableProperty] public partial bool SslWarningVisible { get; set; }

    [ObservableProperty] public partial bool HttpWarningVisible { get; set; }

    [ObservableProperty] public partial bool RunWaitVisible { get; set; }

    [ObservableProperty] public partial bool IsArm64System { get; set; }

    [ObservableProperty] public partial bool IsJavaLoading { get; set; }

    [ObservableProperty] public partial bool HasJavaLoadError { get; set; }

    [ObservableProperty] public partial string? JavaLoadError { get; set; }

    public McInstance? Instance { get; private set; }

    public bool IsSyncing { get; set; }

    public event Action? ReloadRequested;

    public void SetInstance(McInstance? instance)
    {
        Instance = instance;
    }

    public void Load()
    {
        IsSyncing = true;
        try
        {
            if (Instance is null)
            {
                TitleText = string.Empty;
                InfoText = string.Empty;
                IndieEnabled = false;
                return;
            }

            var path = Instance.PathInstance;
            if (Config.Instance.AssetVerifySolutionV1[path] == 2)
            {
                LauncherCommon.Log("[Setup] 已迁移老版本的关闭文件校验设置", LauncherLogLevel.Debug);
                Config.Instance.AssetVerifySolutionV1[path] = 0;
                Config.Instance.DisableAssetVerifyV2[path] = true;
            }

            IndieEnabled = Config.Instance.IndieV2[path];
            TitleText = Config.Instance.Title[path];
            UseGlobalTitle = Config.Instance.UseGlobalTitle[path];
            InfoText = Config.Instance.TypeInfo[path];
            RefreshTitleHint();

            MemorySolution = Config.Instance.MemorySolution[path];
            CustomMemorySize = Math.Clamp(Config.Instance.CustomMemorySize[path], 1, 49);
            CustomInitialMemorySize = Math.Clamp(
                Config.Instance.CustomInitialMemorySize[path],
                0,
                CustomMemorySize);
            RefreshRam();

            ServerEnterText = Config.Instance.ServerToEnter[path];
            LoginRequirement = Math.Clamp(Config.InstanceAuth.LoginRequirementSolution[path], 0, 3);
            AuthServerAddress = Config.InstanceAuth.AuthServerAddress[path];
            AuthDisplayName = Config.InstanceAuth.AuthServerDisplayName[path];
            AuthRegisterAddress = Config.InstanceAuth.AuthRegisterAddress[path];
            RefreshServerLogin(LoginRequirement);

            RendererIndex = Math.Clamp(Config.Instance.Renderer[path], 0, 4);
            ClasspathHead = Config.Instance.ClasspathHead[path];
            JvmArgs = Config.Instance.JvmArgs[path];
            GameArgs = Config.Instance.GameArgs[path];
            PreLaunchCommand = Config.Instance.PreLaunchCommand[path];
            PreLaunchCommandWait = Config.Instance.PreLaunchCommandWait[path];
            DisableLwjglUnsafeAgent = Config.Instance.DisableLwjglUnsafeAgent[path];
            DisableModUpdate = Config.Instance.DisableModUpdate[path];
            DisableAssetVerify = Config.Instance.DisableAssetVerifyV2[path];
            UseProxy = Config.Instance.UseProxy[path];
            IgnoreJavaCompatibility = Config.Instance.IgnoreJavaCompatibility[path];
            IsArm64System = SystemInfo.IsArm64System;
            DisableJlw = IsArm64System || Config.Instance.DisableJlw[path];
            UseDebugLog4j = Config.Instance.UseDebugLof4j2Config[path];
            DisableLf = Config.Instance.DisableLF[path];
            RefreshRunWait();
        }
        finally
        {
            IsSyncing = false;
        }
    }

    public bool SetIndie(bool enabled)
    {
        if (Instance is null)
            return false;
        Config.Instance.IndieV2[Instance.PathInstance] = enabled;
        IndieEnabled = enabled;
        return true;
    }

    public void SetTitle(string title)
    {
        if (Instance is null)
            return;
        TitleText = title;
        Config.Instance.Title[Instance.PathInstance] = title;
        RefreshTitleHint();
    }

    public void SetUseGlobalTitle(bool value)
    {
        if (Instance is null)
            return;
        UseGlobalTitle = value;
        Config.Instance.UseGlobalTitle[Instance.PathInstance] = value;
        RefreshTitleHint();
    }

    public void SetInfo(string info)
    {
        if (Instance is null)
            return;
        InfoText = info;
        Config.Instance.TypeInfo[Instance.PathInstance] = info;
    }

    public void SetMemorySolution(int solution)
    {
        if (IsSyncing || Instance is null)
            return;
        MemorySolution = solution;
        Config.Instance.MemorySolution[Instance.PathInstance] = solution;
        RefreshRam();
    }

    public void SetMemoryValues(double max, double initial)
    {
        if (Instance is null)
            return;
        var path = Instance.PathInstance;
        CustomMemorySize = Math.Clamp(max, 1, 49);
        CustomInitialMemorySize = Math.Clamp(initial, 0, CustomMemorySize);
        Config.Instance.CustomMemorySize[path] = (int)Math.Round(CustomMemorySize);
        Config.Instance.CustomInitialMemorySize[path] = (int)Math.Round(CustomInitialMemorySize);
        RefreshRam();
    }

    public void SetServerEnter(string text)
    {
        if (Instance is null)
            return;
        ServerEnterText = text;
        Config.Instance.ServerToEnter[Instance.PathInstance] = text;
    }

    public bool SetLoginRequirement(int index)
    {
        if (Instance is null || index < 0)
            return false;
        LoginRequirement = index;
        RefreshServerLogin(index);
        if (index is 2 or 3 && string.IsNullOrWhiteSpace(AuthServerAddress))
            return false;
        Config.InstanceAuth.LoginRequirementSolution[Instance.PathInstance] = index;
        return true;
    }

    public void SetAuthServer(string address)
    {
        if (Instance is null)
            return;
        AuthServerAddress = address;
        Config.InstanceAuth.AuthServerAddress[Instance.PathInstance] = address;
        RefreshServerLogin(LoginRequirement);
    }

    public bool NormalizeAuthServer()
    {
        var address = AuthServerAddress.Trim();
        if (string.IsNullOrWhiteSpace(address))
            return false;
        var changed = false;
        if (!address.EndsWith("/api/yggdrasil/", StringComparison.OrdinalIgnoreCase)
            && !address.EndsWith("/api/yggdrasil", StringComparison.OrdinalIgnoreCase))
        {
            address = address.EndsWith('/')
                ? $"{address}api/yggdrasil"
                : $"{address}/api/yggdrasil";
            changed = true;
        }

        if (address.EndsWith("/api/yggdrasil/", StringComparison.OrdinalIgnoreCase))
        {
            address = address[..^1];
            changed = true;
        }

        if (!changed)
            return false;
        AuthServerAddress = address;
        HintService.Show(Hints.Info(Lang.Text("Instance.Setup.Server.AuthServer.AutoFormatted")));
        return true;
    }

    public void SetAuthRegister(string address)
    {
        if (Instance is null)
            return;
        AuthRegisterAddress = address;
        Config.InstanceAuth.AuthRegisterAddress[Instance.PathInstance] = address;
    }

    public void SetAuthDisplayName(string name)
    {
        if (Instance is null)
            return;
        AuthDisplayName = name;
        Config.InstanceAuth.AuthServerDisplayName[Instance.PathInstance] = name;
    }

    public void SetRenderer(int index)
    {
        if (Instance is null || index < 0)
            return;
        RendererIndex = index;
        Config.Instance.Renderer[Instance.PathInstance] = index;
    }

    public void SetClasspath(string text)
    {
        if (Instance is null)
            return;
        ClasspathHead = text;
        Config.Instance.ClasspathHead[Instance.PathInstance] = text;
    }

    public void SetJvmArgs(string text)
    {
        if (Instance is null)
            return;
        JvmArgs = text;
        Config.Instance.JvmArgs[Instance.PathInstance] = text;
    }

    public void SetGameArgs(string text)
    {
        if (Instance is null)
            return;
        GameArgs = text;
        Config.Instance.GameArgs[Instance.PathInstance] = text;
    }

    public void SetPreLaunchCommand(string text)
    {
        if (Instance is null)
            return;
        PreLaunchCommand = text;
        Config.Instance.PreLaunchCommand[Instance.PathInstance] = text;
        RefreshRunWait();
    }

    public void SetPreLaunchCommandWait(bool value)
    {
        if (Instance is null)
            return;
        PreLaunchCommandWait = value;
        Config.Instance.PreLaunchCommandWait[Instance.PathInstance] = value;
    }

    public void SetDisableLwjglUnsafeAgent(bool value)
    {
        if (Instance is null)
            return;
        DisableLwjglUnsafeAgent = value;
        Config.Instance.DisableLwjglUnsafeAgent[Instance.PathInstance] = value;
    }

    public void SetDisableModUpdate(bool value)
    {
        if (Instance is null)
            return;
        DisableModUpdate = value;
        Config.Instance.DisableModUpdate[Instance.PathInstance] = value;
    }

    public void SetDisableAssetVerify(bool value)
    {
        if (Instance is null)
            return;
        DisableAssetVerify = value;
        Config.Instance.DisableAssetVerifyV2[Instance.PathInstance] = value;
    }

    public void SetUseProxy(bool value)
    {
        if (Instance is null)
            return;
        UseProxy = value;
        Config.Instance.UseProxy[Instance.PathInstance] = value;
    }

    public void SetIgnoreJavaCompatibility(bool value)
    {
        if (Instance is null)
            return;
        IgnoreJavaCompatibility = value;
        Config.Instance.IgnoreJavaCompatibility[Instance.PathInstance] = value;
    }

    public void SetDisableJlw(bool value)
    {
        if (Instance is null || IsArm64System)
            return;
        DisableJlw = value;
        Config.Instance.DisableJlw[Instance.PathInstance] = value;
    }

    public void SetDisableLf(bool value)
    {
        if (Instance is null)
            return;
        DisableLf = value;
        Config.Instance.DisableLF[Instance.PathInstance] = value;
    }

    public void SetUseDebugLog4jOnReload(bool value)
    {
        UseDebugLog4j = value;
    }

    public void RefreshTitleHint()
    {
        TitleHintText = UseGlobalTitle
            ? Lang.Text("Common.Option.Default")
            : Lang.Text("Instance.Setup.FollowGlobal");
        TitleDefaultVisible = string.IsNullOrEmpty(TitleText);
    }

    public void RefreshRunWait()
    {
        RunWaitVisible = !string.IsNullOrEmpty(PreLaunchCommand);
    }

    public void RefreshServerLogin(int type)
    {
        var visible = type is 2 or 3;
        var editable = Instance is null || !Config.InstanceAuth.AuthLocked[Instance.PathInstance];
        AuthVisible = visible;
        AuthEditable = editable;
        NewProfileVisible = visible && CanCreateOtherProfile();
        AuthLockVisible = type is not (0 or 1);
        AuthLockEnabled = editable;
        LoginLockVisible = !editable;
        if (!visible)
        {
            SslWarningVisible = false;
            HttpWarningVisible = false;
        }
        else if (AuthServerAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            SslWarningVisible = true;
            HttpWarningVisible = false;
        }
        else if (AuthServerAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            SslWarningVisible = false;
            HttpWarningVisible = true;
        }
        else
        {
            SslWarningVisible = false;
            HttpWarningVisible = false;
        }
    }

    public void RefreshRam()
    {
        if (Instance is null)
            return;

        var instance = Instance;
        var ramGame = Math.Round(MemorySettings.GetRam(instance), 5);
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        const double bytesPerGigabyte = 1024d * 1024d * 1024d;
        var total = Math.Round(memory.Total / bytesPerGigabyte, 1);
        var available = Math.Round(memory.Available / bytesPerGigabyte, 1);
        var gameActual = Math.Round(Math.Min(ramGame, available), 5);
        var used = Math.Round(total - available, 5);
        var empty = Math.Round(Math.Clamp(total - used - ramGame, 0d, 1000d), 1);

        RamMaxValue = total switch
        {
            <= 1.5d => (int)Math.Round(Math.Max(Math.Floor((total - 0.3d) / 0.1d), 1d)),
            <= 8d => (int)Math.Round(Math.Floor((total - 1.5d) / 0.5d) + 12d),
            <= 16d => (int)Math.Round(Math.Floor((total - 8d) / 1d) + 25d),
            _ => (int)Math.Round(Math.Floor((total - 16d) / 2d) + 33d)
        };
        RamUsedWeight = Math.Max(0.01d, used);
        RamGameWeight = Math.Max(0.01d, gameActual);
        RamEmptyWeight = Math.Max(0.01d, empty);

        var ramInitial = MemorySettings.GetInitialRam(instance);
        string suffixText;
        if (ramInitial.HasValue && ramGame != gameActual)
            suffixText = Lang.Text(
                "Setup.Launch.Memory.SuffixBoth",
                Lang.Number(ramInitial.Value, "N1"),
                Lang.Number(gameActual, "N1"));
        else if (ramInitial.HasValue)
            suffixText = Lang.Text(
                "Setup.Launch.Memory.SuffixInitial",
                Lang.Number(ramInitial.Value, "N1"));
        else if (ramGame != gameActual)
            suffixText = Lang.Text(
                "Setup.Launch.Memory.SuffixAvailable",
                Lang.Number(gameActual, "N1"));
        else
            suffixText = string.Empty;

        RamUsedText = $"{Lang.Number(used, "N1")} GiB";
        RamTotalText = $" / {Lang.Number(total, "N1")} GiB";
        RamGameText =
            $"{Lang.Number(ramGame, "N1")} GiB{(suffixText.Length > 0 ? $" ({suffixText})" : string.Empty)}";

        Ram32WarningVisible = ramGame == 1d
            && !JavaRuntime.IsGameSet64BitJava(instance)
            && !SystemInfo.Is32BitSystem
            && JavaRuntime.Javas.ExistAnyJava();
        RamHighWarningVisible = total > 0d && ramGame / total > 0.75d;
        RamCustomEnabled = MemorySolution == 1;
        RamInitialCustomEnabled = MemorySolution == 1 && !Config.Launch.LockMemory;
    }

    [RelayCommand]
    public void Reset()
    {
        if (Instance is null)
            return;
        try
        {
            if (!Config.InstanceAuth.AuthLocked[Instance.PathInstance])
                Config.InstanceAuth.Reset(Instance.PathInstance);
            Config.Instance.Reset(Instance.PathInstance);
            HintService.Show(Hints.Success(Lang.Text("Instance.Setup.Initialize.Success")));
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
        }

        Load();
    }

    public async Task<bool> ApplyIndieAsync(bool desired)
    {
        if (Instance is null)
            return false;
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Warning"),
                    Content = Lang.Text("Instance.Setup.Options.InstanceIsolation.Message"),
                    Buttons = DialogButtons.Create(
                        "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                        "Common.Action.Cancel"),
                    IsWarn = true
                });
            return dialog.IsConfirmed && SetIndie(desired);
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
            return false;
        }
    }

    public async Task<bool> ApplyLittleSkinAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(AuthServerAddress) && AuthServerAddress != DefaultAuthServer)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Title"),
                        Content = Lang.Text("Instance.Setup.Server.LittleSkin.Override.Message"),
                        Buttons = DialogButtons.Create(
                            "Instance.Setup.Server.LittleSkin.Override.Continue",
                            "Common.Action.Cancel")
                    });
                if (!dialog.IsConfirmed)
                    return false;
            }

            SetAuthServer(DefaultAuthServer);
            SetAuthRegister("https://littleskin.cn/auth/register");
            SetAuthDisplayName(Lang.Text("Instance.Setup.Server.LittleSkin.Name"));
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
            return false;
        }
    }

    [RelayCommand]
    public async Task LockAuthAsync()
    {
        if (Instance is null)
            return;
        AuthLockEnabled = false;
        try
        {
            var dialog = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Setup.Server.LockLoginMethod.Title"),
                    Content = Lang.Text("Instance.Setup.Server.LockLoginMethod.Message"),
                    Buttons = DialogButtons.OkCancel,
                    IsWarn = true
                });
            if (!dialog.IsConfirmed)
                return;
            Config.InstanceAuth.AuthLocked[Instance.PathInstance] = true;
            ReloadRequested?.Invoke();
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
        }
        finally
        {
            AuthLockEnabled = true;
        }
    }

    [RelayCommand]
    public void CreateServerProfile()
    {
        if (!CanCreateOtherProfile())
            return;
        PageLoginAuth.PendingAuthServer = AuthServerAddress;
        PageRouter.Navigate(PageType.Launch);
        LauncherCommon.RunInNewThread(
            () =>
            {
                Thread.Sleep(150);
                LauncherCommon.RunInUi(() =>
                    PageLaunchLeft.Current?.RefreshPage(
                        true,
                        GameLaunch.McLoginType.Auth));
            },
            "Instance Auth Profile",
            ThreadPriority.BelowNormal);
    }

    public static bool CanCreateOtherProfile()
    {
#if DEBUG || DEBUGCI
        return true;
#else
        try
        {
            return ProfileService.HasMicrosoftProfile
                   || (Lang.IsFeaturesUnrestricted && ProfileService.Profiles.Count > 0)
                   || !NetworkHelper.IsNetworkAvailable();
        }
        catch
        {
            return false;
        }
#endif
    }

    public async Task<bool> ApplyRendererAsync(int index)
    {
        if (IsSyncing || Instance is null || index < 0)
            return false;
        var previous = Math.Clamp(Config.Instance.Renderer[Instance.PathInstance], 0, 4);
        try
        {
            if (!States.Hint.Renderer && index != 0)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Warning"),
                        Content = Lang.Text("Setup.Launch.Advanced.Renderer.Warning.Message"),
                        Buttons = DialogButtons.Create(
                            "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                            "Common.Action.Cancel"),
                        IsWarn = true
                    });
                if (!dialog.IsConfirmed)
                {
                    RendererIndex = previous;
                    return false;
                }

                States.Hint.Renderer = true;
            }

            SetRenderer(index);
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
            return false;
        }
    }

    public async Task<bool> ApplyDebugLog4jAsync(bool enabled)
    {
        if (IsSyncing || Instance is null)
            return false;
        try
        {
            if (enabled && !States.Hint.DebugLog4j2Config)
            {
                var dialog = await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Warning"),
                        Content = Lang.Text("Instance.Setup.Advanced.UseDebugLog4j.Message"),
                        Buttons = DialogButtons.Create(
                            "Setup.Launch.Advanced.Renderer.Warning.Confirm",
                            "Common.Action.Cancel"),
                        IsWarn = true
                    });
                if (!dialog.IsConfirmed)
                {
                    UseDebugLog4j = false;
                    return false;
                }

                States.Hint.DebugLog4j2Config = true;
            }

            UseDebugLog4j = enabled;
            Config.Instance.UseDebugLof4j2Config[Instance.PathInstance] = enabled;
            return true;
        }
        catch (Exception ex)
        {
            ReportError("Instance.Setup.Error.OperationFailed", ex);
            return false;
        }
    }

    public async Task<IReadOnlyList<JavaOption>> LoadJavaOptionsAsync()
    {
        IsJavaLoading = true;
        HasJavaLoadError = false;
        JavaLoadError = null;
        try
        {
            var preference = Instance is null
                ? new UseGlobalPreference()
                : JavaRuntime.GetInstanceJavaPreference(Instance);
            List<JavaEntry>? javas = null;
            try
            {
                javas = await Task.Run(() => JavaRuntime.Javas.GetSortedJavaList().ToList());
            }
            catch (Exception ex)
            {
                JavaLoadError = ex.Message;
                HasJavaLoadError = true;
                LauncherCommon.Log(
                    ex,
                    "更新实例设置 Java 下拉框失败",
                    LauncherLogLevel.Feedback,
                    userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
                return [];
            }

            var options = new List<JavaOption>
            {
                new(
                    Lang.Text("Instance.Setup.FollowGlobal"),
                    null,
                    new UseGlobalPreference()),
                new(
                    Lang.Text("Instance.Setup.Options.Java.AutoSelect"),
                    null,
                    new AutoSelect())
            };

            var relative = new UseRelativePath(Path.Combine("jre", "bin", "java.exe"));
            var absolutePath = Path.GetFullPath(
                Path.Combine(Basics.ExecutableDirectory, relative.RelativePath));
            options.Add(
                new JavaOption(
                    Lang.Text("Instance.Setup.Options.Java.SelectRelative"),
                    Lang.Text(
                        "Instance.Setup.Options.Java.RelativePathToolTip",
                        relative.RelativePath,
                        absolutePath),
                    relative));

            options.AddRange(
                (javas ?? []).Select(java => new JavaOption(
                    java.ToString(),
                    Lang.Text(
                        "Instance.Setup.Options.Java.Details.ToolTip",
                        java.Installation.JavaExePath,
                        java.Installation.Version,
                        java.Source),
                    new ExistingJava(java.Installation.JavaExePath),
                    java)));

            var selected = options.FirstOrDefault(option =>
                    PreferencesEqual(option.Preference, preference))
                ?? options.FirstOrDefault(option => option.Preference is AutoSelect)
                ?? options.FirstOrDefault();
            foreach (var option in options)
                option.IsSelected = ReferenceEquals(option, selected);

            if (!JavaRuntime.Javas.ExistAnyJava())
                return
                [
                    new JavaOption(
                        Lang.Text("Instance.Setup.Options.Java.NoRuntime"),
                        Lang.Text("Instance.Setup.Options.Java.NoRuntime.ToolTip"),
                        new AutoSelect(),
                        isEnabled: false,
                        isSelected: true)
                ];

            return options;
        }
        finally
        {
            IsJavaLoading = false;
        }
    }

    public async Task<bool> SelectJavaAsync(JavaOption option)
    {
        if (Instance is null)
            return false;
        JavaPreference preference;
        switch (option.Preference)
        {
            case UseRelativePath:
            {
                var file = SystemDialogs.SelectFile(
                    Lang.Text("Setup.Java.SelectFile.Filter"),
                    Lang.Text("Setup.Java.SelectFile.Title"),
                    Basics.ExecutableDirectory);
                if (string.IsNullOrWhiteSpace(file))
                    return false;

                var absolute = Path.GetFullPath(file);
                if (!absolute.StartsWith(Basics.ExecutableDirectory, StringComparison.Ordinal))
                {
                    HintService.Show(
                        Hints.Error(Lang.Text("Instance.Setup.Options.Java.PathOutOfRange")));
                    return false;
                }

                preference = new UseRelativePath(
                    Path.GetRelativePath(Basics.ExecutableDirectory, absolute));
                break;
            }
            default:
                preference = option.Preference;
                break;
        }

        Config.Instance.SelectedJava[Instance.PathInstance] =
            JsonSerializer.Serialize(preference, JsonCompat.SerializerOptions);
        RefreshRam();
        return true;
    }

    private static bool PreferencesEqual(JavaPreference left, JavaPreference right)
    {
        return (left, right) switch
        {
            (UseGlobalPreference, UseGlobalPreference) => true,
            (AutoSelect, AutoSelect) => true,
            (UseRelativePath a, UseRelativePath b) => a.RelativePath == b.RelativePath,
            (ExistingJava a, ExistingJava b) => string.Equals(
                a.JavaExePath,
                b.JavaExePath,
                StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void ReportError(string titleKey, Exception ex)
    {
        LauncherCommon.Log(
            ex,
            Lang.Text(titleKey),
            LauncherLogLevel.Feedback,
            userSummary: Lang.Text("Instance.Setup.Error.OperationFailed"));
        HintService.Show(Hints.Error(Lang.Text("Instance.Setup.Error.OperationFailed")));
    }
}

/// <summary>实例设置页 Java 下拉框中的一个选项。</summary>
public sealed class JavaOption(
    string text,
    string? toolTip,
    JavaPreference preference,
    JavaEntry? java = null,
    bool isEnabled = true,
    bool isSelected = false)
{
    public string Text { get; } = text;

    public string? ToolTip { get; } = toolTip;

    public JavaPreference Preference { get; } = preference;

    public JavaEntry? Java { get; } = java;

    public bool IsEnabled { get; } = isEnabled;

    public bool IsSelected { get; set; } = isSelected;
}