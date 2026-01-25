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

        Assert.AreEqual(randomData.Length, decryptedData.Length);
        for (var i = 0; i < decryptedData.Length; i++)
            Assert.AreEqual(decryptedData[i], randomData[i]);
    }
}