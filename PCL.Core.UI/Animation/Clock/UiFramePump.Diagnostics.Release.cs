#if !DEBUG
namespace PCL.Core.UI.Animation.Clock;

internal static partial class UiFramePump
{
    private static void DiagStart()
    {
    }

    private static void DiagStop()
    {
    }

    private static void DiagRafCall()
    {
    }

    private static void DiagTimerCall()
    {
    }

    private static long DiagTickStart() => 0;

    private static void DiagTickEnd(long tickStamp)
    {
    }
}
#endif