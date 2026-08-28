using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Skin;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class OfflineSkinServerTest
{
    /// <summary>
    /// 回归测试：HttpServer 构造函数必须能为 IPv6 回环地址生成合法的 URI 前缀。
    /// 此前直接拼接 "http://::1:port/" 会在 AddPrefix 时抛
    /// "Only Uri prefixes with a valid hostname are supported"，导致离线皮肤注入静默失败。
    /// </summary>
    [TestMethod]
    public void Ctor_Ipv6Loopback_DoesNotThrow()
    {
        using var server = new OfflineSkinServer();
        Assert.IsTrue(server.Port > 0);
    }

    /// <summary>
    /// 启动后元数据路由可访问，且签名公钥非空。
    /// </summary>
    [TestMethod]
    public async Task Start_MetadataRoute_Reachable()
    {
        using var server = new OfflineSkinServer();
        server.AddCharacter(Guid.NewGuid(), "test", null);
        server.Start();

        using var client = new HttpClient();
        using var response = await client.GetAsync($"http://127.0.0.1:{server.Port}/");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, "signaturePublickey");
        StringAssert.Contains(body, "127.0.0.1");
        StringAssert.Contains(body, "PCL CE");
    }

    /// <summary>
    /// 注册角色后，hasJoined 能返回带 textures 签名的完整档案。
    /// </summary>
    [TestMethod]
    public async Task Start_HasJoined_ReturnsProfile()
    {
        using var server = new OfflineSkinServer();
        var uuid = Guid.NewGuid();
        server.AddCharacter(uuid, "Tester", null);
        server.Start();

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"http://127.0.0.1:{server.Port}/sessionserver/session/minecraft/hasJoined?username=Tester");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        StringAssert.Contains(body, uuid.ToString("N"));
        StringAssert.Contains(body, "textures");
        StringAssert.Contains(body, "signature");
    }

    /// <summary>
    /// 未知玩家 hasJoined 返回 404。
    /// </summary>
    [TestMethod]
    public async Task Start_HasJoined_UnknownPlayer_Returns404()
    {
        using var server = new OfflineSkinServer();
        server.AddCharacter(Guid.NewGuid(), "Tester", null);
        server.Start();

        using var client = new HttpClient();
        using var response = await client.GetAsync(
            $"http://127.0.0.1:{server.Port}/sessionserver/session/minecraft/hasJoined?username=Nobody");
        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
