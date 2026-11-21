using PCL.Core.Platform;

namespace PCL.Platform;

public sealed class UnsupportedScrollGestureService : IScrollGestureService
{
    public static readonly UnsupportedScrollGestureService Instance = new();

    public double PreciseDeltaScale => 1d;

    public event Action<ScrollGestureInfo>? PhaseChanged
    {
        add { }
        remove { }
    }

    public bool TryGetCurrent(out ScrollGestureInfo info)
    {
        info = default;
        return false;
    }

    public bool EnsurePhaseTracking()
    {
        return false;
    }
}
