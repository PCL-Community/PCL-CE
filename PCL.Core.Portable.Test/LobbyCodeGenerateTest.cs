// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Link.Scaffolding;

namespace PCL.Core.Test;

[TestClass]
public sealed class LobbyCodeGenerateTest
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

        Assert.AreEqual(19, code.Length);
        Assert.AreEqual('-', code[4]);
        Assert.AreEqual('-', code[9]);
        Assert.AreEqual('-', code[14]);
        var parsed = LobbyCodeGenerator.TryParse(code);

        Assert.AreEqual(code, parsed);
    }

    [TestMethod]
    public void ParseNormalizesCaseAndSeparators()
    {
        Assert.AreEqual(
            "0123-4567-89AB-CDEF",
            LobbyCodeGenerator.TryParse("0123 4567-89ab cdef"));
    }

    [TestMethod]
    public void RoomIdAndShortCodeUseNetworkOrder()
    {
        CollectionAssert.AreEqual(
            new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB, 0xCD, 0xEF },
            LobbyCodeGenerator.GetRoomId("0123-4567-89AB-CDEF"));
        Assert.AreEqual("01234567", LobbyCodeGenerator.ToShortCode("0123-4567-89AB-CDEF"));
    }

    [TestMethod]
    public void InvalidInputIsRejected()
    {
        Assert.IsNull(LobbyCodeGenerator.TryParse("not-a-code"));
        Assert.IsNull(LobbyCodeGenerator.GetRoomId("1234"));
        Assert.AreEqual(string.Empty, LobbyCodeGenerator.ToShortCode("1234"));
    }
}
