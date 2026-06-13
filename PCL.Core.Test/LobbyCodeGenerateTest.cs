// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding;

namespace PCL.Core.Test;

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
        Console.WriteLine($"Try to parse: {code}");

        var parsed = LobbyCodeGenerator.TryParse(code);

        Assert.AreEqual(code, parsed);
    }
}
