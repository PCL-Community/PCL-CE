using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL;

public partial class PageLoginProfile : IPageView
{
    private readonly PageLoginProfileViewModel _vm = new();

    public PageLoginProfile()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.PropertyChanged += VmOnPropertyChanged;
        Loaded += (_, _) => Reload();
    }

    public void OnNavigatedTo(object? parameter) => Reload();

    public void OnNavigatedFrom()
    {
    }

    public void Reload()
    {
        _vm.ReloadCommand.Execute(null);
        Launcher.frmLoginProfileSkin = null;
    }

    public void RefreshProfileList() => _vm.ReloadCommand.Execute(null);

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PageLoginProfileViewModel.ShowCreateHint):
                HintCreate.Visibility = _vm.ShowCreateHint ? Visibility.Visible : Visibility.Collapsed;
                break;
            case nameof(PageLoginProfileViewModel.ShowMicrosoftHint):
                HintMicrosoft.Visibility = _vm.ShowMicrosoftHint ? Visibility.Visible : Visibility.Collapsed;
                break;
        }
    }

    public class ProfileItem
    {
        public ProfileItem(McProfile profile)
        {
            Profile = profile;
            Info = ProfileUi.GetProfileInfo(profile);
            var logoPath = Common.pathTemp + $@"Cache\Skin\Head\{profile.SkinHeadId}.png";
            if (File.Exists(logoPath) && new FileInfo(logoPath).Length != 0L)
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

        public string Info { get; private set; }
        public string Logo { get; private set; } = string.Empty;
        public string SvgIcon { get; private set; } = string.Empty;
        public McProfile Profile { get; }
        public string Username => Profile.UserName;
    }

    private void SelectProfile(object sender, MouseButtonEventArgs e)
    {
        if (sender is not MyListItem { DataContext: ProfileItem item }) return;
        _vm.Select(item);
        Launcher.frmLaunchLeft.RefreshPage(true);
        Launcher.frmLaunchLeft.BtnLaunch.IsEnabled = true;
    }

    private void ProfileContMenuBuild(MyListItem sender, EventArgs e)
    {
        // 更改 UUID
        var btnEditUuid = new MyIconButton { SvgIcon = "lucide/pencil", Tag = sender.Tag };
        btnEditUuid.SetLocalizedValue(FrameworkElement.ToolTipProperty, "Launch.Account.Profile.ChangeUuid");
        ToolTipService.SetPlacement(btnEditUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnEditUuid, 2d);
        btnEditUuid.Click += EditProfileUuid;
        // 复制 UUID
        var btnCopyUuid = new MyIconButton { SvgIcon = "lucide/copy", Tag = sender.Tag };
        btnCopyUuid.SetLocalizedValue(FrameworkElement.ToolTipProperty, "Launch.Account.Profile.CopyUuid");
        ToolTipService.SetPlacement(btnCopyUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCopyUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnCopyUuid, 2d);
        btnCopyUuid.Click += CopyProfileUuid;
        // 更改验证服务器名称
        var btnEditServerName = new MyIconButton { SvgIcon = "lucide/info", Tag = sender.Tag };
        btnEditServerName.SetLocalizedValue(FrameworkElement.ToolTipProperty, "Launch.Account.Profile.ChangeAuthServerName");
        ToolTipService.SetPlacement(btnEditServerName, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditServerName, 30d);
        ToolTipService.SetHorizontalOffset(btnEditServerName, 2d);
        btnEditServerName.Click += EditProfileServer;
        // 删除档案
        var btnDelete = new MyIconButton { SvgIcon = "lucide/trash-2", Tag = sender.Tag };
        btnDelete.SetLocalizedValue(FrameworkElement.ToolTipProperty, "Launch.Account.Profile.Delete");
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += DeleteProfile;
        if (((McProfile)sender.Tag).ProfileType == ProfileType.Offline)
            sender.Buttons = new[] { btnEditUuid, btnDelete };
        else
            sender.Buttons = new[] { btnCopyUuid, btnDelete };
    }

    private void BtnNew_Click(object sender, EventArgs e) => _vm.CreateProfileCommand.Execute(null);

    private void EditProfileUuid(object sender, EventArgs e)
    {
        var item = FindProfileItem(sender);
        if (item is not null) _vm.EditUuidCommand.Execute(item);
    }

    private void CopyProfileUuid(object sender, EventArgs e)
    {
        var item = FindProfileItem(sender);
        if (item is not null) _vm.CopyUuidCommand.Execute(item);
    }

    private void EditProfileServer(object sender, EventArgs e)
    {
        var item = FindProfileItem(sender);
        if (item is not null) _vm.EditServerNameCommand.Execute(item);
    }

    private void DeleteProfile(object sender, EventArgs e)
    {
        var item = FindProfileItem(sender);
        if (item is not null) _vm.DeleteProfileCommand.Execute(item);
    }

    private ProfileItem? FindProfileItem(object sender)
    {
        return ((MyIconButton)sender).Tag is McProfile profile
            ? _vm.Profiles.FirstOrDefault(p => p.Profile == profile)
            : null;
    }
}
