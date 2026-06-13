// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Test.Minecraft.IdentityModel;

[TestClass]
public sealed class PkceClientTest
{
    [TestMethod]
    public void CreateS256Challenge_ShouldMatchRfc7636Vector()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        const string expectedChallenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

        Assert.AreEqual(expectedChallenge, PkceClient.CreateS256Challenge(verifier));
    }

    [TestMethod]
    public void Base64UrlEncoding_ShouldRemovePadding()
    {
        var encoded = "PCL N"u8.FromBytesToB64UrlSafe();

        Assert.AreEqual("UENMIE4", encoded);
        CollectionAssert.AreEqual("PCL N"u8.ToArray(), encoded.FromB64UrlSafeToBytes());
    }
}
