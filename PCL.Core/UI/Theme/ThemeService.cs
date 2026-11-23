using System;
using System.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;
using PCL.Core.Platform;
using PCL.Core.UI.Threading;

namespace PCL.Core.UI.Theme;

/// <summary>配色模式更改事件。</summary>
/// <param name="isDarkMode">当前是否为深色模式</param>
/// <param name="theme">当前配色主题</param>
public delegate void ColorModeChangedEvent(bool isDarkMode, ColorTheme theme);

/// <summary>配色主题更改事件。</summary>
/// <param name="theme">当前配色主题</param>
public delegate void ColorThemeChangedEvent(ColorTheme theme);

[LifecycleScope("theme", "主题", false)]
[LifecycleService(LifecycleState.WindowCreating)]
public sealed partial class ThemeService
{
    private static int _customThemeRefreshQueued;

    [AnyConfigItem<ToneProfileConfig>("UiToneProfiles", ConfigSource.Local)]
    public static partial ToneProfileConfig ToneProfiles { get; set; }

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorModeConfigChanged =>
        new(
            Config.Preference.Theme.ColorModeConfig,
            trigger: ConfigEvent.Update,
            handler: _ => RefreshColorMode()
        );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorThemeConfigChanged =>
        new(
            [Config.Preference.Theme.DarkColorConfig, Config.Preference.Theme.LightColorConfig],
            trigger: ConfigEvent.Update,
            handler: e =>
            {
                // ignore no change or non-current color theme change
                if (e.OldValue == e.Value) return;
                if (IsDarkMode)
                {
                    if (e.Item == Config.Preference.Theme.LightColorConfig) return;
                }
                else
                {
                    if (e.Item == Config.Preference.Theme.DarkColorConfig) return;
                }

                // trigger color refresh
                if (Lifecycle.CurrentState > LifecycleState.Loading)
                    UiDispatcher.Post(_RefreshCurrentColorTheme);
            }
        );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnCustomThemeConfigChanged =>
        new(
            [
                Config.Preference.Theme.WindowHueConfig,
                Config.Preference.Theme.WindowSatConfig,
                Config.Preference.Theme.WindowLightConfig,
                Config.Preference.Theme.WindowDeltaConfig
            ],
            trigger: ConfigEvent.Update,
            handler: e =>
            {
                if (e.OldValue == e.Value || CurrentTheme != ColorTheme.Custom) return;
                if (Lifecycle.CurrentState > LifecycleState.Loading)
                    _QueueCustomThemeRefresh();
            }
        );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnPerModeCustomThemeConfigChanged =>
        new(
            [
                Config.Preference.Theme.LightCustomConfig,
                Config.Preference.Theme.DarkCustomConfig
            ],
            trigger: ConfigEvent.Update,
            handler: e =>
            {
                if (e.OldValue == e.Value || CurrentTheme != ColorTheme.Custom) return;
                var current = IsDarkMode
                    ? Config.Preference.Theme.DarkCustomConfig
                    : Config.Preference.Theme.LightCustomConfig;
                if (e.Item != current) return;
                if (Lifecycle.CurrentState > LifecycleState.Loading)
                    _QueueCustomThemeRefresh();
            }
        );

    /// <summary>当前是否为深色主题。</summary>
    public static bool IsDarkMode { get; private set; }

    /// <summary>当前使用的色彩属性。</summary>
    public static ToneProfile CurrentTone => IsDarkMode ? ToneProfiles.Dark : ToneProfiles.Light;

    /// <summary>当前使用的主题色。</summary>
    public static ColorTheme CurrentTheme
    {
        get
        {
            var theme = Config.Preference.Theme;
            return _NormalizeTheme(IsDarkMode ? theme.DarkColor : theme.LightColor);
        }
        set
        {
            var theme = Config.Preference.Theme;
            var config = IsDarkMode ? theme.DarkColorConfig : theme.LightColorConfig;
            config.SetValue(_NormalizeTheme(value));
        }
    }

    /// <summary>当前平台是否支持自定义主题。</summary>
    public static bool SupportsCustomThemes =>
        ApplicationRuntime.IsInitialized
        && ApplicationRuntime.Current.Platform.Capabilities.SupportsCustomThemes;

    /// <summary>系统当前是否使用深色外观，与 <see cref="IsDarkMode" /> 的当前模式无关。</summary>
    public static bool SystemIsDarkMode =>
        ApplicationRuntime.GetService<ISystemThemeProvider>()?.IsDarkMode ?? false;

    private static CatColorResource[] LightGrayCache
    {
        get => field ??= _CalculateGrays(ToneProfiles.Light);
        set;
    } = null!;

    private static CatColorResource[] DarkGrayCache
    {
        get => field ??= _CalculateGrays(ToneProfiles.Dark);
        set;
    } = null!;

    private static ColorTheme _NormalizeTheme(ColorTheme theme)
    {
        return !SupportsCustomThemes && theme == ColorTheme.Custom
            ? ColorTheme.CatBlue
            : theme;
    }

    [LifecycleStart]
    private static void _Start()
    {
        IsDarkMode = _IsDarkMode();
        _SubscribeSystemTheme();
        _LogStatus();
        _RefreshAll();
    }

    private static void _SubscribeSystemTheme()
    {
        if (ApplicationRuntime.GetService<ISystemThemeProvider>() is { } provider)
            provider.ThemeChanged += _OnSystemThemeChanged;
    }

    private static void _OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (Config.Preference.Theme.ColorMode != ColorMode.System) return;
        UiDispatcher.Post(RefreshColorMode);
    }

    private static void _LogStatus()
    {
        Context.Debug($"当前状态: {(IsDarkMode ? "Dark" : "Light")}, {CurrentTheme}");
    }

    private static bool _IsDarkMode()
    {
        return Config.Preference.Theme.ColorMode switch
        {
            ColorMode.Light => false,
            ColorMode.Dark => true,
            ColorMode.System => ApplicationRuntime.GetService<ISystemThemeProvider>()?.IsDarkMode
                ?? false,
            _ => false
        };
    }

    /// <summary>配色模式更改事件。</summary>
    public static event ColorModeChangedEvent? ColorModeChanged;

    /// <summary>配色主题更改事件。</summary>
    public static event ColorThemeChangedEvent? ColorThemeChanged;

    private static void _RefreshAll()
    {
        ApplyGrayResources();
        ApplyColorResources();
        ColorModeChanged?.Invoke(IsDarkMode, CurrentTheme);
        _AprilFoolLogic();
    }

    private static void _RefreshCurrentColorTheme()
    {
        ApplyColorResources();
        ColorThemeChanged?.Invoke(CurrentTheme);
        _AprilFoolLogic();
    }

    private static void _QueueCustomThemeRefresh()
    {
        if (Interlocked.Exchange(ref _customThemeRefreshQueued, 1) != 0) return;
        UiDispatcher.Post(() =>
        {
            Interlocked.Exchange(ref _customThemeRefreshQueued, 0);
            _RefreshCurrentColorTheme();
        });
    }

    private static void _AprilFoolLogic()
    {
        // for HMCL theme on April Fools' Day
        if (Basics.IsAprilFool)
            Config.Preference.WindowTitleTypeConfig.TriggerEvent(ConfigEvent.Changed, null);
        else if (CurrentTheme == ColorTheme.HmclBlue) CurrentTheme = ColorTheme.CatBlue;
    }

    /// <summary>刷新配色模式，若检测到当前配色模式有实际更改，则会触发主题刷新。</summary>
    public static void RefreshColorMode()
    {
        var isDarkMode = _IsDarkMode();
        if (IsDarkMode == isDarkMode) return;
        Context.Info("正在更改配色模式");
        IsDarkMode = isDarkMode;
        _LogStatus();
        if (Lifecycle.CurrentState > LifecycleState.Loading) UiDispatcher.Post(_RefreshAll);
    }

    /// <summary>获取当前色彩主题对应的各种参数。</summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust, int HueSpread)
        GetCurrentThemeArgs()
    {
        return GetThemeArgs(CurrentTheme, IsDarkMode);
    }

    /// <summary>获取指定模式与主题对应的各种参数，不改变当前主题状态。HueSpread 仅用于顶部标题栏渐变。</summary>
    public static (int Hue, double LightAdjust, double ChromaAdjust, int HueSpread) GetThemeArgs(
        ColorTheme theme,
        bool isDarkMode)
    {
        if (!SupportsCustomThemes && theme == ColorTheme.Custom)
            theme = ColorTheme.CatBlue;

        return theme switch
        {
            ColorTheme.SkyBlue => (235, 0.36, 0.2, 0),
            ColorTheme.CatBlue => (255, 0, -0.2, 0),
            ColorTheme.DeathBlue => (268, -0.05, -0.1, 0),
            ColorTheme.HmclBlue => (275, -0.03, -0.35, 0),
            ColorTheme.Custom => _CustomThemeArgs(GetCustomColor(isDarkMode)),
#if DEBUG
            _ => ((int)theme, 0, 0, 0)
#else
            _ => throw new IndexOutOfRangeException($"Invalid theme index: {(int)theme}")
#endif
        };
    }

    /// <summary>获取指定模式的自定义主题参数。新配置缺失时回退到旧版全局参数。</summary>
    public static ThemeCustomColor GetCustomColor(bool isDarkMode)
    {
        var theme = Config.Preference.Theme;
        var config = isDarkMode ? theme.DarkCustomConfig : theme.LightCustomConfig;
        return !config.IsDefault()
            ? (isDarkMode ? theme.DarkCustom : theme.LightCustom).Normalize()
            : ThemeCustomColor.FromLegacy(
                theme.WindowHue,
                theme.WindowSat,
                theme.WindowLight,
                theme.WindowDelta);
    }

    /// <summary>写入指定模式的自定义主题参数。</summary>
    public static void SetCustomColor(bool isDarkMode, ThemeCustomColor value)
    {
        var normalized = value.Normalize();
        if (isDarkMode)
            Config.Preference.Theme.DarkCustom = normalized;
        else
            Config.Preference.Theme.LightCustom = normalized;
    }

    private static (int Hue, double LightAdjust, double ChromaAdjust, int HueSpread)
        _CustomThemeArgs(
            ThemeCustomColor custom)
    {
        var normalized = custom.Normalize();
        return (
            normalized.Hue,
            normalized.LightnessBias,
            normalized.ChromaScale,
            normalized.HueSpread
        );
    }

    private static CatColorResource[] _CalculateGrays(ToneProfile tone)
    {
        return
        [
            OklabColor
                .FromLch(tone.L1)
                .ToCatColor("Gray1"),
            OklabColor
                .FromLch(tone.L2)
                .ToCatColor("Gray2"),
            OklabColor
                .FromLch(tone.L3)
                .ToCatColor("Gray3"),
            OklabColor
                .FromLch(tone.L4)
                .ToCatColor("Gray4"),
            OklabColor
                .FromLch(tone.L5)
                .ToCatColor("Gray5"),
            OklabColor
                .FromLch(tone.L6)
                .ToCatColor("Gray6"),
            OklabColor
                .FromLch(tone.L7)
                .ToCatColor("Gray7"),
            OklabColor
                .FromLch(tone.L8)
                .ToCatColor("Gray8"),
            OklabColor
                .FromLch(tone.LWhite, alpha: tone.AHalfWhite)
                .ToCatColor("HalfWhite", false),
            OklabColor
                .FromLch(tone.LWhite, alpha: tone.ASemiWhite)
                .ToCatColor("SemiWhite", false),
            OklabColor
                .FromLch(tone.LWhite)
                .ToCatColor("White", false),
            OklabColor
                .FromLch(tone.LWhite, alpha: tone.ATransparent)
                .ToCatColor("Transparent", false),
            OklabColor
                .FromLch(tone.LBackground, alpha: tone.ABackground)
                .ToCatColor("TransparentBackground", false),
            OklabColor
                .FromLch(tone.LBackground)
                .ToCatColor("Background", false),
            OklabColor
                .FromLch(tone.LBackground, alpha: tone.AToolTip)
                .ToCatColor("ToolTip", false),
            OklabColor
                .FromLch(tone.L7, 0.25, 30, tone.AHalfTransparent)
                .ToCatColor("RedBack", false),
            OklabColor
                .FromLch(tone.LForeground)
                .ToCatColor("Memory", false)
        ];
    }

    /// <summary>按指定色阶与参数计算彩色资源，不读取配置，也不写入应用资源。色阶只使用基色，色相渐变仅用于顶部标题栏。</summary>
    public static CatColorResource[] CalculateColorResources(
        ToneProfile tone,
        (int hue, double lightAdj, double chromaAdj, int hueSpread) args)
    {
        return
        [
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L1, args.lightAdj * 0.1),
                    ThemeColorMath.AdjustLinear(tone.C1, args.chromaAdj * 0.25),
                    Hue())
                .ToCatColor("1"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L2, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C2, args.chromaAdj),
                    Hue())
                .ToCatColor("2"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L3, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C3, args.chromaAdj),
                    Hue())
                .ToCatColor("3"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L4, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C4, args.chromaAdj),
                    Hue())
                .ToCatColor("4"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L5, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C5, args.chromaAdj),
                    Hue())
                .ToCatColor("5"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L6, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C6, args.chromaAdj),
                    Hue())
                .ToCatColor("6"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L7, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C7, args.chromaAdj),
                    Hue())
                .ToCatColor("7"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L8, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C8, args.chromaAdj),
                    Hue())
                .ToCatColor("8"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L8, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C8, args.chromaAdj),
                    Hue(),
                    tone.ASemiTransparent)
                .ToCatColor("SemiTransparent", false),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L5, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C5, args.chromaAdj),
                    Hue())
                .ToCatColor("Bg0"),
            OklabColor
                .FromLch(
                    ThemeColorMath.AdjustLinear(tone.L7, args.lightAdj),
                    ThemeColorMath.AdjustLinear(tone.C7, args.chromaAdj),
                    Hue(),
                    tone.ASemiWhite)
                .ToCatColor("Bg1")
        ];

        int Hue()
        {
            return args.hue;
        }
    }

    /// <summary>清除灰度配色的计算缓存。</summary>
    public static void InvalidateGrayCache()
    {
        LightGrayCache = null!;
        DarkGrayCache = null!;
    }

    /// <summary>
    /// 把灰度配色写入界面框架的应用资源字典（由 <see cref="PCL.Core.Platform.IThemeBackend" /> 实现， Avalonia
    /// 侧即 <c>Application.Resources</c>）。
    /// </summary>
    public static void ApplyGrayResources()
    {
        var cache = IsDarkMode ? DarkGrayCache : LightGrayCache;
        foreach (var c in cache) c.Apply();
    }

    /// <summary>
    /// 把彩色配色写入界面框架的应用资源字典（由 <see cref="PCL.Core.Platform.IThemeBackend" /> 实现， Avalonia
    /// 侧即 <c>Application.Resources</c>）。
    /// </summary>
    public static void ApplyColorResources()
    {
        var colors = CalculateColorResources(CurrentTone, GetCurrentThemeArgs());
        foreach (var c in colors) c.Apply();
    }
}