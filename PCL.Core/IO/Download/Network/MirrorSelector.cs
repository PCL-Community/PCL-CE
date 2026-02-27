using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace PCL.Core.IO.Download.Network;

/// <summary>
/// 镜像运行时状态 - 追踪下载过程中的动态指标
/// </summary>
public sealed class MirrorState
{
    private const double EmaAlpha = 0.3;
    private const double DecayFactor = 0.95;
    private const int MinSamplesForReliability = 3;

    private SpinLock _lock = new(false);

    public required MirrorInfo BaseInfo { get; init; }

    // 性能指标 (优先使用运行时数据，其次是探测数据，最后基于延迟估算)
    public double EstimatedBandwidthBps => _runtimeBandwidth > 0
        ? _runtimeBandwidth
        : BaseInfo.EstimatedBandwidthBps > 0
            ? BaseInfo.EstimatedBandwidthBps
            : EstimateBandwidthFromLatency(BaseInfo.LatencyMilliseconds);

    private double _runtimeBandwidth;
    public double EmaSpeedBps { get; private set; }
    public double SpeedVariance { get; private set; }

    private static double EstimateBandwidthFromLatency(long latencyMs) =>
        latencyMs > 0 ? 65536.0 * 1000 / (latencyMs * 2) : 1_000_000;

    // 可靠性指标
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public int TotalAttempts => SuccessCount + FailureCount;
    public double SuccessRate => TotalAttempts > 0 ? (double)SuccessCount / TotalAttempts : 1.0;

    // 时序信息
    public DateTime LastUsedAt { get; private set; } = DateTime.MinValue;
    public DateTime LastSuccessAt { get; private set; } = DateTime.MinValue;
    public int SelectionCount { get; private set; }

    // 状态标记
    public bool IsAlive { get; private set; } = true;
    public bool IsGreylisted { get; private set; }
    public DateTime GreylistUntil { get; private set; }

    /// <summary>
    /// 报告一次成功的下载
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReportSuccess(double speedBps, long bytesTransferred)
    {
        var lockTaken = false;
        try
        {
            _lock.Enter(ref lockTaken);

            SuccessCount++;
            ConsecutiveFailures = 0;
            LastSuccessAt = DateTime.UtcNow;

            // EMA 速度更新
            EmaSpeedBps = EmaSpeedBps == 0
                ? speedBps
                : EmaAlpha * speedBps + (1 - EmaAlpha) * EmaSpeedBps;

            // 方差计算 (Welford's online algorithm)
            var delta = speedBps - EmaSpeedBps;
            SpeedVariance = (1 - EmaAlpha) * (SpeedVariance + EmaAlpha * delta * delta);

            // 带宽估算更新
            if (speedBps > _runtimeBandwidth)
                _runtimeBandwidth = speedBps;

            // 灰名单恢复
            if (IsGreylisted && DateTime.UtcNow > GreylistUntil)
            {
                IsGreylisted = false;
            }
        }
        finally
        {
            if (lockTaken) _lock.Exit(false);
        }
    }

    /// <summary>
    /// 报告一次失败
    /// </summary>
    public void ReportFailure(FailureType type)
    {
        var lockTaken = false;
        try
        {
            _lock.Enter(ref lockTaken);

            FailureCount++;
            ConsecutiveFailures++;

            var penaltySeconds = type switch
            {
                FailureType.Timeout => 30,
                FailureType.SlowSpeed => 15,
                FailureType.ConnectionError => 60,
                FailureType.HttpError => 45,
                _ => 20
            };

            // 连续失败指数退避
            var backoffMultiplier = Math.Min(ConsecutiveFailures, 5);
            penaltySeconds *= backoffMultiplier;

            if (ConsecutiveFailures >= 3)
            {
                IsGreylisted = true;
                GreylistUntil = DateTime.UtcNow.AddSeconds(penaltySeconds);
            }

            if (ConsecutiveFailures >= 5)
            {
                IsAlive = false;
            }

            // 速度估算衰减
            EmaSpeedBps *= 0.5;
        }
        finally
        {
            if (lockTaken) _lock.Exit(false);
        }
    }

    /// <summary>
    /// 标记已被选中使用
    /// </summary>
    public void MarkSelected()
    {
        var lockTaken = false;
        try
        {
            _lock.Enter(ref lockTaken);
            LastUsedAt = DateTime.UtcNow;
            SelectionCount++;
        }
        finally
        {
            if (lockTaken) _lock.Exit(false);
        }
    }

    /// <summary>
    /// 尝试从死亡状态恢复
    /// </summary>
    public bool TryRevive()
    {
        if (!IsAlive && LastSuccessAt != DateTime.MinValue)
        {
            var timeSinceDeath = DateTime.UtcNow - LastSuccessAt;
            if (timeSinceDeath.TotalMinutes > 2)
            {
                var lockTaken = false;
                try
                {
                    _lock.Enter(ref lockTaken);
                    IsAlive = true;
                    IsGreylisted = true;
                    GreylistUntil = DateTime.UtcNow.AddSeconds(10);
                    ConsecutiveFailures = 2; // 保留惩罚记录
                    return true;
                }
                finally
                {
                    if (lockTaken) _lock.Exit(false);
                }
            }
        }
        return false;
    }
}

public enum FailureType
{
    Timeout,
    SlowSpeed,
    ConnectionError,
    HttpError
}

/// <summary>
/// 智能镜像选择器 - 基于多因素评分和自适应学习
/// </summary>
/// <remarks>
/// 评分算法融合:
/// <list type="bullet">
/// <item>UCB1 (Upper Confidence Bound) 探索与利用平衡</item>
/// <item>EMA 指数移动平均速度追踪</item>
/// <item>加权多因子评分</item>
/// <item>自适应惩罚与恢复机制</item>
/// </list>
/// </remarks>
public sealed class MirrorSelector
{
    // 评分权重配置
    private static class Weights
    {
        public const double Latency = 0.15;
        public const double Bandwidth = 0.25;
        public const double Stability = 0.20;
        public const double SuccessRate = 0.25;
        public const double Recency = 0.10;
        public const double ExplorationBonus = 0.05;
    }

    private readonly List<MirrorState> _mirrors;
    private readonly Random _random = new();
    private readonly double _explorationFactor;
    private int _totalSelections;

    public MirrorSelector(List<MirrorInfo> mirrors, double explorationFactor = 1.5)
    {
        _mirrors = mirrors.Select(m => new MirrorState { BaseInfo = m }).ToList();
        _explorationFactor = explorationFactor;
    }

    /// <summary>
    /// 选择最优镜像
    /// </summary>
    /// <returns>选中的镜像状态，如果全部不可用则返回null</returns>
    public MirrorState? SelectBest()
    {
        // 尝试复活死亡镜像
        foreach (var mirror in _mirrors.Where(m => !m.IsAlive))
        {
            mirror.TryRevive();
        }

        var candidates = _mirrors
            .Where(m => m.IsAlive && !IsCurrentlyGreylisted(m))
            .ToList();

        if (candidates.Count == 0)
        {
            // 灰名单中选一个惩罚时间最短的
            var greylistCandidate = _mirrors
                .Where(m => m.IsAlive && m.IsGreylisted)
                .OrderBy(m => m.GreylistUntil)
                .FirstOrDefault();

            if (greylistCandidate != null)
            {
                greylistCandidate.MarkSelected();
                Interlocked.Increment(ref _totalSelections);
                return greylistCandidate;
            }

            return null;
        }

        // 计算所有候选者的UCB分数
        var scored = candidates
            .Select(m => (Mirror: m, Score: ComputeScore(m)))
            .ToList();

        // ε-greedy: 小概率随机探索
        MirrorState selected;
        if (_random.NextDouble() < 0.05 && scored.Count > 1)
        {
            // 轮盘赌选择 (按分数加权)
            selected = WeightedRandomSelect(scored);
        }
        else
        {
            selected = scored.OrderByDescending(s => s.Score).First().Mirror;
        }

        selected.MarkSelected();
        Interlocked.Increment(ref _totalSelections);
        return selected;
    }

    /// <summary>
    /// 获取所有镜像状态 (用于诊断)
    /// </summary>
    public IReadOnlyList<MirrorState> GetAllStates() => _mirrors.AsReadOnly();

    /// <summary>
    /// 综合评分计算
    /// </summary>
    private double ComputeScore(MirrorState mirror)
    {
        var latencyScore = ComputeLatencyScore(mirror);
        var bandwidthScore = ComputeBandwidthScore(mirror);
        var stabilityScore = ComputeStabilityScore(mirror);
        var successRateScore = ComputeSuccessRateScore(mirror);
        var recencyScore = ComputeRecencyScore(mirror);
        var ucbBonus = ComputeUcbBonus(mirror);

        return Weights.Latency * latencyScore
             + Weights.Bandwidth * bandwidthScore
             + Weights.Stability * stabilityScore
             + Weights.SuccessRate * successRateScore
             + Weights.Recency * recencyScore
             + Weights.ExplorationBonus * ucbBonus;
    }

    /// <summary>
    /// 延迟评分 - 指数衰减模型
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeLatencyScore(MirrorState mirror)
    {
        // 50ms 以下满分，每增加 100ms 分数减半
        var latency = mirror.BaseInfo.LatencyMilliseconds;
        return Math.Exp(-latency / 200.0);
    }

    /// <summary>
    /// 带宽评分 - 对数归一化
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeBandwidthScore(MirrorState mirror)
    {
        var speed = Math.Max(mirror.EmaSpeedBps, mirror.EstimatedBandwidthBps);
        if (speed <= 0) return 0.5; // 无数据时给予中等分数

        var maxSpeed = _mirrors.Max(m => Math.Max(m.EmaSpeedBps, m.EstimatedBandwidthBps));
        if (maxSpeed <= 0) return 0.5;

        // 对数归一化，避免极端值影响
        return Math.Log(1 + speed) / Math.Log(1 + maxSpeed);
    }

    /// <summary>
    /// 稳定性评分 - 基于速度方差的倒数
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeStabilityScore(MirrorState mirror)
    {
        if (mirror.TotalAttempts < 2) return 0.8;

        // CV (Coefficient of Variation) 越低越稳定
        var cv = mirror.EmaSpeedBps > 0
            ? Math.Sqrt(mirror.SpeedVariance) / mirror.EmaSpeedBps
            : 1.0;

        return 1.0 / (1.0 + cv);
    }

    /// <summary>
    /// 成功率评分 - Wilson score interval 下界
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeSuccessRateScore(MirrorState mirror)
    {
        var n = mirror.TotalAttempts;
        if (n == 0) return 0.9; // 未知时假设较高

        // Wilson score interval (95% confidence)
        var p = mirror.SuccessRate;
        const double z = 1.96;
        var z2 = z * z;

        var center = (p + z2 / (2 * n)) / (1 + z2 / n);
        var offset = z * Math.Sqrt((p * (1 - p) + z2 / (4 * n)) / n) / (1 + z2 / n);

        return Math.Max(0, center - offset);
    }

    /// <summary>
    /// 时效性评分 - 最近使用的镜像有亲和性加成
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double ComputeRecencyScore(MirrorState mirror)
    {
        if (mirror.LastUsedAt == DateTime.MinValue) return 0.5;

        var secondsSinceUse = (DateTime.UtcNow - mirror.LastUsedAt).TotalSeconds;

        // 30秒内使用过有加成，超过则衰减
        return secondsSinceUse switch
        {
            < 5 => 1.0,   // 刚用过，TCP连接可能还热
            < 30 => 0.8,
            < 120 => 0.5,
            _ => 0.3
        };
    }

    /// <summary>
    /// UCB1 探索奖励 - 鼓励尝试选择次数少的镜像
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ComputeUcbBonus(MirrorState mirror)
    {
        if (_totalSelections == 0 || mirror.SelectionCount == 0)
            return 1.0; // 未被选择过的镜像获得最大探索奖励

        // UCB1 formula: sqrt(2 * ln(total) / selections)
        return _explorationFactor * Math.Sqrt(2 * Math.Log(_totalSelections + 1) / mirror.SelectionCount);
    }

    /// <summary>
    /// 加权随机选择
    /// </summary>
    private MirrorState WeightedRandomSelect(List<(MirrorState Mirror, double Score)> scored)
    {
        var totalScore = scored.Sum(s => Math.Max(s.Score, 0.01));
        var randomValue = _random.NextDouble() * totalScore;
        var cumulative = 0.0;

        foreach (var (mirror, score) in scored)
        {
            cumulative += Math.Max(score, 0.01);
            if (randomValue <= cumulative)
                return mirror;
        }

        return scored.Last().Mirror;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCurrentlyGreylisted(MirrorState mirror) =>
        mirror.IsGreylisted && DateTime.UtcNow < mirror.GreylistUntil;
}
