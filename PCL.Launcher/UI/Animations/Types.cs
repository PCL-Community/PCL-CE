using System.Collections;
using PCL.Core.App;
using PCL.Core.App.Configuration;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     动画基础种类。
    /// </summary>
    public enum AniType
    {
        /// <summary>
        ///     单个 Double 的动画，包括位置、长宽、透明度等。这需要附属类型。
        /// </summary>
        Number,

        /// <summary>
        ///     颜色属性的动画。这需要附属类型。
        /// </summary>
        Color,

        /// <summary>
        ///     缩放控件大小。比起 4 个 DoubleAnimation 来说效率更高。
        /// </summary>
        Scale,

        /// <summary>
        ///     文字一个个出现。
        /// </summary>
        TextAppear,

        /// <summary>
        ///     执行代码。
        /// </summary>
        Code,

        /// <summary>
        ///     以变换方式缩放控件。
        /// </summary>
        ScaleTransform,

        /// <summary>
        ///     以变换方式旋转控件。
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

    private const int DefaultSpeedValue = 9;

    private static double? _aniSpeedOverride;

    /// <summary>
    ///     动画速度。最大为 200。
    ///     默认跟随 <see cref="Config.Debug" /> 中的动画速度设置（默认 9 → 1.0）。
    /// </summary>
    public static double AniSpeed
    {
        get =>
            _aniSpeedOverride
            ?? (ConfigService.IsInitialized ? _MapSpeedValue(Config.Debug.AnimationSpeed) : 1d);
        set => _aniSpeedOverride = value;
    }

    /// <summary>
    ///     将配置中的动画速度（0-30+）映射为播放倍率。
    /// </summary>
    public static double _MapSpeedValue(int value)
    {
        return value >= 30
            ? 200d
            : Math.Clamp(value * 0.1d + 0.1d, 0.1d, 3d);
    }

    /// <summary>
    ///     清除显式设置的速度，恢复跟随配置。
    /// </summary>
    public static void AniResetSpeed()
    {
        _aniSpeedOverride = null;
    }

    // 名称 -> 动画组。始终同步，因此 AniStart 后 AniIsRun 立即为 true。
    private static readonly Dictionary<string, AniGroupEntry> AniByName =
        new(StringComparer.Ordinal);

    // 插入顺序。仅在帧开始时修改，因此帧循环读取时无需加锁。
    private static readonly List<AniGroupEntry> AniOrder = [];

    // 当前帧内启动的动画组，在下一帧开始时发布。
    private static readonly List<AniGroupEntry> AniPendingAdds = [];

    private static readonly Lock AniGroupsLock = new();
    private static bool _aniOrderDirty;

    /// <summary>
    ///     上一次记刻的时间。
    /// </summary>
    private static long _aniLastTick;

    /// <summary>
    ///     动画模块是否正在运行。
    /// </summary>
    private static bool _aniRunning;

    /// <summary>
    ///     控件动画执行是否开启。先 +1，再 -1。
    /// </summary>
    private static int _aniControlEnabled;

    /// <summary>
    ///     全局自增序号，用于生成不会重复的动画名。
    /// </summary>
    private static int _aniUuid;

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
    ///     重置控件动画执行计数，并返回重置前的值。
    /// </summary>
    public static int AniResetControl(int resetValue)
    {
        return Interlocked.Exchange(ref _aniControlEnabled, resetValue);
    }

    /// <summary>
    ///     恢复重置前的控件动画执行计数。
    /// </summary>
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

        public int Uuid = GetUuid();

        /// <summary>Set when the group is stopped or finished; the frame loop skips it immediately.</summary>
        internal volatile bool Dead;
    }

    /// <summary>
    ///     单个动画对象。
    /// </summary>
    public struct AniData
    {
        /// <summary>
        ///     动画种类。
        /// </summary>
        public AniType typeMain;

        /// <summary>
        ///     动画副种类。
        /// </summary>
        public AniTypeSub typeSub;

        /// <summary>
        ///     动画总长度。
        /// </summary>
        public int timeTotal;

        /// <summary>
        ///     已经执行的动画长度。如果为负数则为延迟。
        /// </summary>
        public int timeFinished;

        /// <summary>
        ///     已经完成的百分比。
        /// </summary>
        public double timePercent;

        /// <summary>
        ///     是否为“以后”。
        /// </summary>
        public bool isAfter;

        /// <summary>
        ///     插值器类型。
        /// </summary>
        public AniEase? ease;

        /// <summary>
        ///     动画对象。
        /// </summary>
        public object? obj;

        /// <summary>
        ///     动画值。
        /// </summary>
        public object? value;

        /// <summary>
        ///     上次执行时的动画值。
        /// </summary>
        public object? valueLast;

        public override string ToString()
        {
            return
                $"{GetStringFromEnum(typeMain)} | {timeFinished}/{timeTotal}({Math.Round(timePercent * 100d)}%){(obj is null ? "" : " | " + obj + "(" + obj.GetType().Name + ")")}";
        }
    }

    /// <summary>
    ///     生成一个程序内不会重复的序号（伪 Uuid）。
    /// </summary>
    internal static int GetUuid()
    {
        return Interlocked.Increment(ref _aniUuid);
    }

    /// <summary>
    ///     将元素与 List 的混合体拆分为元素组。
    /// </summary>
    internal static List<T> GetFullList<T>(IList data)
    {
        var result = new List<T>();
        for (int i = 0, loopTo = data.Count - 1; i <= loopTo; i++)
            if (data[i] is ICollection collection)
                result.AddRange((IEnumerable<T>)collection);
            else
                result.Add((T)data[i]!);
        return result;
    }

    /// <summary>
    ///     返回一个枚举对应的字符串。
    /// </summary>
    internal static string? GetStringFromEnum(Enum enumData)
    {
        return Enum.GetName(enumData.GetType(), enumData);
    }

    /// <summary>
    ///     矩形值（AaScale 使用）。
    /// </summary>
    public struct MyRect(
        double left,
        double top,
        double width,
        double height)
    {
        public double Left = left;
        public double Top = top;
        public double Width = width;
        public double Height = height;
    }
}