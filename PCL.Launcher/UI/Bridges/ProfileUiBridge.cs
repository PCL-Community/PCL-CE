using System.Text.Json;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils;

namespace PCL.UI.Bridges;

/// <summary>登录档案相关的界面桥接：档案校验、设备码登录与微软登录错误处理。</summary>
public sealed class ProfileUiBridge : IProfileUi
{
    public GameLaunch.McLoginData? GetLoginData()
    {
        var profile = ProfileService.Current;
        if (profile is null) return new GameLaunch.McLoginLegacy();
        return profile.ProfileType switch
        {
            ProfileType.Microsoft => new GameLaunch.McLoginMs(),

            ProfileType.Authlib => new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                UserName = profile.LoginName,
                Password = profile.Password,
                Description = profile.ServerName ?? "Authlib-Injector",
                IsExist = true
            },

            ProfileType.YggdrasilConnect => new GameLaunch.McLoginServer(
                GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                Description = profile.ServerName ?? "Yggdrasil Connect",
                IsExist = true,
                ProviderType = ProfileType.YggdrasilConnect,
                DiscoveryAddress = profile.DiscoveryAddress
            },

            _ => new GameLaunch.McLoginLegacy
            {
                UserName = profile.UserName,
                Uuid = profile.Uuid
            }
        };
    }

    public string IsProfileValid()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return Lang.Text("Minecraft.Launch.Precheck.NoProfile");

        var userName = profile.UserName;
        return profile.ProfileType switch
        {
            not ProfileType.Offline => string.Empty,

            ProfileType.Offline when
                string.IsNullOrWhiteSpace(userName) =>
                Lang.Text("Launch.Account.Profile.Validation.EmptyUsername"),

            ProfileType.Offline when
                userName?.Contains('"') == true =>
                Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername"),

            ProfileType.Offline when
                InstanceList.McMcInstanceSelected is { Info.Drop: >= 203 }
                && userName is not null
                && userName.Trim().Length > 16 =>
                Lang.Text("Launch.Account.Profile.Validation.UsernameTooLong"),

            _ => string.Empty
        };
    }

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<AuthorizeResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new LoginDialogRequest
        {
            Title = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin"),
            Buttons = default,
            Data = JsonSerializer
                .SerializeToNode(context.Data, JsonCompat.SerializerOptions)!
                .AsObject(),
            DeviceCodePoll = (_, pollToken) => context.PollAsync(pollToken),
            LoginResultHandler = (oauth, _) =>
            {
                completion.TrySetResult(oauth);
                return Task.CompletedTask;
            },
            CompletionHandler = result =>
            {
                if (result is Exception exception) completion.TrySetException(exception);
            }
        };
        DialogService.Instance.ShowNotification(request);
        return completion.Task;
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception)
    {
        if (exception.Error is not { } error
            || !error.StartsWith("xsts_", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalizedError = error.ToLowerInvariant();
        if (normalizedError is not ("xsts_2148916227"
            or "xsts_2148916233"
            or "xsts_2148916235"
            or "xsts_2148916238"))
            return false;

        switch (normalizedError)
        {
            case "xsts_2148916227":
                _ = DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Minecraft.Launch.Login.Failed"),
                        Content = Lang.Text("Minecraft.Launch.Login.Microsoft.Banned"),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Minecraft.Launch.Login.IKnow"))),
                        IsWarn = true
                    });
                break;
            case "xsts_2148916233":
                if (UiInteraction.MessageBlockingOnWorkerThread(
                        Lang.Text("Minecraft.Launch.Login.Microsoft.XboxNotRegistered"),
                        Lang.Text("Minecraft.Launch.Login.Hint"),
                        Lang.Text("Minecraft.Launch.Login.Register"),
                        Lang.Text("Common.Action.Cancel"))
                    == 1)
                    UiInteraction.OpenWebsite("https://signup.live.com/signup");
                break;
            case "xsts_2148916235":
                _ = DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Minecraft.Launch.Login.Failed"),
                        Content = Lang.Text("Minecraft.Launch.Login.Microsoft.RegionBlocked"),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Minecraft.Launch.Login.IKnow")))
                    });
                break;
            case "xsts_2148916238":
                var result = UiInteraction.MessageBlockingOnWorkerThread(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.Message"),
                    Lang.Text("Minecraft.Launch.Login.Hint"),
                    Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeOver13"),
                    Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeUnder13"),
                    Lang.Text("Common.Option.IDontKnow"));
                switch (result)
                {
                    case 1:
                        UiInteraction.OpenWebsite("https://account.live.com/editprof.aspx");
                        _ = DialogService.Instance.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Minecraft.Launch.Login.Hint"),
                                Content = Lang.Text(
                                    "Minecraft.Launch.Login.Microsoft.ChangeBirthDate.Message"),
                                Buttons = DialogButtons.Ok
                            });
                        break;
                    default:
                        UiInteraction.OpenWebsite(
                            "https://support.microsoft.com/zh-cn/account-billing/"
                            + "如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                        _ = DialogService.Instance.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Minecraft.Launch.Login.Hint"),
                                Content = Lang.Text(
                                    "Minecraft.Launch.Login.Microsoft.ChangeBirthDate.SupportMessage"),
                                Buttons = DialogButtons.Ok
                            });
                        break;
                }

                break;
        }

        return true;
    }

    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(
                exception.Error,
                "minecraft_not_owned",
                StringComparison.OrdinalIgnoreCase))
            return false;

        var result = UiInteraction.MessageBlockingOnWorkerThread(
            Lang.Text("Minecraft.Launch.Login.Microsoft.NotPurchased"),
            Lang.Text("Minecraft.Launch.Login.Failed"),
            Lang.Text("Minecraft.Launch.Login.Microsoft.PurchaseMinecraft"),
            Lang.Text("Common.Action.Cancel"));
        if (result == 1)
            UiInteraction.OpenWebsite(
                "https://www.xbox.com/zh-cn/games/store/"
                + "minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        return true;
    }

    public bool HandleMicrosoftCreateProfileError(
        IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(
                exception.Error,
                "profile_not_created",
                StringComparison.OrdinalIgnoreCase))
            return false;

        var result = UiInteraction.MessageBlockingOnWorkerThread(
            Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Message"),
            Lang.Text("Minecraft.Launch.Login.Failed"),
            Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Button"),
            Lang.Text("Common.Action.Cancel"));
        if (result == 1)
            UiInteraction.OpenWebsite(
                "https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
        return true;
    }
}