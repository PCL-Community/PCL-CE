using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Theme;

namespace PCL;

public partial class Setup
{
    /// <summary>
    ///     将布尔值转换为控件可见性。
    /// </summary>
    private static Visibility ToVisibility(bool visible)
    {
        return visible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public static void UiLauncherTransparent(int value)
    {
        Launcher.frmMain?.Opacity = value / 1000d + 0.4d;
    }

    public static void UiLauncherTheme(int value)
    {
        ThemeManager.ThemeRefresh(value);
    }

    public static void UiBackgroundColorful(bool value)
    {
        ThemeManager.ThemeRefresh();
    }

    public static void UiLockWindowSize(bool value)
    {
        if (value)
            Launcher.frmMain?.RemoveResizer();
        else
            Launcher.frmMain?.AddResizer();
    }

    public static void UiAutoPauseVideo(bool value)
    {
        VideoBackground.ForcePlay = !value;

        if (!value)
            VideoBackground.VideoPlay();
        else if (VideoBackground.IsGaming)
            VideoBackground.VideoPause();
    }

    public static void UiBackgroundOpacity(int value)
    {
        Launcher.frmMain?.ImgBack.Opacity = value / 1000d;
    }

    public static void UiBackgroundBlur(int value)
    {
        var background = Launcher.frmMain?.ImgBack;
        if (background is null)
            return;

        background.Effect = value == 0
            ? null
            : new BlurEffect
            {
                Radius = value + 1
            };

        background.Margin = new Thickness(-(value + 1) / 1.8d);
    }

    /// <summary>
    ///     设置背景图片的布局方式。
    /// </summary>
    public static void UiBackgroundSuit(int value)
    {
        var background = Launcher.frmMain?.ImgBack;
        if (background?.Background is null)
            return;

        var brush = (ImageBrush)background.Background;
        var width = brush.ImageSource.Width;
        var height = brush.ImageSource.Height;

        if (value == 0)
        {
            var main = Launcher.frmMain?.PanMain;
            if (main is null)
                return;
            value = width < main.ActualWidth / 2d && height < main.ActualHeight / 2d ? 4 : 2;
        }

        brush.TileMode = TileMode.None;
        brush.Viewport = new Rect(0d, 0d, 1d, 1d);
        brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;

        switch (value)
        {
            case 1 or 5 or 6 or 7 or 8:
                background.HorizontalAlignment = value switch
                {
                    1 => HorizontalAlignment.Center,
                    5 or 7 => HorizontalAlignment.Left,
                    _ => HorizontalAlignment.Right
                };

                background.VerticalAlignment = value switch
                {
                    1 => VerticalAlignment.Center,
                    5 or 6 => VerticalAlignment.Top,
                    _ => VerticalAlignment.Bottom
                };

                brush.Stretch = Stretch.None;
                background.Width = width;
                background.Height = height;
                break;

            case 2 or 3 or 4:
                background.HorizontalAlignment = HorizontalAlignment.Stretch;
                background.VerticalAlignment = VerticalAlignment.Stretch;
                background.Width = double.NaN;
                background.Height = double.NaN;

                brush.Stretch = value switch
                {
                    2 => Stretch.UniformToFill,
                    3 => Stretch.Fill,
                    _ => Stretch.None
                };

                if (value == 4)
                {
                    brush.TileMode = TileMode.Tile;
                    brush.Viewport = new Rect(0d, 0d, width, height);
                    brush.ViewportUnits = BrushMappingMode.Absolute;
                }

                break;
        }
    }

    public static void UiFont(string value)
    {
        try
        {
            Common.SetLaunchFont(value);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "字体加载失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     设置自定义主页类型。
    ///     0：无，1：本地，2：联网，3：预设。
    /// </summary>
    public static void UiCustomType(int value)
    {
        var ui = Launcher.frmSetupUi;
        if (ui is null)
            return;

        if (value is >= 0 and <= 3)
        {
            ui.PanCustomPreset.Visibility = ToVisibility(value == 3);
            ui.PanCustomLocal.Visibility = ToVisibility(value == 1);
            ui.PanCustomNet.Visibility = ToVisibility(value == 2);

            var showHint = value is 1 or 2;
            ui.HintCustom.Visibility = ToVisibility(showHint);
            ui.HintCustomWarn.Visibility = ToVisibility(
                showHint && !States.Hint.UntrustedHomepage);

            if (showHint)
            {
                var hintKey = value == 1
                    ? "Setup.Ui.Homepage.LocalFile.Hint"
                    : "Setup.Ui.Homepage.NetUpdate.Hint";

                ui.HintCustom.TrySetLocalizedValue("Text", hintKey);

                CustomEventService.SetEventType(
                    ui.HintCustom,
                    value == 1 ? EventType.None : EventType.OpenUrl);

                if (value == 2)
                    CustomEventService.SetEventData(
                        ui.HintCustom,
                        "https://github.com/PCL-Community/PCL-CE/discussions");
            }
        }

        ui.CardCustom.TriggerForceResize();
    }

    public static void UiBlur(bool value)
    {
        var ui = Launcher.frmSetupUi;
        if (ui is null)
            return;

        ui.PanBlurValue.Visibility = ToVisibility(value);
        UiBlurValue(value ? Config.Preference.Blur.Radius : 0);
    }

    public static void UiBlurValue(int value)
    {
        System.Windows.Application.Current.Resources["BlurRadius"] = (double)value;
    }

    public static void UiBlurSamplingRate(int value)
    {
        System.Windows.Application.Current.Resources["BlurSamplingRate"] = value * 0.01d;
    }

    public static void UiBlurType(int value)
    {
        System.Windows.Application.Current.Resources["BlurType"] = (KernelType)value;
    }

    /// <summary>
    ///     设置标题栏 Logo 类型。
    ///     0：无，1：默认，2：文本，3：图片，4：HMCL。
    /// </summary>
    public static void UiLogoType(int value)
    {
        if (ThemeService.CurrentTheme == ColorTheme.HmclBlue)
            value = 4;

        var main = Launcher.frmMain;
        var ui = Launcher.frmSetupUi;
        if (main is null)
            return;

        if (value is >= 0 and <= 4)
        {
            main.ShapeTitleLogo.Visibility = ToVisibility(value == 1);
            main.ShapeHMCLTitleLogo.Visibility = ToVisibility(value == 4);
            main.BtnTitleHelp.Visibility = ToVisibility(value == 4);
            main.LabTitleLogo.Visibility = ToVisibility(value == 2);
            main.ImageTitleLogo.Visibility = ToVisibility(value == 3);

            if (value != 4)
                main.CELogo.Visibility = ToVisibility(value != 0);

            if (ui is not null)
            {
                ui.CheckLogoLeft.Visibility = ToVisibility(value == 0);
                ui.PanLogoText.Visibility = ToVisibility(value == 2);
                ui.PanLogoChange.Visibility = ToVisibility(value == 3);
            }

            switch (value)
            {
                case 2:
                    _ = Config.Preference.WindowTitleCustomText;
                    break;

                case 3:
                    try
                    {
                        main.ImageTitleLogo.Source =
                            Common.ExePath + @"PCL\Logo.png";
                    }
                    catch (Exception ex)
                    {
                        main.ImageTitleLogo.Source = null;
                        Common.Log(
                            ex,
                            "显示标题栏图片失败",
                            Common.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Error.OperationFailed"));
                    }

                    break;
            }
        }

        UiLogoLeft(Config.Preference.TopBarLeftAlign);
        ui?.CardLogo.TriggerForceResize();
    }

    public static void UiLogoText(string value)
    {
        Launcher.frmMain?.LabTitleLogo.Text = value;
    }

    public static void UiLogoLeft(bool value)
    {
        var width = value && Config.Preference.WindowTitleType == LauncherTitleType.None
            ? 0d
            : 1d;

        Launcher.frmMain?.PanTitleMain.ColumnDefinitions[0].Width =
            new GridLength(width, GridUnitType.Star);
    }
}
