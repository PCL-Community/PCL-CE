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
        BtnBack.Click += BtnBack_Click;
        RadioUuidCustom.Check += RadioUuid_Checked;
        RadioUuidStandard.Check += RadioUuid_Checked;
        RadioUuidLegacy.Check += RadioUuid_Checked;
        BtnLogin.Click += BtnLogin_Click;
    }

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true));
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
        // 玩家 ID 输入检查
        var Username = TextName.Text;
        var UsernameValidateResult = new RegexValidator("^[A-z0-9_]{3,16}$").Validate(Username);
        if (!UsernameValidateResult.IsValid)
                if (ModMain.MyMsgBox(
                        Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                        Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), IsWarn: true, ForceWait: true) == 2)
                return;
        // UUID
        string UserUuid = null;
        if (RadioUuidCustom.Checked)
        {
            // 自定义输入检查
            var UuidInput = TextUuid.Text.Replace("-", "");
            var UuidValidateResult = new RegexValidator("^[a-fA-F0-9]{32}$").Validate(UuidInput);
            if (RadioUuidCustom.Checked && !UuidValidateResult.IsValid)
            {
                ModMain.Hint(Lang.Text("Launch.Account.Offline.InvalidUuid", UuidValidateResult), ModMain.HintType.Critical);
                return;
            }

            UserUuid = UuidInput;
        }
        else if (RadioUuidLegacy.Checked)
        {
            UserUuid = ModProfile.GetOfflineUuid(Username, isLegacy: true);
        }
        else
        {
            UserUuid = ModProfile.GetOfflineUuid(Username);
        }

        // 创建档案
        var NewProfile = new ModProfile.McProfile
        {
            Type = ModLaunch.McLoginType.Legacy,
            Uuid = UserUuid,
            Username = Username,
            Desc = ""
        };
        ModProfile.ProfileList.Add(NewProfile);
        ModProfile.SaveProfile();
        ModProfile.SelectedProfile = NewProfile;
        ModProfile.IsCreatingProfile = false;
        ModMain.Hint(Lang.Text("Launch.Account.Profile.Created"), ModMain.HintType.Finish);
        ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true));
    }
}
