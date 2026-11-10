using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Natayark;
using PCL.Core.Minecraft.Profile;

namespace PCL;

public static partial class GameLink
{
    public static bool LobbyPrecheck()
    {
        if (!LobbyInfoProvider.IsLobbyAvailable)
        {
            HintService.Show(Hints.Error(Lang.Text("Link.Mod.LobbyUnavailable")));
            return false;
        }

        if (ProfileService.Current is not null)
            if (ProfileService.Current.UserName.Contains('|'))
            {
                HintService.Show(Hints.Info(Lang.Text("Link.Mod.InvalidPlayerId")));
                return false;
            }

        if (LobbyInfoProvider.RequiresLogin)
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.LoginFirst")));
                return false;
            }

            try
            {
                NatayarkProfileManager
                    .GetNaidDataAsync(States.Link.NaidRefreshToken, true)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception)
            {
                Common.Log("[Link] 刷新 Natayark ID 信息失败，需要重新登录");
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.ReLoginRequired")));
                return false;
            }

            var waitCount = 0;
            while (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
            {
                if (waitCount > 30)
                    break;
                Thread.Sleep(500);
                waitCount += 1;
            }

            if (string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.NaidFetchFailed")));
                return false;
            }

            if (LobbyInfoProvider.RequiresRealName
                && !NatayarkProfileManager.NaidProfile.IsRealNamed)
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.RealNameRequired")));
                return false;
            }

            if (NatayarkProfileManager.NaidProfile.Status != 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Link.Mod.AccountBanned")));
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(Config.Link.Username)
            && string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
        {
            HintService.Show(Hints.Error(Lang.Text("Link.Mod.UsernameOrLogin")));
            return false;
        }

        if (ETController.Precheck() == 1)
        {
            HintService.Show(Hints.Info(Lang.Text("Link.Mod.DownloadingDeps")));
            DownloadEasyTier();
            return false;
        }

        if (dlEasyTierLoader is not null)
            switch (dlEasyTierLoader.State)
            {
                case LoadState.Loading:
                    HintService.Show(Hints.Info(Lang.Text("Link.Mod.EasyTierNotReady")));
                    return false;
                case LoadState.Failed or LoadState.Aborted:
                    HintService.Show(Hints.Info(Lang.Text("Link.Mod.DownloadingEasyTier")));
                    DownloadEasyTier();
                    return false;
            }

        return true;
    }
}
