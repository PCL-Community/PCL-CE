using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding;
using System;

namespace PCL.Test;

[TestClass]
public class LobbyCodeGenerateTest
{
    [TestMethod]
    public void GenerateTest()
    {
        var code = LobbyCodeGenerator.Generate();
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