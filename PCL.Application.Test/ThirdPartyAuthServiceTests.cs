// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using PCL.Application.Accounts;

namespace PCL.Application.Test;

[TestClass]
public sealed class ThirdPartyAuthServiceTests
{
    [TestMethod]
    public async Task AuthenticateAsync_ParsesSelectedProfile()
    {
        using HttpClient client = new(new DelegateHandler(request =>
        {
            Assert.AreEqual(
                "https://example.com/api/yggdrasil/authserver/authenticate",
                request.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "accessToken": "token-123",
                      "selectedProfile": {
                        "id": "uuid-123",
                        "name": "Steve"
                      }
                    }
                    """)
            };
        }));
        ThirdPartyAuthService service = new(client);

        ThirdPartyAuthLoginResult result = await service.AuthenticateAsync(
            new ThirdPartyAuthLoginRequest("https://example.com", "steve@example.com", "secret"));

        Assert.AreEqual("Steve", result.Username);
        Assert.AreEqual("uuid-123", result.Uuid);
        Assert.AreEqual("token-123", result.AccessToken);
        Assert.AreEqual("https://example.com/api/yggdrasil", result.AuthServer);
        Assert.AreEqual("example.com", result.AuthServerDisplayName);
    }

    [TestMethod]
    public async Task AuthenticateAsync_UsesServerErrorMessage()
    {
        using HttpClient client = new(new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("""{"error":"ForbiddenOperationException","errorMessage":"密码错误"}""")
        }));
        ThirdPartyAuthService service = new(client);

        try
        {
            await service.AuthenticateAsync(
                new ThirdPartyAuthLoginRequest("https://example.com/api/yggdrasil/authserver", "steve", "bad"));
            Assert.Fail("AuthenticateAsync should throw when the auth server rejects the request.");
        }
        catch (InvalidOperationException exception)
        {
            Assert.AreEqual("密码错误", exception.Message);
        }
    }

    [TestMethod]
    public void NormalizeYggdrasilServer_AppendsApiPath()
    {
        Assert.AreEqual(
            "https://example.com/api/yggdrasil",
            ThirdPartyAuthService.NormalizeYggdrasilServer("example.com/"));
        Assert.AreEqual(
            "https://example.com/api/yggdrasil",
            ThirdPartyAuthService.NormalizeYggdrasilServer("https://example.com/api/yggdrasil/authserver"));
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
