// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

namespace PCL.Core.Test.Minecraft.IdentityModel;

[TestClass]
public sealed class OpenIdClientTests
{
    [TestMethod]
    public async Task InitializeAndJwksLookupUsePortableJsonAndHeaders()
    {
        var requests = new List<string>();
        using var http = new HttpClient(new StubHandler(request =>
        {
            Assert.AreEqual("portable", request.Headers.GetValues("X-PCL-Test").Single());
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath switch
            {
                "/.well-known/openid-configuration" => Json(DiscoveryJson),
                "/keys" => Json("""{"keys":[{"kty":"RSA","kid":"signing-key","alg":"RS256","n":"AQ","e":"AQAB"}]}"""),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }));
        var options = CreateOptions(http);

        await options.InitializeAsync(CancellationToken.None);
        var key = await options.GetSignatureKeyAsync("signing-key", CancellationToken.None);
        var oauth = await options.BuildOAuthOptionsAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "/.well-known/openid-configuration", "/keys" },
            requests);
        Assert.AreEqual("RSA", key.KeyType);
        Assert.AreEqual("AQAB", key.Exponent);
        Assert.AreEqual("portable", oauth.Headers!["X-PCL-Test"]);
        Assert.AreNotSame(options.Headers, oauth.Headers);
    }

    [TestMethod]
    public async Task ClientInitializationLoadsDiscoveryAndCreatesPkceClient()
    {
        using var http = new HttpClient(new StubHandler(_ => Json(DiscoveryJson)));
        var options = CreateOptions(http);
        var client = new OpenIdClient(options);

        await client.InitializeAsync(CancellationToken.None, checkAddress: true);
        var url = client.GetAuthorizeUrl(["openid"], "state");

        Assert.IsNotNull(options.Meta);
        Assert.IsTrue(url.StartsWith("https://identity.example/authorize?", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("code_challenge=", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("code_challenge_method=S256", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task MissingJwksKeyProducesConfigurationError()
    {
        using var http = new HttpClient(new StubHandler(request =>
            request.RequestUri!.AbsolutePath == "/keys"
                ? Json("""{"keys":[]}""")
                : Json(DiscoveryJson)));
        var options = CreateOptions(http);
        await options.InitializeAsync(CancellationToken.None);

        await Assert.ThrowsExactlyAsync<IdentityModelConfigurationException>(
            () => options.GetSignatureKeyAsync("missing", CancellationToken.None));
    }

    private static OpenIdOptions CreateOptions(HttpClient http) =>
        new()
        {
            OpenIdDiscoveryAddress =
                "https://identity.example/.well-known/openid-configuration",
            ClientId = "portable-client",
            RedirectUri = "https://launcher.example/callback",
            Headers = new Dictionary<string, string>
            {
                ["X-PCL-Test"] = "portable"
            },
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
          "issuer":"https://identity.example",
          "authorization_endpoint":"https://identity.example/authorize",
          "device_authorization_endpoint":"https://identity.example/device",
          "token_endpoint":"https://identity.example/token",
          "userinfo_endpoint":"https://identity.example/userinfo",
          "jwks_uri":"https://identity.example/keys",
          "scopes_supported":["openid"],
          "subject_types_supported":["public"],
          "id_token_signing_alg_values_supported":["RS256"]
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
