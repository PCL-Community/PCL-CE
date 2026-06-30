using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

namespace PCL;

/// <summary>
///     视觉捕获与冻结工具。
/// </summary>
public static class VisualCapture
{
    public static ImageBrush ControlBrush(FrameworkElement ui)
    {
        var width = ui.ActualWidth;
        var height = ui.ActualHeight;

        if (width < 1d || height < 1d)
            return new ImageBrush();

        var bmp = new RenderTargetBitmap(
            (int)Math.Round(DpiUtils.GetPixelSize(width)),
            (int)Math.Round(DpiUtils.GetPixelSize(height)),
            DpiUtils.Dpi,
            DpiUtils.Dpi,
            PixelFormats.Pbgra32);
        bmp.Render(ui);

        return new ImageBrush(bmp);
    }

    public static ImageBrush ControlBrush(
        FrameworkElement ui,
        double width,
        double height,
        double left = 0d,
        double top = 0d)
    {
        ui.Measure(new Size(width, height));
        ui.Arrange(new Rect(0d, 0d, width, height));

        var bmp = new RenderTargetBitmap(
            (int)Math.Round(DpiUtils.GetPixelSize(width)),
            (int)Math.Round(DpiUtils.GetPixelSize(height)),
            DpiUtils.Dpi,
            DpiUtils.Dpi,
            PixelFormats.Default);
        bmp.Render(ui);

        if (left != 0d || top != 0d)
            ui.Arrange(new Rect(left, top, width, height));

        return new ImageBrush(bmp);
    }

    public static void ControlFreeze(Panel ui)
    {
        ui.Background = ControlBrush(ui);
        ui.Children.Clear();
    }

    public static void ControlFreeze(Border ui)
    {
        ui.Background = ControlBrush(ui);
        ui.Child = null;
    }
}