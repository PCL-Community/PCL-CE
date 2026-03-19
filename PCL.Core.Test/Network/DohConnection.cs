using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Http.Client;

namespace PCL.Core.Test.Network;

[TestClass]
public class DohConnection
{
    [TestMethod]
    public async Task TestDohConnection()
    {
        using var client = new HttpClient(new SocketsHttpHandler()
            {
                ConnectCallback = HostConnectionHandler.Instance.GetConnectionAsync
            }
        );
        var request = new HttpRequestMessage(HttpMethod.Get, "https://www.cloudflare.com/cdn-cgi/trace")
        {
            Version = HttpVersion.Version30
        };
        using var response = await client.SendAsync(request, TestContext.CancellationTokenSource.Token);
        Assert.IsTrue(response.IsSuccessStatusCode);
        Assert.IsTrue(response.Content.Headers.ContentLength > 0);
        Console.WriteLine(await response.Content.ReadAsStringAsync(TestContext.CancellationTokenSource.Token));
    }

    public TestContext TestContext { get; set; }
}