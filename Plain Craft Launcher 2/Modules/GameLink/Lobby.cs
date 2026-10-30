using PCL.Core.App;
using PCL.Core.App.Localization;
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
            HintService.Hint(Lang.Text("Link.Mod.LobbyUnavailable"), HintType.Error);
            return false;
        }

        if (ProfileService.Current is not null)
            if (ProfileService.Current.UserName.Contains('|'))
            {
                HintService.Hint(Lang.Text("Link.Mod.InvalidPlayerId"));
                return false;
            }

        if (LobbyInfoProvider.RequiresLogin)
        {
            if (string.IsNullOrWhiteSpace(States.Link.NaidRefreshToken))
            {
                HintService.Hint(Lang.Text("Link.Mod.LoginFirst"), HintType.Error);
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
                HintService.Hint(Lang.Text("Link.Mod.ReLoginRequired"), HintType.Error);
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
                HintService.Hint(Lang.Text("Link.Mod.NaidFetchFailed"), HintType.Error);
                return false;
            }

            if (LobbyInfoProvider.RequiresRealName
                && !NatayarkProfileManager.NaidProfile.IsRealNamed)
            {
                HintService.Hint(Lang.Text("Link.Mod.RealNameRequired"), HintType.Error);
                return false;
            }

            if (NatayarkProfileManager.NaidProfile.Status != 0)
            {
                HintService.Hint(Lang.Text("Link.Mod.AccountBanned"), HintType.Error);
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(Config.Link.Username)
            && string.IsNullOrWhiteSpace(NatayarkProfileManager.NaidProfile.Username))
        {
            HintService.Hint(Lang.Text("Link.Mod.UsernameOrLogin"), HintType.Error);
            return false;
        }

        if (ETController.Precheck() == 1)
        {
            HintService.Hint(Lang.Text("Link.Mod.DownloadingDeps"));
            DownloadEasyTier();
            return false;
        }

        if (dlEasyTierLoader is not null)
            switch (dlEasyTierLoader.State)
            {
                case Common.LoadState.Loading:
                    HintService.Hint(Lang.Text("Link.Mod.EasyTierNotReady"));
                    return false;
                case Common.LoadState.Failed or Common.LoadState.Aborted:
                    HintService.Hint(Lang.Text("Link.Mod.DownloadingEasyTier"));
                    DownloadEasyTier();
                    return false;
            }

        return true;
    }
}
