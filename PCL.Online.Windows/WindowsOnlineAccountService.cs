// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
using PCL.Core.Logging;

namespace PCL.Online.Windows;

public static class WindowsOnlineAccountService
{
    private static readonly string[] MsalXboxScopes = ["XboxLive.signin"];
    private static readonly string[] MsalGraphScopes = ["User.Read"];

    public static void Register()
    {
        OnlineAccountService.RegisterPlatformXboxAuthorizationProvider(GetXboxAuthorizationWithWamSilent);
    }

    public static async Task<OnlineLoginResult> LoginWithWindowsAccountAsync(
        IntPtr parentWindowHandle,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return new OnlineLoginResult { Success = false, Message = Text("Online.Login.WindowsUnsupported") };
        if (string.IsNullOrWhiteSpace(OnlineAccountService.MicrosoftClientId))
            return new OnlineLoginResult { Success = false, Message = Text("Online.Login.ClientIdMissing") };

        try
        {
            var app = BuildWamApplication(parentWindowHandle);
            var xboxResult = await app.AcquireTokenInteractive(MsalXboxScopes)
                .WithAccount(PublicClientApplication.OperatingSystemAccount)
                .WithParentActivityOrWindow(parentWindowHandle)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(true);

            AuthenticationResult graphResult;
            try
            {
                graphResult = await app.AcquireTokenSilent(MsalGraphScopes, xboxResult.Account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (MsalUiRequiredException)
            {
                graphResult = await app.AcquireTokenInteractive(MsalGraphScopes)
                    .WithAccount(xboxResult.Account)
                    .WithParentActivityOrWindow(parentWindowHandle)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(true);
            }

            return await Task.Run(
                    () => OnlineAccountService.CompleteLoginWithAccessTokens(
                        graphResult.AccessToken,
                        xboxResult.AccessToken),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "WAM 登录失败");
            return new OnlineLoginResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private static IPublicClientApplication BuildWamApplication(IntPtr parentWindowHandle)
    {
        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
        {
            Title = "PCL N Edition",
            ListOperatingSystemAccounts = true
        };

        var builder = PublicClientApplicationBuilder
            .Create(OnlineAccountService.MicrosoftClientId)
            .WithAuthority("https://login.microsoftonline.com/consumers")
            .WithDefaultRedirectUri()
            .WithBroker(brokerOptions);

        if (parentWindowHandle != IntPtr.Zero)
            builder = builder.WithParentActivityOrWindow(() => parentWindowHandle);

        return builder.Build();
    }

    private static XboxAuthorization? GetXboxAuthorizationWithWamSilent(string relyingParty)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(OnlineAccountService.MicrosoftClientId))
            return null;

        try
        {
            var app = BuildWamApplication(IntPtr.Zero);
            var result = app.AcquireTokenSilent(MsalXboxScopes, PublicClientApplication.OperatingSystemAccount)
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();
            return OnlineAccountService.CreateXboxAuthorizationFromMicrosoftAccessToken(result.AccessToken, relyingParty);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "WAM 静默获取 Xbox 令牌失败");
            return null;
        }
    }

    private static string Text(string key, params object?[] args) => OnlineRuntime.Host.Text(key, args);
}
