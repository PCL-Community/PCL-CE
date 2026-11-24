using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Secret;

namespace PCL.Core.Test.Utils.Secret;

[TestClass]
[DoNotParallelize] // EncryptHelper 的缓存是进程级静态
public class EncryptHelperKeyTest
{
    private static readonly byte[] Entropy = [.. "PCL CE Encryption Key"u8];
    private string _tempDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            $"PCLCE_KeyTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        EncryptHelper.OverrideKeyFile(Path.Combine(_tempDir, "UserKey.bin"));
        EncryptHelper.ResetKeyCache();
    }

    [TestCleanup]
    public void Cleanup()
    {
        EncryptHelper.ResetKeyCache();
        EncryptHelper.OverrideKeyFile(null);
        try
        {
            Directory.Delete(_tempDir, true);
        }
        catch
        {
            /* ignore */
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Version1KeyIsMigratedToCng()
    {
        var rawKey = RandomKey();
        Write(1, ProtectedData.Protect(rawKey, Entropy, DataProtectionScope.CurrentUser));

        Assert.AreSequenceEqual(rawKey, EncryptHelper.EncryptionKey);
        Assert.AreEqual(2u, ReadVersion(), "v1 密钥应被就地迁移为 v2");
        Assert.IsEmpty(BrokenFiles());

        EncryptHelper.ResetKeyCache(); // 迁移后的文件必须能重新读回同一把密钥
        Assert.AreSequenceEqual(rawKey, EncryptHelper.EncryptionKey);
    }

    [TestMethod]
    [DataRow(1u, 0)] // 结构合法但内容损坏 / 版本未知，都应重建
    [DataRow(1u, 4)]
    [DataRow(2u, 32)] // Windows DPAPI 密钥在非 Windows 上同样不可用
    [DataRow(7u, 4)]
    public void UnusableKeyFileIsQuarantinedAndRebuilt(uint version, int payloadLength)
    {
        var payload = new byte[payloadLength];
        Random.Shared.NextBytes(payload);
        Write(version, payload);

        Assert.HasCount(32, EncryptHelper.EncryptionKey);
        Assert.HasCount(1, BrokenFiles(), "坏文件应被改名留存");
        Assert.AreEqual(
            OperatingSystem.IsWindows() ? 2u : 3u,
            ReadVersion(),
            "应生成当前平台版本的新密钥");
    }

    [TestMethod]
    public void CreatedKeyIsReloadedFromDisk()
    {
        var key = EncryptHelper.EncryptionKey;
        Assert.HasCount(32, key);
        Assert.AreEqual(OperatingSystem.IsWindows() ? 2u : 3u, ReadVersion());

        EncryptHelper.ResetKeyCache(); // 落盘的密钥必须能重新读回同一把
        Assert.AreSequenceEqual(key, EncryptHelper.EncryptionKey);
        Assert.IsEmpty(BrokenFiles());
    }

    [TestMethod]
    public void LockedKeyFileIsNotRotated()
    {
        Write(1, new byte[64]);
        using (new FileStream(
                   EncryptHelper.KeyFilePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.None))
        {
            // 瞬时占用是 IOException，必须原样抛出而不是轮换
            Assert.ThrowsExactly<IOException>(() => EncryptHelper.EncryptionKey);
        }

        Assert.IsTrue(File.Exists(EncryptHelper.KeyFilePath), "瞬时占用不应导致密钥被换掉");
        Assert.IsEmpty(BrokenFiles());
    }

    private static byte[] RandomKey()
    {
        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    private static void Write(uint version, byte[] protectedKey) => File.WriteAllBytes(
        EncryptHelper.KeyFilePath,
        EncryptHelper.EncryptionData.ToBytes(
            new EncryptHelper.EncryptionData
            {
                Version = version,
                Data = protectedKey
            }));

    private static uint ReadVersion()
    {
        return EncryptHelper.EncryptionData
            .FromBytes(File.ReadAllBytes(EncryptHelper.KeyFilePath))
            .Version;
    }

    private string[] BrokenFiles()
    {
        return Directory.GetFiles(_tempDir, "UserKey.bin.broken-*");
    }
}