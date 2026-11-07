using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Launch;

namespace PCL;

/// <summary>当前档案卡片的视图模型：档案信息、皮肤地址加载与档案相关菜单命令。</summary>
public partial class PageLoginProfileSkinViewModel : ViewModelBase
{
    private int _loadRevision;

    [ObservableProperty] public partial string ProfileName { get; set; } = string.Empty;

    [ObservableProperty] public partial string ProfileTypeText { get; set; } = string.Empty;

    [ObservableProperty] public partial bool EditVisible { get; set; }

    [ObservableProperty] public partial bool HasProfile { get; set; }

    [ObservableProperty] public partial bool IsSkinLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string SkinAddress { get; set; } = string.Empty;

    public bool CanSave => !string.IsNullOrWhiteSpace(SkinAddress);

    /// <summary>请求页面调用皮肤控件的保存操作。</summary>
    public event Action? SaveSkinRequested;

    /// <summary>请求页面调用皮肤控件的刷新操作。</summary>
    public event Action? RefreshSkinRequested;

    /// <summary>当前档案被清除，需要启动页重新选择登录子视图。</summary>
    public event Action? RefreshRequested;

    public async Task ReloadAsync()
    {
        var revision = ++_loadRevision;
        SkinAddress = "";
        var profile = ProfileService.Current;
        if (profile is null)
        {
            HasProfile = false;
            ProfileName = "";
            ProfileTypeText = "";
            EditVisible = false;
            IsSkinLoading = false;
            SkinAddress = "";
            return;
        }

        HasProfile = true;
        ProfileName = profile.UserName;
        ProfileTypeText = LaunchLoginHelper.GetProfileInfo(profile);
        EditVisible = profile.ProfileType is not ProfileType.Offline;
        IsSkinLoading = true;

        var address = "";
        try
        {
            if (profile.ProfileType == ProfileType.Offline)
                address = LaunchLoginHelper.GetOfflineSkinAddress(profile.Uuid);
            else
                address = await Task.Run(() =>
                {
                    try
                    {
                        return LaunchLoginHelper.GetOnlineSkinAddress(profile);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "Launch", $"获取在线皮肤失败：{profile.UserName}");
                        return LaunchLoginHelper.GetOfflineSkinAddress(profile.Uuid);
                    }
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", $"加载皮肤失败：{profile.UserName}");
        }

        if (revision != _loadRevision)
            return;
        SkinAddress = address;
        IsSkinLoading = false;
    }

    [RelayCommand]
    private void SelectProfile()
    {
        ProfileService.Select(null);
        RefreshRequested?.Invoke();
    }

    [RelayCommand]
    private void SaveSkin()
    {
        SaveSkinRequested?.Invoke();
    }

    [RelayCommand]
    private void RefreshSkin()
    {
        RefreshSkinRequested?.Invoke();
    }

    [RelayCommand]
    private static void ChangeSkin()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
            return;

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/closet");
    }

    [RelayCommand]
    private static void ChangeCape()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
            return;

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/closet");
    }

    [RelayCommand]
    private static void EditPassword()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
        {
            Basics.OpenPath("https://account.live.com/password/Change");
            return;
        }

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/profile");
    }

    [RelayCommand]
    private static void EditUserName()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Offline)
        {
            LaunchLoginHelper.EditOfflineUserName(profile);
            return;
        }

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/profile");
    }

    private static void OpenProfilePage(string server, string subPath)
    {
        try
        {
            var address = server;
            var authIndex = address.IndexOf(
                "api/yggdrasil/authserver",
                StringComparison.OrdinalIgnoreCase);
            if (authIndex >= 0)
                address = address[..authIndex];
            if (!address.EndsWith('/'))
                address += "/";
            Basics.OpenPath(address + subPath);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", $"打开档案网页失败：{server}/{subPath}");
        }
    }
}