using System.Threading;

namespace PCL.Core.App;

public static class GlobalUniqueId
{
    private static ulong _globalUniqueId = 0;

    public static ulong GetUniqueId()
    {
        return Interlocked.Increment(ref _globalUniqueId);
    }
}