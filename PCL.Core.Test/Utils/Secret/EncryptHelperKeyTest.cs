using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Secret;
using System;
using System.IO;
using System.Security.Cryptography;

namespace PCL.Core.Test.Utils.Secret;

[TestClass]
[DoNotParallelize] // EncryptHelper 的缓存是进程级静态
public class EncryptHelperKeyTest
{
    private static readonly byte[] Entropy = "PCL CE Encryption Key"u8.ToArray();
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PCLCE_KeyTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        EncryptHelper.OverrideKeyFile(Path.Combine(_tempDir, "UserKey.bin"));
        EncryptHelper.ResetKeyCache();
    }

    [TestCleanup]
    public void Cleanup()
    {
        EncryptHelper.ResetKeyCache();
        EncryptHelper.OverrideKeyFile(null);
        try { Directory.Delete(_tempDir, true); } catch { /* ignore */ }
    }

    [TestMethod]
    public void Version1KeyIsMigratedToCng()
    {
        var rawKey = RandomKey();
        Write(1, ProtectedData.Protect(rawKey, Entropy, DataProtectionScope.CurrentUser));

        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey);
        Assert.AreEqual(2u, ReadVersion(), "v1 密钥应被就地迁移为 v2");
        Assert.AreEqual(0, BrokenFiles().Length);

        EncryptHelper.ResetKeyCache(); // 迁移后的文件必须能重新读回同一把密钥
        CollectionAssert.AreEqual(rawKey, EncryptHelper.EncryptionKey);
    }

    [DataTestMethod]
    [DataRow(1u, 0)]  // 结构合法但内容损坏 / 版本未知，都应重建
    [DataRow(1u, 4)]
    [DataRow(7u, 4)]
    public void UnusableKeyFileIsQuarantinedAndRebuilt(uint version, int payloadLength)
    {
        var payload = new byte[payloadLength];
        Random.Shared.NextBytes(payload);
        Write(version, payload);

        Assert.AreEqual(32, EncryptHelper.EncryptionKey.Length);
        Assert.AreEqual(1, BrokenFiles().Length, "坏文件应被改名留存");
        Assert.AreEqual(2u, ReadVersion(), "应生成 version 2 新密钥");
    }

    [TestMethod]
    public void LockedKeyFileIsNotRotated()
    {
        Write(1, new byte[64]);
        using (new FileStream(EncryptHelper.KeyFilePath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // 瞬时占用是 IOException，必须原样抛出而不是轮换
            Assert.ThrowsExactly<IOException>(() => EncryptHelper.EncryptionKey);
        }
        Assert.IsTrue(File.Exists(EncryptHelper.KeyFilePath), "瞬时占用不应导致密钥被换掉");
        Assert.AreEqual(0, BrokenFiles().Length);
    }

    private static byte[] RandomKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static void Write(uint version, byte[] protectedKey) => File.WriteAllBytes(
        EncryptHelper.KeyFilePath,
        EncryptHelper.EncryptionData.ToBytes(new EncryptHelper.EncryptionData
        {
            Version = version,
            Data = protectedKey
        }));

    private uint ReadVersion() =>
        EncryptHelper.EncryptionData.FromBytes(File.ReadAllBytes(EncryptHelper.KeyFilePath)).Version;

    private string[] BrokenFiles() =>
        Directory.GetFiles(_tempDir, "UserKey.bin.broken-*");
}