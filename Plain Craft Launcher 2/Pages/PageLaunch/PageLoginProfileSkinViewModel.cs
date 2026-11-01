using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>启动页面中档案与皮肤卡片的视图模型。</summary>
public partial class PageLoginProfileSkinViewModel : ViewModelBase
{
    [ObservableProperty] private Visibility _editVisibility = Visibility.Visible;

    [ObservableProperty] private string _profileName = string.Empty;

    [ObservableProperty] private string _profileTypeText = string.Empty;

    public void Load(McProfile profile)
    {
        ProfileName = profile.UserName;
        ProfileTypeText = ProfileUi.GetProfileInfo(profile);
        EditVisibility = profile.ProfileType is ProfileType.Microsoft or ProfileType.Authlib
            or ProfileType.YggdrasilConnect
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
