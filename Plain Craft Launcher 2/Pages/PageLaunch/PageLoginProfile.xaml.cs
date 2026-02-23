using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

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
        ModMain.FrmLoginProfileSkin = null;
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
            foreach (var Profile in ModProfile.ProfileList)
                ProfileCollection.Add(new ProfileItem(Profile));
            ModBase.Log("[Profile] 档案列表刷新完成");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取档案列表失败", ModBase.LogLevel.Feedback);
        }

        if (!ModProfile.ProfileList.Any())
        {
            ModBase.Setup.Set("HintProfileSelect", true);
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
            Info = Conversions.ToString(ModProfile.GetProfileInfo(profile));
            var LogoPath = ModBase.PathTemp + $@"Cache\Skin\Head\{profile.SkinHeadId}.png";
            if (!(File.Exists(LogoPath) && !(new FileInfo(LogoPath).Length == 0L)))
                LogoPath = ModBase.Logo.IconButtonUser;
            Logo = LogoPath;
        }

        public string Info { get; private set; }
        public string Logo { get; private set; }
        public ModProfile.McProfile Profile { get; }
        public string Username => Profile.Username;
    }

    #region 控件

    private void SelectProfile(object sender, MouseButtonEventArgs e)
    {
        ModProfile.SelectedProfile = (ModProfile.McProfile)((MyListItem)sender).Tag;
        ModBase.Log($"[Profile] 选定档案: {((dynamic)sender).Tag.Username}, 以 {((dynamic)sender).Tag.Type} 方式验证");
        ModProfile.LastUsedProfile =
            ModProfile.ProfileList.IndexOf((ModProfile.McProfile)((dynamic)sender).Tag); // 获取当前档案的序号
        ModProfile.SaveProfile(); // 保存档案配置，确保切换后的档案被正确保存

        // 清除登录验证缓存，确保使用新档案的验证信息
        ModLaunch.McLoginMsLoader.State = ModBase.LoadState.Waiting;
        ModLaunch.McLoginAuthLoader.State = ModBase.LoadState.Waiting;
        ModLaunch.McLoginLegacyLoader.State = ModBase.LoadState.Waiting;

        ModBase.RunInUi(() =>
        {
            ModMain.FrmLaunchLeft.RefreshPage(true);
            ModMain.FrmLaunchLeft.BtnLaunch.IsEnabled = true;
        });
    }

    private void ProfileContMenuBuild(MyListItem sender, EventArgs e)
    {
        // 更改 UUID
        var btnEditUuid = new MyIconButton
            { Logo = ModBase.Logo.IconButtonEdit, ToolTip = "更改 UUID", Tag = sender.Tag };
        ToolTipService.SetPlacement(btnEditUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnEditUuid, 2d);
        btnEditUuid.Click += EditProfileUuid;
        // 复制 UUID
        var btnCopyUuid = new MyIconButton
            { Logo = ModBase.Logo.IconButtonCopy, ToolTip = "复制 UUID", Tag = sender.Tag };
        ToolTipService.SetPlacement(btnCopyUuid, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnCopyUuid, 30d);
        ToolTipService.SetHorizontalOffset(btnCopyUuid, 2d);
        btnCopyUuid.Click += CopyProfileUuid;
        // 更改验证服务器名称
        var btnEditServerName = new MyIconButton
            { Logo = ModBase.Logo.IconButtonInfo, ToolTip = "更改验证服务器名称", Tag = sender.Tag };
        ToolTipService.SetPlacement(btnEditServerName, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnEditServerName, 30d);
        ToolTipService.SetHorizontalOffset(btnEditServerName, 2d);
        btnEditServerName.Click += EditProfileServer;
        // 删除档案
        var btnDelete = new MyIconButton { Logo = ModBase.Logo.IconButtonDelete, ToolTip = "删除档案", Tag = sender.Tag };
        ToolTipService.SetPlacement(btnDelete, PlacementMode.Center);
        ToolTipService.SetVerticalOffset(btnDelete, 30d);
        ToolTipService.SetHorizontalOffset(btnDelete, 2d);
        btnDelete.Click += DeleteProfile;
        // 根据档案类型显示不同的菜单项
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)sender.Tag).Type,
                ModLaunch.McLoginType.Legacy, false)))
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
        ModProfile.EditOfflineUuid((ModProfile.McProfile)((dynamic)sender).Tag);
    }

    private void CopyProfileUuid(object sender, EventArgs e)
    {
        ModBase.ClipboardSet(Conversions.ToString(((dynamic)sender).Tag.UUID));
    }

    // 编辑验证服务器名称
    private void EditProfileServer(object sender, EventArgs e)
    {
        string name = ModMain.MyMsgBoxInput("修改验证服务器名称", "请输入新的验证服务器名称",
            Conversions.ToString(((dynamic)sender).Tag.ServerName));
        if (name is not null) ModProfile.EditAuthServerName((ModProfile.McProfile)((dynamic)sender).Tag, name);
    }

    // 删除档案
    private void DeleteProfile(object sender, EventArgs e)
    {
        if (ModMain.MyMsgBox($"你正在选择删除此档案，该操作无法撤销。{Constants.vbCrLf}确定继续？", "删除档案确认", "继续", "取消", IsWarn: true,
                ForceWait: true) == 2)
            return;
        ModProfile.RemoveProfile((ModProfile.McProfile)((dynamic)sender).Tag);
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