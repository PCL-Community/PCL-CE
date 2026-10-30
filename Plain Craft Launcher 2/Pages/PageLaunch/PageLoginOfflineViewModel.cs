using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Utils.Validate;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>离线登录页面的视图模型。</summary>
public partial class PageLoginOfflineViewModel : ViewModelBase
{
    public enum UuidMode
    {
        Standard,
        Legacy,
        Custom
    }

    [ObservableProperty] private string _userName = string.Empty;

    [ObservableProperty] private string _uuid = string.Empty;

    [ObservableProperty] private UuidMode _mode = UuidMode.Standard;

    public bool ShowCustomUuid => Mode == UuidMode.Custom;

    partial void OnModeChanged(UuidMode value) => OnPropertyChanged(nameof(ShowCustomUuid));

    [RelayCommand]
    public void Back()
    {
        ProfileService.IsCreatingProfile = false;
        ModBase.RunInUi(() => ModMain.frmLaunchLeft?.RefreshPage());
    }

    [RelayCommand]
    public void ChangeUuidMode(UuidMode mode) => Mode = mode;

    [RelayCommand]
    public async Task LoginAsync()
    {
        try
        {
            var username = UserName;
            if (!new RegexValidator("^[A-z0-9_]{3,16}$").Validate(username).IsValid)
            {
                var result = await ModMain.MyMsgBoxAsync(
                    Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                    Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"),
                    Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true);
                if (result == 2) return;
            }

            string userUuid;
            if (Mode == UuidMode.Custom)
            {
                var uuidInput = Uuid.Replace("-", "");
                var uuidValidateResult = new RegexValidator("^[a-fA-F0-9]{32}$").Validate(uuidInput);
                if (!uuidValidateResult.IsValid)
                {
                    HintService.Hint(Lang.Text("Launch.Account.Offline.InvalidUuid", uuidValidateResult),
                        HintType.Error);
                    return;
                }

                userUuid = uuidInput;
            }
            else if (Mode == UuidMode.Legacy)
            {
                userUuid = ProfileUi.GetOfflineUuid(username, isLegacy: true);
            }
            else
            {
                userUuid = ProfileUi.GetOfflineUuid(username);
            }

            var newProfile = new McProfile
            {
                ProfileType = ProfileType.Offline,
                Uuid = userUuid,
                UserName = username,
                Description = "",
                AccessToken = userUuid,
                ClientToken = userUuid,
                ProfileId = Guid.NewGuid().ToString("N")
            };
            ProfileService.Add(newProfile);
            ProfileService.IsCreatingProfile = false;
            HintService.Hint(Lang.Text("Launch.Account.Profile.Created"), HintType.Success);
            ModBase.RunInUi(() => ModMain.frmLaunchLeft?.RefreshPage());
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "创建离线档案失败", ModBase.LogLevel.Feedback);
        }
    }
}
