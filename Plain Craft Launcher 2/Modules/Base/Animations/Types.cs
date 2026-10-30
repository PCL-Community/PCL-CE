using System.Collections.Concurrent;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     动画基础种类。
    /// </summary>
    public enum AniType
    {
        /// <summary>
        ///     单个Double的动画，包括位置、长宽、透明度等。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
        Number,

        /// <summary>
        ///     颜色属性的动画。这需要附属类型。
        /// </summary>
        /// <remarks></remarks>
        Color,

        /// <summary>
        ///     缩放控件大小。比起4个DoubleAnimation来说效率更高。
        /// </summary>
        /// <remarks></remarks>
        Scale,

        /// <summary>
        ///     文字一个个出现。
        /// </summary>
        /// <remarks></remarks>
        TextAppear,

        /// <summary>
        ///     执行代码。
        /// </summary>
        /// <remarks></remarks>
        Code,

        /// <summary>
        ///     以 WPF 方式缩放控件。
        /// </summary>
        ScaleTransform,

        /// <summary>
        ///     以 WPF 方式旋转控件。
        /// </summary>
        RotateTransform
    }

    /// <summary>
    ///     动画扩展种类。
    /// </summary>
    public enum AniTypeSub
    {
        X,
        Y,
        Width,
        Height,
        Opacity,
        Value,
        Radius,
        BorderThickness,
        StrokeThickness,
        TranslateX,
        TranslateY,
        Double,
        DoubleParam,
        GridLengthWidth
    }

    /// <summary>
    ///     动画速度。最大为 200。
    /// </summary>
    public static double aniSpeed = 1d;

    // 名称 -> 动画组。始终同步，因此 AniStart 后 AniIsRun 立即为 true。
    private static readonly Dictionary<string, AniGroupEntry> _aniByName = new(StringComparer.Ordinal);

    // 插入顺序。仅在帧开始时修改，因此帧循环读取时无需加锁。
    private static readonly List<AniGroupEntry> _aniOrder = [];

    // 当前帧内启动的动画组，在下一帧开始时发布。
    private static readonly List<AniGroupEntry> _aniPendingAdds = [];

    private static readonly Lock _aniGroupsLock = new();
    private static bool _aniOrderDirty;

    /// <summary>
    ///     上一次记刻的时间。
    /// </summary>
    private static long _aniLastTick;

    /// <summary>
    ///     动画模块是否正在运行。
    /// </summary>
    private static bool aniRunning;

    private static readonly object AniControlEnabledLock = new();

    /// <summary>
    ///     控件动画执行是否开启。先 +1，再 -1。
    /// </summary>
    private static int _aniControlEnabled;

    public static void AniBeginControl()
    {
        Interlocked.Add(ref _aniControlEnabled, 1);
    }

    public static void AniEndControl()
    {
        Interlocked.Add(ref _aniControlEnabled, -1);
    }

    public static bool AniExitsAnyControl()
    {
        return _aniControlEnabled != 0;
    }

    /// <summary>
    ///     Reset to target value and return rest value
    /// </summary>
    /// <param name="resetValue"></param>
    /// <returns></returns>
    public static int AniResetControl(int resetValue)
    {
        return Interlocked.Exchange(ref _aniControlEnabled, resetValue);
    }

    /// <summary>
    ///     Restore the value reset before
    /// </summary>
    /// <param name="restoreValue"></param>
    /// <returns></returns>
    public static int AniRestoreControl(int restoreValue)
    {
        return Interlocked.Add(ref _aniControlEnabled, restoreValue);
    }

    public class AniGroupEntry
    {
        public List<AniData> data = [];

        /// <summary>Stopwatch tick at which the group was started.</summary>
        public long startTick;

        /// <summary>Registry key, used to unregister the group without touching a same-named newcomer.</summary>
        public string Key = "";

        public int Uuid = Common.GetUuid();

        /// <summary>Set when the group is stopped or finished; the frame loop skips it immediately.</summary>
        internal volatile bool Dead;
    }

    /// <summary>
    ///     单个动画对象。
    /// </summary>
    /// <remarks></remarks>
    public struct AniData
    {
        /// <summary>
        ///     动画种类。
        /// </summary>
        /// <remarks></remarks>
        public AniType typeMain;

        /// <summary>
        ///     动画副种类。
        /// </summary>
        /// <remarks></remarks>
        public AniTypeSub typeSub;

        /// <summary>
        ///     动画总长度。
        /// </summary>
        /// <remarks></remarks>
        public int timeTotal;

        /// <summary>
        ///     已经执行的动画长度。如果为负数则为延迟。
        /// </summary>
        /// <remarks></remarks>
        public int timeFinished;

        /// <summary>
        ///     已经完成的百分比。
        /// </summary>
        /// <remarks></remarks>
        public double timePercent;

        /// <summary>
        ///     是否为“以后”。
        /// </summary>
        /// <remarks></remarks>
        public bool isAfter;

        /// <summary>
        ///     插值器类型。
        /// </summary>
        /// <remarks></remarks>
        public AniEase ease;

        /// <summary>
        ///     动画对象。
        /// </summary>
        /// <remarks></remarks>
        public object obj;

        /// <summary>
        ///     动画值。
        /// </summary>
        /// <remarks></remarks>
        public object value;

        /// <summary>
        ///     上次执行时的动画值。
        /// </summary>
        /// <remarks></remarks>
        public object valueLast;

        public override string ToString()
        {
            return
                $"{Common.GetStringFromEnum(typeMain)} | {timeFinished}/{timeTotal}({Math.Round(timePercent * 100d)}%){(obj is null ? "" : " | " + obj + "(" + obj.GetType().Name + ")")}";
        }
    }
}