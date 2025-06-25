using System;
using System.Net.Http;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using PCL.Core.Model.ResourceProject.Modrinth;

namespace PCL.Test.Project;

[TestClass]
public class Modrinth
{
    [TestMethod]
    public async Task GetProjectTest()
    {
        using var c = new HttpClient();
        using var req = new HttpRequestMessage();
        req.RequestUri = new Uri("https://api.modrinth.com/v2/project/sodium");
        req.Method = HttpMethod.Get;
        var ret = await c.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        Assert.IsNotNull(ret);
        ret.EnsureSuccessStatusCode();
        var s = await ret!.Content.ReadAsStreamAsync();
        Assert.IsNotNull(s);
        var instance = (ModrinthProject)(JsonSerializer.Deserialize(
            s,
            typeof(ModrinthProject),
            JsonSerializerOptions.Web) ?? throw new NullReferenceException());
        Assert.IsTrue(instance.slug == "sodium");
    }
}