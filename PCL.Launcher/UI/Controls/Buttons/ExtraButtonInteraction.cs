using System.Windows.Input;
using Avalonia.Controls;

namespace PCL.UI.Controls;

/// <summary>两个附加按钮共用的命令、缩放与离场回弹逻辑。</summary>
internal static class ExtraButtonInteraction
{
    public static void ExecuteCommand(
        ICommand? command,
        object? parameter,
        ref bool isRunning)
    {
        if (isRunning || command is null || !command.CanExecute(parameter))
            return;

        isRunning = true;
        try
        {
            command.Execute(parameter);
        }
        finally
        {
            isRunning = false;
        }
    }

    public static void BeginLeftPress(
        Control owner,
        Control scaleSurface,
        string animationKey,
        ref bool isHeld,
        bool animate = true)
    {
        if (!isHeld && animate)
            StartScale(owner, scaleSurface, animationKey, 0.85d, -0.05d);
        isHeld = true;
    }

    public static void StartScale(
        Control owner,
        Control scaleSurface,
        string animationKey,
        double targetScale,
        double reboundScale,
        int reboundDuration = 60)
    {
        if (!ButtonAnimation.CanAnimate(owner))
        {
            ButtonAnimation.SetScale(scaleSurface, targetScale + reboundScale);
            return;
        }

        AnimationService.Run(
            new List<IAnimation>
            {
                new ScaleTransformAnimation(
                    scaleSurface,
                    targetScale - ButtonAnimation.CurrentScale(scaleSurface),
                    800,
                    ease: new FluentEaseOut(EasePower.Strong)),
                new ScaleTransformAnimation(
                    scaleSurface,
                    reboundScale,
                    reboundDuration,
                    ease: new FluentEaseOut())
            },
            animationKey);
    }

    public static void ReleaseScale(Control owner, Control scaleSurface, string animationKey)
    {
        if (!ButtonAnimation.CanAnimate(owner))
        {
            ButtonAnimation.SetScale(scaleSurface, 1d);
            return;
        }

        AnimationService.Run(
            new ScaleTransformAnimation(
                scaleSurface,
                1d - ButtonAnimation.CurrentScale(scaleSurface),
                300,
                ease: new BackEaseWithPowerOut()),
            animationKey);
    }

    public static void ResetScale(Control owner, Control scaleSurface, string animationKey)
    {
        if (ButtonAnimation.CanAnimate(owner))
            AnimationService.Run(
                new ScaleTransformAnimation(
                    scaleSurface,
                    1d - ButtonAnimation.CurrentScale(scaleSurface),
                    500,
                    ease: new FluentEaseOut()),
                animationKey);
        else
            ButtonAnimation.SetScale(scaleSurface, 1d);
    }

    public static void StopAnimations(
        string scaleKey,
        string mainScaleKey,
        string colorKey)
    {
        AnimationService.CancelAnimationByName(scaleKey);
        AnimationService.CancelAnimationByName(mainScaleKey);
        AnimationService.CancelAnimationByName(colorKey);
    }

    public static void OnPointerExited(
        Control owner,
        Control scaleSurface,
        string animationKey,
        Action refreshColor)
    {
        ResetScale(owner, scaleSurface, animationKey);
        refreshColor();
    }
}