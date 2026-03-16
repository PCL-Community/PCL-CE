using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.Test.Network;

[TestClass]
public class WebServerTest
{
    private class TestHttpServer : HttpServer
    {
        public TestHttpServer(IPAddress[] listenAddr, ushort port = 0) : base(listenAddr, port)
        {
        }

        protected override void Init()
        {
            // 注册测试路由
            Register(HttpMethod.Get, "/test", async (request) =>
            {
                var path = request.Url?.AbsolutePath ?? string.Empty;
                return HttpRouteResponse.Text(path);
            });

            dynamic obj = new { a = 123, b = new { c = "test", d = "text" } };
            Register(HttpMethod.Get, "/json", async (request) =>
            {
                return HttpRouteResponse.Json(obj);
            });

            Register(HttpMethod.Get, "/", async (request) =>
            {
                return HttpRouteResponse.NoContent;
            });
        }
    }

    /// <summary>
    /// Please run the following command:
    /// <code>
    /// curl http://localhost:8080/test
    /// curl http://localhost:8080/json
    /// curl -I http://localhost:8080/any/path
    /// curl -I http://localhost:8080
    /// </code>
    /// </summary>
    [TestMethod]
    public async Task Test()
    {
        Console.WriteLine("Starting web server with default listen (127.0.0.1:8080)...");
        var server = new TestHttpServer([IPAddress.Parse("127.0.0.1")], 8080);
        
        server.Start();
        Console.WriteLine($"Server started on {string.Join(", ", server.Host)}:{server.Port}");
        Console.WriteLine("Test(/test): 200 OK (path)");
        Console.WriteLine("Test(/json): 200 OK (JSON response)");
        Console.WriteLine("Test(/any/path): 404 Not Found");
        Console.WriteLine("Test(/): 204 No Content");
        Console.WriteLine("Server is running. Press any key to stop...");

        using var client = new HttpClient();
        using var res = await client.GetAsync("http://127.0.0.1:8080/");
        Assert.AreEqual(HttpStatusCode.NoContent, res.StatusCode);
        using var res2 = await client.GetAsync("http://127.0.0.1:8080/test");
        Assert.AreEqual(HttpStatusCode.OK, res2.StatusCode);
        Console.WriteLine($"Request /test: {await res2.Content.ReadAsStringAsync()}");
        using var res3 = await client.GetAsync("http://127.0.0.1:8080/json");
        Assert.AreEqual(HttpStatusCode.OK, res3.StatusCode);
        Console.WriteLine($"Request /json: {await res3.Content.ReadAsStringAsync()}");
        
        // 等待一段时间让服务器运行
        await Task.Delay(1000);
        
        server.Dispose();
        Console.WriteLine("Test complete.");
    }

    public TestContext TestContext { get; set; }
}

