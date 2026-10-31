using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding;
using System;

namespace PCL.Core.Test;

[TestClass]
public class LobbyCodeGenerateTest
{
    [TestMethod]
    public void GenerateTest()
    {
        var code = LobbyCodeGenerator.Generate();

        Assert.IsNotNull(code, "生成的房间码为 null");
        Assert.IsFalse(string.IsNullOrWhiteSpace(code.FullCode), "生成的房间码全文为空");
        Assert.IsFalse(string.IsNullOrWhiteSpace(code.FullCode.Trim()), "生成的房间码全文只包含空白字符");
    }

    [TestMethod]
    public void ParseTest()
    {
        var code = LobbyCodeGenerator.Generate();
        Console.WriteLine($"Try to parse: {code.FullCode}");

        var success = LobbyCodeGenerator.TryParse(code.FullCode, out _);

        Assert.IsTrue(success);
    }
}