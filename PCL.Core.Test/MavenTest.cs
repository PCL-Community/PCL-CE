using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

[TestClass]
public class MavenTest
{
    private const string UriBase = "https://copytiao.github.io/maven/copytiao/";
    private const string PathBase = "C:/Users/copytiao/AppData/Roaming/.minecraft/library/maven";

    [TestMethod]
    public void ParsePathAndUri()
    {
        // 坐标 → 期望的相对路径。断言具体值而不是「非空 + 语法合法」，
        // 否则 Resolve 返回未拼接的基地址或错误的制品位置时用例依然会通过。
        (string Id, string ExpectedPath)[] cases =
        [
            ("io.github.copytiao:pclce:0.6.2",
                "/io/github/copytiao/pclce/pclce-0.6.2.jar"),
            ("io.github.copytiao:pclce:jar:0.6.2",
                "/io/github/copytiao/pclce/pclce-0.6.2.jar"),
            // 非通用 packaging：按 classifier 处理
            ("io.github.copytiao:pclce:snapshot:0.6.2",
                "/io/github/copytiao/pclce/pclce-snapshot-0.6.2.jar"),
            // 五段式：group:artifact:packaging:classifier:version
            ("io.github.copytiao:pclce:jar:snapshot:0.6.2",
                "/io/github/copytiao/pclce/pclce-snapshot-0.6.2.jar")
        ];

        foreach (var (id, expectedPath) in cases)
        {
            var maven = new MavenArtifact(id);
            var uri = maven.Resolve(UriBase);
            var path = maven.Resolve(PathBase);
            Console.WriteLine($"{id}\n  Uri : {uri}\n  Path: {path}");

            Assert.AreEqual("https://copytiao.github.io/maven/copytiao" + expectedPath, uri,
                $"坐标 {id} 解析出的 Uri 不符");
            Assert.AreEqual(PathBase + expectedPath, path,
                $"坐标 {id} 解析出的 Path 不符");
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
