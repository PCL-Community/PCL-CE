using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public partial class PageLoginProfile
{
    public PageLoginProfile()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
    }

    public ObservableCollection<ProfileItem> ProfileCollection { get; set; } = new();

    /// <summary>
    ///     刷新页面显示的所有信息。
    /// </summary>
    public void Reload()
    {
        RefreshProfileList();
        ModMain.frmLoginProfileSkin = null;
        // RunInNewThread(Sub()
        // Thread.Sleep(800)
        // RunInUi(Sub() FrmLaunchLeft.RefreshPage(True))
        // End Sub)
    }

    /// <summary>
    ///     刷新档案列表
    /// </summary>
    public void RefreshProfileList()
    {
        ModBase.Log("[Profile] 刷新档案列表");
        ProfileCollection.Clear();
        ModProfile.GetProfile();
        try
        {
            foreach (var Profile in ModProfile.profileList)
                ProfileCollection.Add(new ProfileItem(Profile));
            ModBase.Log("[Profile] 档案列表刷新完成");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.Profile.Error.Read"), ModBase.LogLevel.Feedback);
        }

        if (!ModProfile.profileList.Any())
        {
            States.Hint.LaunchWithProfile = true;
            HintCreate.Visibility = Visibility.Visible;
        }
        else
        {
            HintCreate.Visibility = Visibility.Collapsed;
        }
    }

    public class ProfileItem
    {
        public ProfileItem(ModProfile.McProfile profile)
        {
            Profile = profile;
            Info = (string)ModProfile.GetProfileInfo(profile);
            var logoPath = ModBase.pathTemp + $@"Cache\Skin\Head\{profile.skinHeadId}.png";
            if (!(File.Exists(logoPath) && !(new FileInfo(logoPath).Length == 0L)))
                logoPath = Icon.IconButtonUser;
            Logo = logoPath;
        }

        public string Info { get; private set; }
        public string Logo { get; private set; }
        public ModProfile.McProfile Profile { get; }
        public string Username => Profile.username;
    }

    #region 控件

    private void SelectProfile(object sender, MouseButtonEventArgs e)
    {
        var item = (MyListItem)sender;
        var tag = (ModProfile.McProfile)item.Tag;
        ModProfile.selectedProfile = (ModProfile.McProfile)((MyListItem)sender).Tag;
        ModBase.Log($"[Profile] 选定档案: {tag.username}, 以 {tag.type} 方式验证");
        ModProfile.lastUsedProfile =
            ModProfile.profileList.IndexOf((ModProfile.McProfile)((MyListItem)sender).Tag); // 获取当前档案的序号
        ModProfile.SaveProfile(); // 保存档案配置，确保切换后的档案被正确保存

        // 清除登录验证缓存，确保使用新档案的验证信息
        ModLaunch.mcLoginMsLoader.State = ModBase.LoadState.Waiting;
        ModLaunch.mcLoginAuthLoader.State = ModBase.LoadState.Waiting;
        ModLaunch.mcLoginLegacyLoader.State = ModBase.LoadState.Waiting;

        ModBase.RunInUi(() =>
        {
            ModMain.frmLaunchLeft.RefreshPage(true);
            ModMain.frmLaunchLeft.BtnLaunch.IsEnabled = true;
        });
    }

    private void ProfileContMenuBuild(MyListItem sender, EventArgs e)
    {
        // 更改 UUID
        var btnEditUuid = new MyIconButton
            { Logo = Icon.IconButtonEdit, ToolTip = Lang.Text("Launch.Account.Profile.ChangeUuid"), Tag = sender.Tag };
        ToolTipService.SetPlacement(btnEditUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnEditUuid, 2d);
        btnEditUuid.Click += EditProfileUuid;
        // 复制 UUID
        var btnCopyUuid = new MyIconButton
            { Logo = Icon.IconButtonCopy, ToolTip = Lang.Text("Launch.Account.Profile.CopyUuid"), Tag = sender.Tag };
        ToolTipService.SetPlacement(btnCopyUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCopyUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnCopyUuid, 2d);
        btnCopyUuid.Click += CopyProfileUuid;
        // 更改验证服务器名称
        var btnEditServerName = new MyIconButton
            { Logo = Icon.IconButtonInfo, ToolTip = Lang.Text("Launch.Account.Profile.ChangeAuthServerName"), Tag = sender.Tag };
        ToolTipService.SetPlacement(btnEditServerName, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditServerName, 30d);
        ToolTipService.SetHorizontalOffset(btnEditServerName, 2d);
        btnEditServerName.Click += EditProfileServer;
        // 删除档案
        var btnDelete = new MyIconButton { Logo = Icon.IconButtonDelete, ToolTip = Lang.Text("Launch.Account.Profile.Delete"), Tag = sender.Tag };
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += DeleteProfile;
        // 根据档案类型显示不同的菜单项
        if (((ModProfile.McProfile)sender.Tag).type == ModLaunch.McLoginType.Legacy)
            sender.Buttons = new[] { btnEditUuid, btnDelete };
        else
            sender.Buttons = new[] { btnCopyUuid, btnDelete };
    }

    // 创建档案
    private void BtnNew_Click(object sender, EventArgs e)
    {
        ModBase.RunInNewThread(() =>
        {
            ModProfile.CreateProfile();
            ModBase.RunInUi(() => RefreshProfileList());
        });
    }

    // 编辑 UUID
    private void EditProfileUuid(object sender, EventArgs e)
    {
        ModProfile.EditOfflineUuid((ModProfile.McProfile)((MyIconButton)sender).Tag);
    }

    private void CopyProfileUuid(object sender, EventArgs e)
    {
        if (sender is MyIconButton { Tag: ModProfile.McProfile profile }) ModBase.ClipboardSet(profile.uuid);
    }

    // 编辑验证服务器名称
    private void EditProfileServer(object sender, EventArgs e)
    {
        var profile = (ModProfile.McProfile)((MyIconButton)sender).Tag;
        string name = ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.EditServerName.Title"), Lang.Text("Launch.Account.Profile.EditServerName.Message"), profile.serverName);
        if (name is not null) ModProfile.EditAuthServerName(profile, name);
    }

    // 删除档案
    private void DeleteProfile(object sender, EventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"), Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true,
                forceWait: true) == 2)
            return;
        ModProfile.RemoveProfile((ModProfile.McProfile)((MyIconButton)sender).Tag);
        ModBase.RunInUi(() => RefreshProfileList());
    }

    // 导入 / 导出档案
    private void BtnPort_Click(object sender, EventArgs e)
    {
        ModProfile.MigrateProfile();
        ModBase.RunInUi(() => RefreshProfileList());
    }

    #endregion
}
