using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

[TestClass]
public class MavenTest
{
    [TestMethod]
    public void ParsePathAndUri()
    {
        string[] mavenId = ["io.github.copytiao:pclce:0.6.2","io.github.copytiao:pclce:jar:0.6.2",
        "io.github.copytiao:pclce:snapshot:0.6.2",
        "io.github.copytiao:pclce:jar:snapshot:0.6.2"];
        foreach(var id in mavenId)
        {
            var maven = new MavenArtifact(id);
            var uri = maven.Resolve("https://copytiao.github.io/maven/copytiao/");
            var path = maven.Resolve("C:/Users/copytiao/AppData/Roaming/.minecraft/library/maven");
            Console.WriteLine($"Uri: {uri}");
            Console.WriteLine($"Path: {path}");

            Assert.IsFalse(string.IsNullOrWhiteSpace(uri), $"坐标 {id} 解析出的 Uri 为空");
            Assert.IsFalse(string.IsNullOrWhiteSpace(path), $"坐标 {id} 解析出的 Path 为空");
            Assert.IsTrue(Uri.IsWellFormedUriString(uri, UriKind.Absolute), $"坐标 {id} 解析出的 Uri 不是绝对地址");
        }
    }
    [TestMethod]
    public void ParseInvalidId()
    {
        string[] badIds = [
            "io",
            "io.github.copytiao:luotianyi:yuezhengling:xinchen:pclce:tests:aaa"
        ];
        foreach(var id in badIds)
        {
            // 非法坐标必须被拒绝；此前该用例只是把异常打印出来，无论是否抛出都会“通过”
            Assert.ThrowsExactly<FormatException>(
                () => new MavenArtifact(id).Resolve(""),
                $"非法 Maven 坐标 {id} 未被拒绝");
        }
    }
}