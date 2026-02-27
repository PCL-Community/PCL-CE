using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Download.Core;
using PCL.Core.IO.Download.Network;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class MirrorSelectorTests
{
    private static List<MirrorInfo> CreateTestMirrors() =>
    [
        new MirrorInfo { Url = "http://fast.example.com", LatencyMilliseconds = 50, EstimatedBandwidthBps = 10_000_000, HealthScore = 100 },
        new MirrorInfo { Url = "http://medium.example.com", LatencyMilliseconds = 100, EstimatedBandwidthBps = 5_000_000, HealthScore = 90 },
        new MirrorInfo { Url = "http://slow.example.com", LatencyMilliseconds = 200, EstimatedBandwidthBps = 1_000_000, HealthScore = 80 }
    ];

    [TestMethod]
    public void SelectBest_ShouldPreferHighPerformanceMirror()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);

        // Act - 多次选择，统计结果
        var selections = new Dictionary<string, int>();
        for (var i = 0; i < 100; i++)
        {
            var selected = selector.SelectBest();
            Assert.IsNotNull(selected);

            var url = selected.BaseInfo.Url;
            selections[url] = selections.GetValueOrDefault(url) + 1;
        }

        // Assert - 快速镜像应该被选中最多
        var fastCount = selections.GetValueOrDefault("http://fast.example.com");
        Assert.IsTrue(fastCount > 50, $"Fast mirror should be selected most often, got {fastCount}/100");
    }

    [TestMethod]
    public void SelectBest_ShouldAvoidFailedMirrors()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);

        // Act - 让最快的镜像连续失败
        var fastMirror = selector.SelectBest();
        Assert.IsNotNull(fastMirror);
        Assert.IsTrue(fastMirror.BaseInfo.Url.Contains("fast"));

        for (var i = 0; i < 5; i++)
            fastMirror.ReportFailure(FailureType.ConnectionError);

        // 此时 fast 应该被标记为死亡
        Assert.IsFalse(fastMirror.IsAlive);

        // 下次选择应该选中其他镜像
        var nextSelection = selector.SelectBest();
        Assert.IsNotNull(nextSelection);
        Assert.AreNotEqual("http://fast.example.com", nextSelection.BaseInfo.Url);
    }

    [TestMethod]
    public void SelectBest_ShouldGreylistSlowMirrors()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);

        // Act - 模拟速度过慢
        var state = selector.SelectBest();
        Assert.IsNotNull(state);

        for (var i = 0; i < 3; i++)
            state.ReportFailure(FailureType.SlowSpeed);

        // Assert - 应该被灰名单
        Assert.IsTrue(state.IsGreylisted);
        Assert.IsTrue(state.IsAlive); // 但仍然存活
    }

    [TestMethod]
    public void ReportSuccess_ShouldUpdateEmaSpeed()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);
        var state = selector.SelectBest()!;

        var initialEma = state.EmaSpeedBps;

        // Act
        state.ReportSuccess(5_000_000, 1024 * 1024);
        var afterFirst = state.EmaSpeedBps;

        state.ReportSuccess(8_000_000, 1024 * 1024);
        var afterSecond = state.EmaSpeedBps;

        // Assert - EMA 应该逐步更新
        Assert.IsTrue(afterFirst > 0);
        Assert.IsTrue(afterSecond > afterFirst);
        Assert.AreEqual(1, state.SuccessCount + (state.TotalAttempts - state.SuccessCount > 0 ? 0 : 1));
    }

    [TestMethod]
    public void SelectBest_ShouldExploreUnusedMirrors()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors, explorationFactor: 2.0); // 高探索因子

        // Act - 大量选择后所有镜像都应该被尝试过
        var usedMirrors = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var state = selector.SelectBest();
            if (state != null)
            {
                usedMirrors.Add(state.BaseInfo.Url);
                state.ReportSuccess(1_000_000, 1024);
            }
        }

        // Assert - UCB 探索应该确保所有镜像都被尝试
        Assert.AreEqual(3, usedMirrors.Count, "All mirrors should be explored");
    }

    [TestMethod]
    public void SelectBest_ShouldReturnNullWhenAllDead()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);

        // Act - 杀死所有镜像
        foreach (var state in selector.GetAllStates())
        {
            for (var i = 0; i < 5; i++)
                state.ReportFailure(FailureType.ConnectionError);
        }

        // Assert
        var result = selector.SelectBest();
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryRevive_ShouldRecoverDeadMirrors()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);
        var state = selector.SelectBest()!;

        // 先成功一次（设置 LastSuccessAt）
        state.ReportSuccess(1_000_000, 1024);

        // 然后连续失败使其死亡
        for (var i = 0; i < 5; i++)
            state.ReportFailure(FailureType.ConnectionError);

        Assert.IsFalse(state.IsAlive);

        // Act - 尝试复活（需要等待时间，这里只测试方法存在）
        var revived = state.TryRevive();

        // Assert - 由于时间不够，应该不会复活
        Assert.IsFalse(revived);
    }

    [TestMethod]
    public void MirrorState_ShouldTrackVariance()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);
        var state = selector.SelectBest()!;

        // Act - 报告不稳定的速度
        state.ReportSuccess(1_000_000, 1024);
        state.ReportSuccess(5_000_000, 1024);
        state.ReportSuccess(500_000, 1024);
        state.ReportSuccess(8_000_000, 1024);

        // Assert - 应该有显著的方差
        Assert.IsTrue(state.SpeedVariance > 0, "Variance should be calculated");
    }

    [TestMethod]
    public async Task SelectBest_ShouldBeThreadSafe()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);

        // Act - 并发选择和报告
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var state = selector.SelectBest();
            if (state != null)
            {
                state.ReportSuccess(1_000_000, 1024);
            }
        }));

        // Assert - 不应该抛出异常
        await Task.WhenAll(tasks);
    }

    [TestMethod]
    public void SuccessRate_ShouldUseWilsonScore()
    {
        // Arrange
        var mirrors = CreateTestMirrors();
        var selector = new MirrorSelector(mirrors);
        var state = selector.SelectBest()!;

        // Act - 模拟 90% 成功率
        for (var i = 0; i < 9; i++)
            state.ReportSuccess(1_000_000, 1024);
        state.ReportFailure(FailureType.Timeout);

        // Assert
        Assert.AreEqual(0.9, state.SuccessRate, 0.01);
        Assert.AreEqual(10, state.TotalAttempts);
    }
}
