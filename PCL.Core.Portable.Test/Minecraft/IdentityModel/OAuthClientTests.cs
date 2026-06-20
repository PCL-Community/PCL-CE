// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Test.Minecraft.IdentityModel;

[TestClass]
public sealed class OAuthClientTests
{
    [TestMethod]
    public void AuthorizeUrlEscapesStandardAndExtensionValues()
    {
        using var http = new HttpClient(new StubHandler(_ => Json(HttpStatusCode.OK, "{}")));
        var client = CreateClient(http);

        var url = client.GetAuthorizeUrl(
            ["XboxLive.signin", "offline_access"],
            "state with spaces",
            new Dictionary<string, string> { ["prompt"] = "select account" });

        Assert.IsTrue(url.StartsWith("https://login.example/authorize?", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("scope=XboxLive.signin%20offline_access", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("state=state%20with%20spaces", StringComparison.Ordinal));
        Assert.IsTrue(url.Contains("prompt=select%20account", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OAuthFlowsSendExpectedFormsWithoutMutatingExtensions()
    {
        var requests = new List<CapturedRequest>();
        using var http = new HttpClient(new StubHandler(async request =>
        {
            requests.Add(new CapturedRequest(
                request.RequestUri!.AbsolutePath,
                ParseForm(await request.Content!.ReadAsStringAsync()),
                request.Headers.GetValues("X-PCL-Test").Single()));
            return request.RequestUri.AbsolutePath.EndsWith("/device", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, """{"device_code":"device","user_code":"ABCD","interval":"5"}""")
                : Json(HttpStatusCode.OK, """{"access_token":"access","refresh_token":"refresh","expires_in":"3600"}""");
        }));
        var client = CreateClient(http);
        var extensions = new Dictionary<string, string> { ["resource"] = "minecraft" };

        var code = await client.AuthorizeWithCodeAsync("auth-code", CancellationToken.None, extensions);
        var device = await client.GetCodePairAsync(["scope-a"], CancellationToken.None);
        var deviceToken = await client.AuthorizeWithDeviceAsync(device!, CancellationToken.None);
        var refresh = await client.AuthorizeWithSilentAsync(code!, CancellationToken.None);

        Assert.AreEqual("access", code?.AccessToken);
        Assert.AreEqual(5, device?.Interval);
        Assert.AreEqual("access", deviceToken?.AccessToken);
        Assert.AreEqual("access", refresh?.AccessToken);
        Assert.AreEqual(4, requests.Count);
        Assert.IsFalse(extensions.ContainsKey("client_id"));
        Assert.AreEqual("authorization_code", requests[0].Form["grant_type"]);
        Assert.AreEqual("https://launcher.example/callback", requests[0].Form["redirect_uri"]);
        Assert.AreEqual("urn:ietf:params:oauth:grant-type:device_code", requests[2].Form["grant_type"]);
        Assert.AreEqual("refresh_token", requests[3].Form["grant_type"]);
        Assert.IsTrue(requests.All(static request => request.Header == "portable"));
    }

    [TestMethod]
    public async Task ErrorJsonIsReturnedEvenForFailureStatus()
    {
        using var http = new HttpClient(new StubHandler(_ =>
            Json(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"expired"}""")));
        var client = CreateClient(http);

        var result = await client.AuthorizeWithCodeAsync("expired-code", CancellationToken.None);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsError);
        Assert.AreEqual("invalid_grant", result.Error);
    }

    private static SimpleOAuthClient CreateClient(HttpClient http) =>
        new(new OAuthClientOptions
        {
            ClientId = "client id",
            RedirectUri = "https://launcher.example/callback",
            GetClient = () => http,
            Headers = new Dictionary<string, string> { ["X-PCL-Test"] = "portable" },
            Meta = new EndpointMeta
            {
                AuthorizeEndpoint = "https://login.example/authorize",
                DeviceEndpoint = "https://login.example/device",
                TokenEndpoint = "https://login.example/token"
            }
        });

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static Dictionary<string, string> ParseForm(string content) =>
        content.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(static pair => pair.Split('=', 2))
            .ToDictionary(
                static pair => Decode(pair[0]),
                static pair => Decode(pair.Length > 1 ? pair[1] : string.Empty),
                StringComparer.Ordinal);

    private static string Decode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    private sealed record CapturedRequest(
        string Path,
        Dictionary<string, string> Form,
        string Header);

    private sealed class StubHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this(request => Task.FromResult(handler(request)))
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            handler(request);
    }
}
