// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test.Encryption;

[TestClass]
public class AesGcmTest
{
    [TestMethod]
    public void TestAesGcmTestSimple()
    {
        var randomData = new byte[1024];
        Random.Shared.NextBytes(randomData);

        var randomKey = new byte[32];
        RandomNumberGenerator.Fill(randomKey);

        var encryptedData = Core.Utils.Encryption.AesGcmProvider.Instance.Encrypt(randomData, randomKey);
        var decryptedData = Core.Utils.Encryption.AesGcmProvider.Instance.Decrypt(encryptedData, randomKey);

        CollectionAssert.AreEqual(randomData, decryptedData);
    }
}
