using PCL.Core.UI.MsgBox;

namespace PCL.Controls.MyMsg;

/// <summary>
/// 弹窗动画参数配置。由工厂方法 <see cref="ForTheme" /> 按 <see cref="MsgBoxTheme" /> 生成不同配置。
/// 控件只管按参数执行，不需要判断 isWarn / 按钮数 等分支。
/// </summary>
public class MsgBoxAnimationProfile
{
    // ── Show 动画 ──
    public double ShowFadeMs { get; init; } = 120;
    public double ShowSlideMs { get; init; } = 300;
    public double ShowRotateMs { get; init; } = 300;
    public double ShowDelayMs { get; init; } = 60;
    public ModAnimation.AniEase ShowSlideEase { get; init; } =
        new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak);

    public ModAnimation.AniEase ShowRotateEase { get; init; } =
        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak);

    // ── Close 动画 ──
    public double CloseFadeMs { get; init; } = 80;
    public double CloseFadeDelayMs { get; init; } = 20;
    public double CloseSlideMs { get; init; } = 150;
    public double CloseSlideDistance { get; init; } = 20;
    public double CloseAngle { get; init; } = 6;
    public double CloseDelayMs { get; init; } = 30;
    public ModAnimation.AniEase CloseSlideEase { get; init; } = new ModAnimation.AniEaseOutFluent();

    public ModAnimation.AniEase CloseRotateEase { get; init; } =
        new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak);

    // ── 按钮样式 ──
    public bool HighlightPrimaryButton { get; private init; } = true;

    // ── 工厂 ──
    public static MsgBoxAnimationProfile ForTheme(MsgBoxTheme theme) => theme switch
    {
        MsgBoxTheme.Warning or MsgBoxTheme.Error => WarningProfile,
        _ => DefaultProfile
    };

    private static readonly MsgBoxAnimationProfile DefaultProfile = new();

    private static readonly MsgBoxAnimationProfile WarningProfile = new()
    {
        HighlightPrimaryButton = false
    };
}
