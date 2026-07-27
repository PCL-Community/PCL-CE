using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;

namespace PCL.Core.Test.Minecraft.Modpack;

/// <summary>
/// 整合包路径安全策略测试。整合包内的路径不可信，这层是防止写出到实例目录之外的唯一屏障。
/// </summary>
[TestClass]
public class ModpackPathPolicyTest
{
    [TestMethod]
    [DataRow("mods/example.jar")]
    [DataRow("config/sub/dir/a.cfg")]
    [DataRow("mods\\windows-style.jar")]
    [DataRow("./mods/example.jar")]
    [DataRow("mods//double//slash.jar")]
    public void AcceptsSafeRelativePaths(string input)
    {
        Assert.IsTrue(ModpackPathPolicy.TryNormalizeRelativePath(input, out var normalized));
        Assert.IsFalse(normalized.StartsWith(Path.DirectorySeparatorChar));
        Assert.IsFalse(normalized.Contains(".."));
    }

    [TestMethod]
    [DataRow("../evil.jar")]
    [DataRow("mods/../../evil.jar")]
    [DataRow("a/../../b")]                 // 逐段拒绝，不做抵消
    [DataRow("/absolute/path.jar")]
    [DataRow("\\absolute\\path.jar")]
    [DataRow("C:/Windows/System32/evil.dll")]
    [DataRow("C:\\Windows\\evil.dll")]
    [DataRow("mods/file.jar:stream")]      // NTFS 备用数据流
    [DataRow("mods/trailing. ")]
    [DataRow("mods/bad<name>.jar")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void RejectsUnsafePaths(string? input)
    {
        Assert.IsFalse(ModpackPathPolicy.TryNormalizeRelativePath(input, out _));
    }

    [TestMethod]
    public void NormalizesSeparators()
    {
        Assert.IsTrue(ModpackPathPolicy.TryNormalizeRelativePath("mods/sub/a.jar", out var normalized));
        Assert.AreEqual(Path.Combine("mods", "sub", "a.jar"), normalized);
    }

    [TestMethod]
    public void ResolveWithinKeepsPathInsideBase()
    {
        var root = Path.Combine(Path.GetTempPath(), "pclce-instance");
        var resolved = ModpackPathPolicy.ResolveWithin(root, "mods/a.jar");

        Assert.IsTrue(ModpackPathPolicy.IsWithin(root, resolved));
        Assert.AreEqual(Path.GetFullPath(Path.Combine(root, "mods", "a.jar")), resolved);
    }

    [TestMethod]
    public void ResolveWithinThrowsOnTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "pclce-instance");

        Assert.ThrowsExactly<ModpackUnsafePathException>(
            () => ModpackPathPolicy.ResolveWithin(root, "../../evil.dll"));
    }

    /// <summary>
    /// 前缀相同但并非子目录的路径不应被判定为「位于目录内」。
    /// </summary>
    [TestMethod]
    public void IsWithinRejectsSiblingWithSharedPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "instance");
        var sibling = Path.Combine(Path.GetTempPath(), "instance-evil", "a.jar");

        Assert.IsFalse(ModpackPathPolicy.IsWithin(root, sibling));
    }
}
