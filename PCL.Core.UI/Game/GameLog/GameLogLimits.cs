namespace PCL.Core.UI.Game.Log;

public static class GameLogLimits
{
    public static ulong GetMaxLineCount(int configured)
    {
        var maxLog = (ulong)Math.Max(0, configured);
        return maxLog switch
        {
            <= 5UL => (ulong)Math.Round(maxLog * 10m + 50m),
            <= 13UL => (ulong)Math.Round(maxLog * 50m - 150m),
            <= 28UL => (ulong)Math.Round(maxLog * 100m - 800m),
            _ => ulong.MaxValue
        };
    }
}