// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

namespace PCL.Core.Test.Minecraft.IdentityModel;

[TestClass]
public sealed class YggdrasilClientTests
{
    [TestMethod]
    public async Task SharedClientIdIsUsedWhenLocalClientIdIsEmpty()
    {
        using var http = new HttpClient(new StubHandler(_ => Json(DiscoveryJson)));
        var options = CreateOptions(http, clientId: string.Empty);

        await options.InitializeAsync(CancellationToken.None);
        var oauth = await options.BuildOAuthOptionsAsync(CancellationToken.None);

        Assert.AreEqual("shared-client", oauth.ClientId);
        Assert.IsInstanceOfType<YggdrasilConnectMetaData>(options.Meta);
    }

    [TestMethod]
    public async Task MissingRequiredScopeIsRejected()
    {
        using var http = new HttpClient(new StubHandler(_ => Json(
            """
            {
              "issuer":"https://connect.example",
              "authorization_endpoint":"https://connect.example/authorize",
              "device_authorization_endpoint":"https://connect.example/device",
              "token_endpoint":"https://connect.example/token",
              "userinfo_endpoint":"https://connect.example/userinfo",
              "jwks_uri":"https://connect.example/keys",
              "scopes_supported":[
                "openid",
                "Yggdrasil.PlayerProfiles.Select"
              ],
              "subject_types_supported":["public"],
              "id_token_signing_alg_values_supported":["RS256"],
              "shared_client_id":"shared-client"
            }
            """)));
        var options = CreateOptions(http, clientId: "local-client");

        var exception = await Assert.ThrowsExactlyAsync<IdentityModelConfigurationException>(
            () => options.InitializeAsync(CancellationToken.None));

        Assert.IsTrue(
            exception.Message.Contains("Yggdrasil.Server.Join", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ClientInitializationLoadsMetadataWithoutExternalPreload()
    {
        using var http = new HttpClient(new StubHandler(_ => Json(DiscoveryJson)));
        var options = CreateOptions(http, clientId: "local-client");
        var client = new YggdrasilClient(options);

        await client.InitializeAsync(CancellationToken.None);
        var url = client.GetAuthorizeUrl(["openid"], "state");

        Assert.IsNotNull(options.Meta);
        Assert.IsTrue(url.StartsWith("https://connect.example/authorize?", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("client_id=local-client", StringComparison.Ordinal));
    }

    private static YggdrasilOptions CreateOptions(HttpClient http, string clientId) =>
        new()
        {
            OpenIdDiscoveryAddress =
                "https://connect.example/.well-known/openid-configuration",
            ClientId = clientId,
            RedirectUri = "https://launcher.example/callback",
            GetClient = () => http
        };

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private const string DiscoveryJson =
        """
        {
          "issuer":"https://connect.example",
          "authorization_endpoint":"https://connect.example/authorize",
          "device_authorization_endpoint":"https://connect.example/device",
          "token_endpoint":"https://connect.example/token",
          "userinfo_endpoint":"https://connect.example/userinfo",
          "jwks_uri":"https://connect.example/keys",
          "scopes_supported":[
            "openid",
            "Yggdrasil.PlayerProfiles.Select",
            "Yggdrasil.Server.Join"
          ],
          "subject_types_supported":["public"],
          "id_token_signing_alg_values_supported":["RS256"],
          "shared_client_id":"shared-client"
        }
        """;

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
