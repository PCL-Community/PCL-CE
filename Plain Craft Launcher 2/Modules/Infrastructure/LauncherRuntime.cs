namespace PCL;

/// <summary>
///     运行期状态，承接启动器全局运行态字段。
/// </summary>
public static class LauncherRuntime
{
    private static int _uuid = 1;

    /// <summary>
    ///     程序的打开计时。
    /// </summary>
    public static long ApplicationStartTick { get; set; } = TimeUtils.GetTimeTick();

    /// <summary>
    ///     程序打开时的时间。
    /// </summary>
    public static DateTime ApplicationOpenTime { get; set; } = DateTime.Now;

    /// <summary>
    ///     程序是否已结束。
    /// </summary>
    public static bool IsProgramEnded { get; set; }

    /// <summary>
    ///     是否开启调试模式提示。
    /// </summary>
    public static bool ModeDebug { get; set; }

    /// <summary>
    ///     获取一个全程序内不会重复的数字（伪 Uuid）。
    /// </summary>
    public static int GetUuid()
    {
        return Interlocked.Increment(ref _uuid);
    }
}