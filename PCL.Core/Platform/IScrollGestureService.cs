using System;

namespace PCL.Core.Platform;

[Flags]
public enum ScrollGesturePhase : ulong
{
    None = 0,
    Began = 1,
    Stationary = 2,
    Changed = 4,
    Ended = 8,
    Cancelled = 16,
    MayBegin = 32
}

public readonly record struct ScrollGestureInfo(
    bool HasPreciseDeltas,
    ScrollGesturePhase Phase,
    ScrollGesturePhase MomentumPhase)
{
    public bool IsMomentum => MomentumPhase != ScrollGesturePhase.None;
}

public interface IScrollGestureService
{
    double PreciseDeltaScale { get; }

    event Action<ScrollGestureInfo>? PhaseChanged;

    bool TryGetCurrent(out ScrollGestureInfo info);

    bool EnsurePhaseTracking();
}
