using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>登录档案页面的视图模型。</summary>
public partial class PageLoginProfileViewModel : ViewModelBase
{
    public ObservableCollection<PageLoginProfile.ProfileItem> Profiles { get; } = [];

    [ObservableProperty] private bool _showCreateHint;

    [ObservableProperty] private bool _showMicrosoftHint;

    [RelayCommand]
    public async Task ReloadAsync()
    {
        Common.Log("[Profile] 刷新档案列表");
        var profiles = await RunAsync(ct => Task.Run(() =>
        {
            ProfileService.Load();
            return ProfileService.Profiles.ToList();
        }, ct));
        if (profiles is null) return;
        try
        {
            Profiles.Clear();
            foreach (var profile in profiles)
                Profiles.Add(new PageLoginProfile.ProfileItem(profile));
            Common.Log("[Profile] 档案列表刷新完成");
        }
        catch (Exception ex)
        {
            Common.Log(ex, Lang.Text("Launch.Account.Profile.Error.Read"), Common.LogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Read"));
        }

        var isEmpty = profiles.Count == 0;
        ShowMicrosoftHint = isEmpty;
        ShowCreateHint = isEmpty;
        if (isEmpty) States.Hint.LaunchWithProfile = true;
    }

    [RelayCommand]
    public async Task CreateProfileAsync()
    {
        await ProfileUi.CreateProfileAsync();
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task DeleteProfileAsync(PageLoginProfile.ProfileItem item)
    {
        var result = await Dialog.ShowAsync(
            title: Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"),
            content: Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
            button1: Lang.Text("Common.Action.Continue"),
            button2: Lang.Text("Common.Action.Cancel"),
            isWarn: true);
        if (result == 2) return;
        ProfileUi.RemoveProfile(item.Profile);
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task EditServerNameAsync(PageLoginProfile.ProfileItem item)
    {
        var name = await Dialog.InputAsync(
            title: Lang.Text("Launch.Account.Profile.EditServerName.Title"),
            content: Lang.Text("Launch.Account.Profile.EditServerName.Message"),
            defaultValue: item.Profile.ServerName ?? "");
        if (name is not null) ProfileUi.EditAuthServerName(item.Profile, name);
    }

    [RelayCommand]
    public void EditUuid(PageLoginProfile.ProfileItem item) => ProfileUi.EditOfflineUuid(item.Profile);

    [RelayCommand]
    public void CopyUuid(PageLoginProfile.ProfileItem item) => Common.ClipboardSet(item.Profile.Uuid);

    public void Select(PageLoginProfile.ProfileItem item)
    {
        ProfileService.Select(item.Profile);
        Common.Log($"[Profile] 选定档案: {item.Profile.UserName}, 以 {item.Profile.ProfileType} 方式验证");
        GameLaunch.mcLoginMsLoader.State = Common.LoadState.Waiting;
        GameLaunch.mcLoginAuthLoader.State = Common.LoadState.Waiting;
        GameLaunch.mcLoginLegacyLoader.State = Common.LoadState.Waiting;
    }
}
