using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Platform;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Launch;

namespace PCL;

/// <summary>登录档案列表的视图模型：档案集合、选择、删除、UUID 操作与新建档案引导。</summary>
public partial class PageLoginProfileViewModel : ViewModelBase
{
    private const string DefaultAuthServer = "https://littleskin.cn/api/yggdrasil";

    public ObservableCollection<ProfileItem> Profiles { get; } = [];

    [ObservableProperty] public partial bool ShowSelectHint { get; set; } = true;

    [ObservableProperty] public partial bool ShowCreateHint { get; set; }

    [ObservableProperty] public partial bool ShowMicrosoftHint { get; set; }

    /// <summary>档案集合重建完成后触发，页面据此重建列表控件。</summary>
    public event Action? ProfilesUpdated;

    /// <summary>当前档案发生变化，需要启动页重新选择登录子视图。</summary>
    public event Action? RefreshRequested;

    /// <summary>新建档案时请求切换到指定的验证方式。</summary>
    public event Action<GameLaunch.McLoginType>? LoginTypeRequested;

    [RelayCommand]
    public async Task ReloadAsync()
    {
        LauncherCommon.Log("[Profile] 刷新档案列表");
        var profiles = await RunAsync(ct => Task.Run(
            () =>
            {
                ProfileService.Load();
                return ProfileService.Profiles.ToList();
            },
            ct));
        if (profiles is null)
            return;

        try
        {
            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(new ProfileItem(profile));
            LauncherCommon.Log("[Profile] 档案列表刷新完成");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Launch.Account.Profile.Error.Read"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Read"));
        }
        finally
        {
            var isEmpty = Profiles.Count == 0;
            ShowSelectHint = !isEmpty;
            ShowMicrosoftHint = isEmpty;
            ShowCreateHint = isEmpty;
            if (isEmpty)
                States.Hint.LaunchWithProfile = true;
            ProfilesUpdated?.Invoke();
        }
    }

    [RelayCommand]
    public async Task CreateProfileAsync()
    {
        try
        {
            var includeOthers = PageInstanceSetupViewModel.CanCreateOtherProfile();
            var options = new List<DialogOption>
            {
                new()
                {
                    Text = Lang.Text("Launch.Account.Type.Microsoft"),
                    SvgIcon = "lucide/shield-check"
                }
            };
            if (includeOthers)
            {
                options.Add(new DialogOption
                {
                    Text = Lang.Text("Launch.Account.Type.ThirdParty"),
                    SvgIcon = "lucide/network"
                });
                options.Add(new DialogOption
                {
                    Text = Lang.Text("Launch.Account.Type.Offline"),
                    SvgIcon = "lucide/link-2-off"
                });
            }
            var result = await Dialog.ShowAsync(
                new SelectDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"),
                    Options = options,
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Action.Continue")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                    HighLight = false
                });
            if (!result.IsConfirmed)
                return;

            var type = result.SelectedIndex switch
            {
                0 => GameLaunch.McLoginType.Ms,
                1 => GameLaunch.McLoginType.Auth,
                _ => GameLaunch.McLoginType.Legacy
            };
            ProfileService.IsCreatingProfile = true;
            if (type == GameLaunch.McLoginType.Auth && !await TrySelectAuthServerAsync())
            {
                ProfileService.IsCreatingProfile = false;
                return;
            }

            LoginTypeRequested?.Invoke(type);
        }
        catch (Exception ex)
        {
            ProfileService.IsCreatingProfile = false;
            LauncherCommon.Log(ex, "选择验证方式失败", LauncherLogLevel.Feedback);
        }
    }

    private async Task<bool> TrySelectAuthServerAsync()
    {
        var serverDialog = await Dialog.ShowAsync(
            new AuthServerDialogRequest
            {
                Title = Lang.Text("Launch.Account.Auth.SelectServer"),
                DefaultServer = DefaultAuthServer,
                Presets = new Dictionary<string, string>
                {
                    [Lang.Text("Launch.Account.Auth.Preset.LittleSkin")] = DefaultAuthServer,
                    [Lang.Text("Common.Option.Customize")] = ""
                },
                Buttons = DialogButtons.OkCancel,
                HighLight = false
            });
        var server = serverDialog.IsConfirmed ? serverDialog.InputText?.Trim() ?? "" : "";
        if (string.IsNullOrWhiteSpace(server))
            return false;

        PageLoginAuth.PendingAuthServerOAuthSupported =
            await PageLoginAuth.IsOAuthSupportedAsync(server);
        PageLoginAuth.PendingAuthServer = server;
        return true;
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(ProfileItem item)
    {
        try
        {
            var result = await Dialog.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"),
                    Content = Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
            if (result.Button == 2)
                return;
            ProfileService.Remove(item.Profile);
            HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Deleted")));
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除档案失败", LauncherLogLevel.Feedback);
        }
    }

    [RelayCommand]
    public async Task EditServerNameAsync(ProfileItem item)
    {
        var current = ProfileService.Profiles.FirstOrDefault(profile =>
            profile.ProfileId == item.Profile.ProfileId);
        if (current is null)
            return;

        var result = await Dialog.ShowAsync(
            new InputDialogRequest
            {
                Title = Lang.Text("Launch.Account.Profile.EditServerName.Title"),
                Content = Lang.Text("Launch.Account.Profile.EditServerName.Message"),
                DefaultValue = current.ServerName ?? "",
                Buttons = DialogButtons.OkCancel,
                HighLight = false
            });
        if (!result.IsConfirmed)
            return;

        var updated = current.Clone();
        updated.ServerName = result.InputText ?? "";
        ProfileService.Update(current, updated);
        if (ReferenceEquals(ProfileService.Current, current))
            ProfileService.Select(updated);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Saved")));
    }

    [RelayCommand]
    public static void EditUuid(ProfileItem item)
    {
        LaunchLoginHelper.EditOfflineUuid(item.Profile);
    }

    [RelayCommand]
    public static void CopyUuid(ProfileItem item)
    {
        var clipboard = PlatformServices.Clipboard;
        if (clipboard is null)
            return;
        clipboard.SetText(item.Profile.Uuid);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Uuid.Hint")));
    }

    [RelayCommand]
    public void Select(ProfileItem item)
    {
        ProfileService.Select(item.Profile);
        ProfileService.IsCreatingProfile = false;
        LauncherCommon.Log(
            $"[Profile] 选定档案: {item.Profile.UserName}, 以 {item.Profile.ProfileType} 方式验证");
        GameLaunch.mcLoginMsLoader.State = LoadState.Waiting;
        GameLaunch.mcLoginAuthLoader.State = LoadState.Waiting;
        GameLaunch.mcLoginLegacyLoader.State = LoadState.Waiting;
        RefreshRequested?.Invoke();
    }
}

/// <summary>档案列表中的一条档案及其展示信息。</summary>
public sealed class ProfileItem
{
    public ProfileItem(McProfile profile)
    {
        Profile = profile;
        Info = LaunchLoginHelper.GetProfileInfo(profile);
        var logoPath = GetHeadCachePath(profile);
        if (logoPath is not null)
        {
            Logo = logoPath;
            SvgIcon = string.Empty;
        }
        else
        {
            Logo = string.Empty;
            SvgIcon = "lucide/user";
        }
    }

    public McProfile Profile { get; }

    public string Username => Profile.UserName;

    public string Info { get; }

    public string Logo { get; }

    public string SvgIcon { get; }

    private static string? GetHeadCachePath(McProfile profile)
    {
        if (string.IsNullOrEmpty(profile.SkinHeadId))
            return null;
        var path = Path.Combine(
            LauncherPaths.Temp,
            "Cache",
            "Skin",
            "Head",
            profile.SkinHeadId + ".png");
        var file = new FileInfo(path);
        return file is { Exists: true, Length: > 0L } ? path : null;
    }
}