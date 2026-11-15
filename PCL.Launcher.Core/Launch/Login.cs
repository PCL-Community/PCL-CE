using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL;

public static partial class GameLaunch
{
    public enum McLoginType
    {
        Legacy = 1,
        Auth = 2,
        Ms = 3
    }

    public static Loading.LoaderTask<McLoginData, McLoginResult> mcLoginLoader =
        new(
            Lang.Text("Minecraft.Launch.Stage.Login"),
            McLoginStart,
            inputDelegate: McLoginInput,
            priority: ThreadPriority.BelowNormal)
        {
            reloadTimeout = 1,
            ProgressWeight = 15d,
            block = false,
            Identifier = LaunchStageLoginId,
            DisplayNameFactory = () => Lang.Text("Minecraft.Launch.Stage.Login")
        };

    public static Loading.LoaderTask<McLoginMs, McLoginResult> mcLoginMsLoader =
        new("Loader Login Ms", McLoginMsStart)
        {
            reloadTimeout = 1
        };

    public static Loading.LoaderTask<McLoginLegacy, McLoginResult> mcLoginLegacyLoader =
        new("Loader Login Legacy", McLoginLegacyStart);

    public static Loading.LoaderTask<McLoginServer, McLoginResult> mcLoginAuthLoader =
        new("Loader Login Auth", McLoginServerStartNew)
        {
            reloadTimeout = 1000 * 60 * 10
        };

    public static McLoginData? McLoginInput()
    {
        McLoginData? loginData = null;
        try
        {
            loginData = (McLoginData?)LoginProfile.GetLoginData();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Launch.Login.Error.Input"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Launch.Login.Error.Input"));
        }

        return loginData;
    }

    private static void McLoginStart(Loading.LoaderTask<McLoginData, McLoginResult> data)
    {
        LauncherCommon.Log("[Profile] 开始加载选定档案");
        var checkResult = LoginProfile.IsProfileValid();
        if (!string.IsNullOrEmpty(checkResult))
            throw new ArgumentException(checkResult);

        switch (data.input!.LoginType)
        {
            case McLoginType.Ms:
                mcLoginMsLoader.WaitForExit(data.input, mcLoginLoader, data.isForceRestarting);
                data.output = mcLoginMsLoader.output;
                break;

            case McLoginType.Legacy:
                mcLoginLegacyLoader.WaitForExit(data.input, mcLoginLoader, data.isForceRestarting);
                data.output = mcLoginLegacyLoader.output;
                break;

            case McLoginType.Auth:
                mcLoginAuthLoader.WaitForExit(data.input, mcLoginLoader, data.isForceRestarting);
                data.output = mcLoginAuthLoader.output;
                break;

            default:
                throw new Exception("未知的登录方式");
        }

        LaunchUi.RefreshLaunchPage(false);
        LauncherCommon.Log("[Profile] 选定档案加载完成");
    }

    private static void McLoginMsStart(Loading.LoaderTask<McLoginMs, McLoginResult> data)
    {
        var existing =
            ProfileService.Current?.ProfileType == ProfileType.Microsoft
                ? ProfileService.Current
                : null;
        LogWrapper.Info(
            "Profile",
            $"验证方式：正版（{(existing is null ? "尚未登录" : existing.UserName)}）");
        data.Progress = 0.05d;
        var stored = _AuthenticateMicrosoft(data, existing);
        ThrowIfAborted(data);
        ProfileService.IsCreatingProfile = false;

        data.output = _CreateMicrosoftLoginResult(stored);

        data.Progress = 0.98d;
        LogWrapper.Info("Profile", "正版验证完成");
    }

    private static McProfile _AuthenticateMicrosoft(
        Loading.LoaderTask<McLoginMs, McLoginResult> data,
        McProfile? existing)
    {
        try
        {
            return ProfileService
                .AuthenticateAsync(
                    ProfileType.Microsoft,
                    new AuthenticationRequest
                    {
                        ForceRefresh = data.isForceRestarting,
                        DeviceCodeHandler = LoginProfile.ShowDeviceCodeLoginAsync,
                        RefreshFailureHandler = _ConfirmMicrosoftRefreshFailureAsync
                    },
                    existing,
                    true,
                    data.AbortedToken)
                .GetAwaiter()
                .GetResult();
        }
        catch (IdentityModelAuthenticationException ex)
            when (LoginProfile.HandleMicrosoftXstsError(ex)
                  || LoginProfile.HandleMicrosoftNotOwnedError(ex)
                  || LoginProfile.HandleMicrosoftCreateProfileError(ex))
        {
            throw new IdentityModelException("$$", ex);
        }
    }

    private static McLoginResult _CreateMicrosoftLoginResult(McProfile stored)
    {
        return new McLoginResult
        {
            AccessToken = stored.AccessToken,
            Name = stored.UserName,
            Uuid = stored.Uuid,
            Type = "Microsoft",
            ClientToken = stored.ClientToken,
            ProfileJson = stored.RawJson
        };
    }

    /// <summary>
    ///     检查是否被中断
    /// </summary>
    private static void ThrowIfAborted(Loading.LoaderTask<McLoginMs, McLoginResult> data)
    {
        if (data.IsAborted)
            throw new ThreadInterruptedException();
    }

    private static Task<bool> _ConfirmMicrosoftRefreshFailureAsync(
        Exception exception,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        LogWrapper.Info("Profile", $"获取正版 OAuth Token 失败：{exception}");
        var reuseCachedProfile = false;
        LauncherCommon.RunInUiWait(() =>
        {
            if (!isLaunching)
                return;
            reuseCachedProfile =
                LaunchUi.ShowMessageBox(
                    Lang.Text("Minecraft.Launch.Login.RefreshAccountFailed.Message"),
                    Lang.Text("Minecraft.Launch.Login.RefreshAccountFailed.Title"),
                    Lang.Text("Minecraft.Launch.Login.Continue"),
                    Lang.Text("Common.Action.Cancel")
                )
                == 1;
        });
        return Task.FromResult(reuseCachedProfile);
    }

    private static void McLoginServerStartNew(Loading.LoaderTask<McLoginServer, McLoginResult> data)
    {
        var input = data.input!;
        LogWrapper.Info("Profile", $"验证方式：{input.Description}");
        data.Progress = 0.1d;
        var existing =
            input.IsExist && !ProfileService.IsCreatingProfile ? ProfileService.Current : null;
        var stored = ProfileService
            .AuthenticateAsync(
                input.ProviderType,
                _CreateServerAuthenticationRequest(input, data),
                existing,
                true,
                data.AbortedToken)
            .GetAwaiter()
            .GetResult();
        if (data.IsAborted)
            throw new ThreadInterruptedException();

        data.output = _CreateAuthLoginResult(stored);
        ProfileService.IsCreatingProfile = false;
        data.Progress = 0.98d;
        LogWrapper.Info("Profile", "第三方验证完成");
    }

    private static AuthenticationRequest _CreateServerAuthenticationRequest(
        McLoginServer input,
        Loading.LoaderTask<McLoginServer, McLoginResult> data)
    {
        return new AuthenticationRequest
        {
            Server = input.BaseUrl,
            DiscoveryAddress = input.DiscoveryAddress,
            Username = input.UserName,
            Password = input.Password,
            ForceRefresh = data.isForceRestarting,
            ForceReselectProfile = input.ForceReselectProfile,
            DeviceCodeHandler =
                input.ProviderType == ProfileType.YggdrasilConnect
                    ? LoginProfile.ShowDeviceCodeLoginAsync
                    : null,
            ProfileSelector = (candidates, _) => Task.FromResult(_SelectAuthProfile(candidates))
        };
    }

    private static McLoginResult _CreateAuthLoginResult(McProfile stored)
    {
        return new McLoginResult
        {
            AccessToken = stored.AccessToken,
            ClientToken = stored.ClientToken,
            Uuid = stored.Uuid,
            Name = stored.UserName,
            Type = "Auth"
        };
    }

    private static AuthenticationCandidate? _SelectAuthProfile(
        IReadOnlyList<AuthenticationCandidate> candidates)
    {
        switch (candidates.Count)
        {
            case 0:
                return null;
            case 1:
                return candidates[0];
        }

        var index = LaunchUi.ShowSelect(
            Lang.Text("Minecraft.Launch.Login.Auth.SelectProfile"),
            [.. candidates.Select(candidate => candidate.Name)]);
        return index is >= 0 and < 100000 ? candidates[index.Value] : null;
    }

    private static void McLoginLegacyStart(Loading.LoaderTask<McLoginLegacy, McLoginResult> data)
    {
        var input = data.input!;
        LogWrapper.Info("Profile", $"验证方式：离线（{input.UserName}, {input.Uuid}）");
        data.Progress = 0.1d;
        data.output.Name = input.UserName ?? "";
        data.output.Uuid = input.Uuid ?? "";
        data.output.Type = "Legacy";
        data.output.AccessToken = data.output.Uuid;
        data.output.ClientToken = data.output.Uuid;
    }

    public abstract class McLoginData
    {
        /// <summary>
        ///     登录方式。
        /// </summary>
        public McLoginType LoginType;

        public override bool Equals(object? obj)
        {
            return obj is not null && obj.GetHashCode() == GetHashCode();
        }
    }

    public class McLoginServer : McLoginData
    {
        /// <summary>
        ///     登录服务器基础地址。
        /// </summary>
        public string? BaseUrl;

        /// <summary>
        ///     登录方式的描述字符串，如 “正版”、“统一通行证”。
        /// </summary>
        public string? Description;

        public string? DiscoveryAddress;

        /// <summary>
        ///     是否在本次登录中强制要求玩家重新选择角色，目前仅对 Authlib-Injector 生效。
        /// </summary>
        public bool ForceReselectProfile = false;

        /// <summary>
        ///     是否已经存在该验证信息，用于判断是否为新增档案。
        /// </summary>
        public bool IsExist = false;

        /// <summary>
        ///     登录密码。
        /// </summary>
        public string? Password;

        public ProfileType ProviderType = ProfileType.Authlib;

        /// <summary>
        ///     登录用户名。
        /// </summary>
        public string? UserName;

        public McLoginServer(McLoginType type)
        {
            LoginType = type;
        }

        public override int GetHashCode()
        {
            return (int)
                Math.Round(
                    LauncherCommon.GetHash(UserName + Password + BaseUrl + (int)LoginType)
                    % (decimal)int.MaxValue);
        }
    }

    public class McLoginMs : McLoginData
    {
        public McLoginMs()
        {
            LoginType = McLoginType.Ms;
        }

        public override int GetHashCode()
        {
            return (int)Math.Round(
                LauncherCommon.GetHash(LoginType.ToString()) % (decimal)int.MaxValue);
        }
    }

    public class McLoginLegacy : McLoginData
    {
        /// <summary>
        ///     若采用正版皮肤，则为该皮肤名。
        /// </summary>
        public string? SkinName;

        /// <summary>
        ///     皮肤种类。
        /// </summary>
        public int SkinType;

        /// <summary>
        ///     登录用户名。
        /// </summary>
        public string? UserName;

        /// <summary>
        ///     UUID。
        /// </summary>
        public string? Uuid;

        public McLoginLegacy()
        {
            LoginType = McLoginType.Legacy;
        }

        public override int GetHashCode()
        {
            return (int)
                Math.Round(
                    LauncherCommon.GetHash(UserName + SkinType + SkinName + (int)LoginType)
                    % (decimal)int.MaxValue);
        }
    }

    public struct McLoginResult
    {
        public string Name;
        public string Uuid;
        public string AccessToken;
        public string Type;
        public string ClientToken;

        /// <summary>
        ///     进行微软登录时返回的 profile 信息。
        /// </summary>
        public string ProfileJson;
    }
}