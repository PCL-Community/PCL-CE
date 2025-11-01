using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using PCL.Core.Link.Lobby;

namespace PCL.Test;

[TestClass]
public class LobbyCodeGenerateTest
{
    [TestMethod]
    public void GenerateAndParseTest()
    {
        var code = LobbyInfoGenerator.Generate();
        Console.WriteLine($"Try to parse: {code}");

        var success = LobbyInfoGenerator.TryParse(code.FullCode, out _);

        Assert.IsTrue(success);
    }
}