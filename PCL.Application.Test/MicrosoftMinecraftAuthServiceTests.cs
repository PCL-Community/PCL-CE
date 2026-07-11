// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Accounts;

namespace PCL.Application.Test;

[TestClass]
public sealed class MicrosoftMinecraftAuthServiceTests
{
    [TestMethod]
    public async Task DeviceLogin_CompletesMicrosoftXboxAndMinecraftValidation()
    {
        int tokenPolls = 0;
        List<double> progressValues = [];
        using HttpClient client = new(new DelegateHandler(request =>
        {
            string path = request.RequestUri!.AbsoluteUri;
            if (path.EndsWith("/devicecode", StringComparison.Ordinal))
            {
                StringAssert.Contains(ReadBody(request), "scope=XboxLive.signin+offline_access");
                return Json("""
                    {"device_code":"device","user_code":"ABCD-EFGH","verification_uri":"https://microsoft.com/link","verification_uri_complete":"https://microsoft.com/link?otc=ABCD-EFGH","expires_in":900,"interval":1,"message":"Enter the code"}
                    """);
            }
            if (path.EndsWith("/token", StringComparison.Ordinal))
            {
                tokenPolls++;
                return tokenPolls == 1
                    ? Json("{" + "\"error\":\"authorization_pending\"}", HttpStatusCode.BadRequest)
                    : Json("""{"access_token":"ms-access","refresh_token":"ms-refresh"}""");
            }
            if (path.Contains("user.auth.xboxlive.com", StringComparison.Ordinal))
            {
                StringAssert.Contains(ReadBody(request), "ms-access");
                return Json("""{"Token":"xbl-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}""");
            }
            if (path.Contains("xsts.auth.xboxlive.com", StringComparison.Ordinal))
                return Json("""{"Token":"xsts-token","DisplayClaims":{"xui":[{"uhs":"user-hash"}]}}""");
            if (path.EndsWith("/authentication/login_with_xbox", StringComparison.Ordinal))
            {
                StringAssert.Contains(ReadBody(request), "XBL3.0 x=user-hash;xsts-token");
                return Json("""{"access_token":"minecraft-access"}""");
            }
            if (path.EndsWith("/minecraft/profile", StringComparison.Ordinal))
            {
                Assert.AreEqual("Bearer", request.Headers.Authorization?.Scheme);
                Assert.AreEqual("minecraft-access", request.Headers.Authorization?.Parameter);
                return Json("""
                    {"id":"0123456789abcdef0123456789abcdef","name":"Steve","skins":[{"state":"ACTIVE","url":"https://textures.example/skin.png"}]}
                    """);
            }
            if (path.EndsWith("/entitlements/mcstore", StringComparison.Ordinal))
                return Json("""{"items":[{"name":"product_minecraft"}]}""");
            throw new AssertFailedException("Unexpected request: " + path);
        }));
        MicrosoftMinecraftAuthService service = new(client, (_, _) => Task.CompletedTask);

        MicrosoftDeviceCodeInfo deviceCode = await service.RequestDeviceCodeAsync("client-id");
        MicrosoftMinecraftLoginResult result = await service.CompleteDeviceLoginAsync(
            "client-id",
            deviceCode,
            new InlineProgress<double>(progressValues.Add));

        Assert.AreEqual("ABCD-EFGH", deviceCode.UserCode);
        Assert.AreEqual(2, tokenPolls);
        Assert.AreEqual("Steve", result.Username);
        Assert.AreEqual("0123456789abcdef0123456789abcdef", result.Uuid);
        Assert.AreEqual("minecraft-access", result.AccessToken);
        Assert.AreEqual("ms-refresh", result.RefreshToken);
        Assert.AreEqual("https://textures.example/skin.png", result.SkinAddress);
        Assert.IsTrue(result.OwnsMinecraft);
        Assert.IsTrue(progressValues.Count > 0);
    }

    [TestMethod]
    public async Task RefreshAsync_UsesRefreshTokenBeforeMinecraftValidation()
    {
        using HttpClient client = new(new DelegateHandler(request =>
        {
            string path = request.RequestUri!.AbsoluteUri;
            return path switch
            {
                var value when value.EndsWith("/token", StringComparison.Ordinal) =>
                    Json("""{"access_token":"ms-new","refresh_token":"refresh-new"}"""),
                var value when value.Contains("user.auth.xboxlive.com", StringComparison.Ordinal) =>
                    Json("""{"Token":"xbl","DisplayClaims":{"xui":[{"uhs":"uhs"}]}}"""),
                var value when value.Contains("xsts.auth.xboxlive.com", StringComparison.Ordinal) =>
                    Json("""{"Token":"xsts","DisplayClaims":{"xui":[{"uhs":"uhs"}]}}"""),
                var value when value.EndsWith("/authentication/login_with_xbox", StringComparison.Ordinal) =>
                    Json("""{"access_token":"mc-new"}"""),
                var value when value.EndsWith("/minecraft/profile", StringComparison.Ordinal) =>
                    Json("""{"id":"uuid","name":"Alex","skins":[]}"""),
                var value when value.EndsWith("/entitlements/mcstore", StringComparison.Ordinal) =>
                    Json("""{"items":[{"name":"game_minecraft"}]}"""),
                _ => throw new AssertFailedException("Unexpected request: " + path)
            };
        }));
        MicrosoftMinecraftAuthService service = new(client, (_, _) => Task.CompletedTask);

        MicrosoftMinecraftLoginResult result = await service.RefreshAsync("client-id", "refresh-old");

        Assert.AreEqual("Alex", result.Username);
        Assert.AreEqual("mc-new", result.AccessToken);
        Assert.AreEqual("refresh-new", result.RefreshToken);
    }

    private static HttpResponseMessage Json(string value, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(value, Encoding.UTF8, "application/json")
        };

    private static string ReadBody(HttpRequestMessage request) =>
        request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
