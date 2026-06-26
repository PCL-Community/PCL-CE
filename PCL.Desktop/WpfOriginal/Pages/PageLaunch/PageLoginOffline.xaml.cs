using System.Linq;
using System.Windows;
using PCL.Core.Utils.Validate;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageLoginOffline
{
    public PageLoginOffline()
    {
        // Handles
        InitializeComponent();
        // Populate skin source combo
        ComboSkinSource.Items.Clear();
        ComboSkinSource.Items.Add(new MyComboBoxItem { Content = Lang.Text("Launch.Account.SkinSource.None") });
        foreach (var p in ModProfile.profileList.Where(x => x.Type == ModLaunch.McLoginType.Ms))
            ComboSkinSource.Items.Add(new MyComboBoxItem { Content = p.Username, Tag = p.Uuid });
        BtnBack.Click += BtnBack_Click;
        RadioUuidCustom.Check += RadioUuid_Checked;
        RadioUuidStandard.Check += RadioUuid_Checked;
        RadioUuidLegacy.Check += RadioUuid_Checked;
        BtnLogin.Click += BtnLogin_Click;
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true));
    }

    private void RadioUuid_Checked(object sender, ModBase.RouteEventArgs e)
    {
        if (RadioUuidCustom.Checked)
        {
            TextUuidTitle.Visibility = Visibility.Visible;
            TextUuid.Visibility = Visibility.Visible;
        }
        else
        {
            TextUuidTitle.Visibility = Visibility.Collapsed;
            TextUuid.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        // 检查是否有正版账号
        if (!ModProfile.profileList.Any(x => x.Type == ModLaunch.McLoginType.Ms))
        {
            var msWarnResult = ModMain.MyMsgBox(
                Lang.Text("Launch.Account.Unverified.Warning.Message"),
                Lang.Text("Launch.Account.Unverified.Warning.Title"),
                Lang.Text("Common.Action.Cancel"), Lang.Text("Launch.Account.Unverified.Warning.OpenStore"),
                Lang.Text("Launch.Account.Unverified.Warning.Continue"),
                isWarn: true, forceWait: true);
            if (msWarnResult == 1 || msWarnResult == 2)
            {
                if (msWarnResult == 2)
                    ModBase.OpenWebsite("https://www.minecraft.net");
                return;
            }
        }

        // 玩家 ID 输入检查
        var username = TextName.Text;
        var usernameValidateResult = new RegexValidator("^[A-z0-9_]{3,16}$").Validate(username);
        if (!usernameValidateResult.IsValid)
                if (ModMain.MyMsgBox(
                        Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                        Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true, forceWait: true) == 2)
                return;
        // UUID
        string userUuid = null;
        if (RadioUuidCustom.Checked)
        {
            // 自定义输入检查
            var uuidInput = TextUuid.Text.Replace("-", "");
            var uuidValidateResult = new RegexValidator("^[a-fA-F0-9]{32}$").Validate(uuidInput);
            if (RadioUuidCustom.Checked && !uuidValidateResult.IsValid)
            {
                ModMain.Hint(Lang.Text("Launch.Account.Offline.InvalidUuid", uuidValidateResult), ModMain.HintType.Critical);
                return;
            }

            userUuid = uuidInput;
        }
        else if (RadioUuidLegacy.Checked)
        {
            userUuid = ModProfile.GetOfflineUuid(username, isLegacy: true);
        }
        else
        {
            userUuid = ModProfile.GetOfflineUuid(username);
        }

        // 获取借用皮肤来源
        var skinSource = (ComboSkinSource.SelectedItem as MyComboBoxItem)?.Tag?.ToString() ?? "";
        var skinHeadId = "";
        if (!string.IsNullOrEmpty(skinSource))
        {
            if (ModMain.MyMsgBox(
                    Lang.Text("Launch.OfflineSkin.Borrow.Message"),
                    Lang.Text("Launch.OfflineSkin.LoaderRequired.Title"),
                    Lang.Text("Launch.OfflineSkin.LoaderRequired.Confirm"), Lang.Text("Common.Action.Cancel"),
                    isWarn: true, forceWait: true) == 2)
                return;

            var sourceProfile = ModProfile.profileList.FirstOrDefault(p => p.Uuid == skinSource);
            if (sourceProfile is not null)
                skinHeadId = sourceProfile.SkinHeadId;
        }

        // 创建档案
        var newProfile = new ModProfile.McProfile
        {
            Type = ModLaunch.McLoginType.Legacy,
            Uuid = userUuid,
            Username = username,
            Desc = "",
            SkinHeadId = skinHeadId,
            skinSourceUuid = skinSource
        };
        ModProfile.profileList.Add(newProfile);
        ModProfile.SaveProfile();
        ModProfile.selectedProfile = newProfile;
        ModProfile.isCreatingProfile = false;
        ModMain.Hint(Lang.Text("Launch.Account.Profile.Created"), ModMain.HintType.Finish);
        ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true));
    }
}
