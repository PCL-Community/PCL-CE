using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Instance.Utils;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class McVersionComparerTests {
    // 辅助方法：断言 x 小于 y
    private static void AssertLessThan(IComparer<string> comparer, string? x, string? y) {
        var result = comparer.Compare(x, y);
        Assert.IsLessThan(0, result, $"Expected '{x}' to be less than '{y}', but Compare returned {result}");
    }

    // 辅助方法：断言 x 大于 y
    private static void AssertGreaterThan(IComparer<string> comparer, string? x, string? y) {
        var result = comparer.Compare(x, y);
        Assert.IsGreaterThan(0, result, $"Expected '{x}' to be greater than '{y}', but Compare returned {result}");
    }

    // 辅助方法：断言 x 等于 y
    private static void AssertEqual(IComparer<string> comparer, string? x, string? y) {
        var result = comparer.Compare(x, y);
        Assert.AreEqual(0, result, $"Expected '{x}' to be equal to '{y}', but Compare returned {result}");
    }

    #region NeoForge Tests

    [TestMethod]
    public void NeoForge_NewLogic_SemVerNumericSorting() {
        var comparer = new NeoForgeVersionComparer();

        // 测试核心需求：数字后缀应当按数值比较而不是字符比较
        // "alpha.2" 应该小于 "alpha.10" (如果是字符比较 '2' > '1' 会出错)
        AssertLessThan(comparer, "26.1.0.0-alpha.2+snapshot-1", "26.1.0.0-alpha.10+snapshot-1");
        AssertLessThan(comparer, "26.1.0.0-alpha.10+snapshot-1", "26.1.0.0-alpha.2+snapshot-2");
    }

    [TestMethod]
    public void NeoForge_NewLogic_MainVersionPriority() {
        var comparer = new NeoForgeVersionComparer();

        AssertLessThan(comparer, "26.1.0.0-beta.10", "26.1.1.0-beta.9");
        AssertGreaterThan(comparer, "27.0.0.0", "26.9.9.9");
    }
    
    [TestMethod]
    public void NeoForge_Transition_To_Release() {
        var comparer = new NeoForgeVersionComparer();

        // 模拟从 beta 到 正式版 的过程 (根据你的描述: 26.1.1.0-beta -> 26.1.1.15)
        // 26.1.1.15 (4个部分) vs 26.1.1.0-beta (4个部分)
        // 比较第四位: 15 > 0
        AssertGreaterThan(comparer, "26.1.1.15", "26.1.1.0-beta");
    }

    [TestMethod]
    [DataRow("26.1.0.0-alpha.1+snapshot-1", "26.1.0.0-alpha.2+snapshot-1", -1)]
    [DataRow("26.1.0.0-alpha.15+snapshot-1", "26.1.0.0-alpha.3+snapshot-2", -1)]
    [DataRow("26.1.0.0-alpha.10-snapshot.1", "26.1.0.0-beta.1", -1)]
    [DataRow("26.1.0.0-beta.1", "26.1.0.0-beta.2", -1)]
    [DataRow("26.1.0.0-beta", "26.1.0.0", -1)]
    [DataRow("26.1.0.0", "26.1.0.1", -1)]
    public void NeoForge_DataDriven_Scenarios(string v1, string v2, int expectedSign) {
        var comparer = new NeoForgeVersionComparer();
        var result = comparer.Compare(v1, v2);

        if (expectedSign == 0)
            Assert.AreEqual(0, result);
        else if (expectedSign < 0)
            Assert.IsLessThan(0, result, $"{v1} should be < {v2}");
        else
            Assert.IsGreaterThan(0, result, $"{v1} should be > {v2}");
    }

    #endregion

    #region Fabric Tests (回归测试)

    [TestMethod]
    public void Fabric_BuildNumber_Comparison() {
        var comparer = new FabricVersionComparer();

        // 基础数值比较
        AssertLessThan(comparer, "0.14.9+build.1", "0.14.9+build.2");

        // 关键：数字 vs 字符串排序 ("1" vs "10")
        // Fabric 使用了 explicit int parsing，所以这里必须正确
        AssertLessThan(comparer, "0.14.9+build.2", "0.14.9+build.10");
    }

    [TestMethod]
    public void Fabric_MainVersion_Priority() {
        var comparer = new FabricVersionComparer();
        AssertLessThan(comparer, "0.14.0+build.100", "0.15.0+build.1");
    }

    #endregion

    #region Quilt Tests (回归测试)

    [TestMethod]
    public void Quilt_BetaNumber_Comparison() {
        var comparer = new QuiltVersionComparer();

        // Quilt 格式通常是 0.14.9-beta.1
        AssertLessThan(comparer, "0.19.2-beta.1", "0.19.2-beta.2");

        // 验证数字排序
        AssertLessThan(comparer, "0.19.2-beta.9", "0.19.2-beta.10");
    }

    [TestMethod]
    public void Quilt_TrailingSlash_Ignored() {
        var comparer = new QuiltVersionComparer();
        // Quilt 特有的 TrimEnd('/') 逻辑
        AssertEqual(comparer, "0.19.2-beta.1/", "0.19.2-beta.1");
    }

    #endregion

    #region Edge Cases (通用测试)

    [TestMethod]
    public void Common_NullAndEmpty_Handling() {
        var comparers = new VersionComparerBase[] {
            new NeoForgeVersionComparer(),
            new FabricVersionComparer(),
            new QuiltVersionComparer(),
            new CleanroomVersionComparer()
        };

        foreach (var comparer in comparers) {
            Console.WriteLine(comparer);
            // NullHandling
            Assert.AreEqual(0, comparer.Compare(null, null));
            AssertLessThan(comparer, "1.0", null);
            AssertGreaterThan(comparer, null, "1.0");

            // Self comparison
            Assert.AreEqual(0, comparer.Compare("1.0.0", "1.0.0"));
        }
    }

    [TestMethod]
    public void Cleanroom_Optimization_Check() {
        // 虽然 Cleanroom 之前主要是 ordinal，但在基类升级后，
        // 如果你使用了 CompareSemVerSuffix，它应该也能处理更聪明的排序
        var comparer = new CleanroomVersionComparer();

        // 如果基类启用了数字感知比较，下面这个就会通过
        // 0.2.2-alpha1 vs 0.2.2-alpha10
        AssertLessThan(comparer, "0.2.2-alpha1", "0.2.2-alpha10");
    }

    #endregion
}
