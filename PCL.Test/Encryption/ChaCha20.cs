using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Exts;

namespace PCL.Test.Encryption;

[TestClass]
public class ChaCha20
{
    [TestMethod]
    public void TestChaCha20Simple()
    {
        var randomData = new byte[1024];
        Random.Shared.NextBytes(randomData);

        var randomKey = new byte[32];
        Random.Shared.NextBytes(randomKey);
        var randomKeyString = Convert.ToBase64String(randomKey).ToSecureString();

        var encryptedData = Core.Utils.Encryption.ChaCha20.Instance.Encrypt(randomData, randomKeyString);
        var decryptedData = Core.Utils.Encryption.ChaCha20.Instance.Decrypt(encryptedData, randomKeyString);

        Assert.AreEqual(randomData.Length, decryptedData.Length);
        for (var i = 0; i < decryptedData.Length; i++)
            Assert.AreEqual(decryptedData[i], randomData[i]);
    }
}