namespace PCL.Core.Utils.Exts;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

/// <summary>
/// 提供 WPF UI 控件的扩展方法。
/// </summary>
public static class UiExtension {
    /// <summary>
    /// 检查控件是否在指定窗口的可视区域内，且控件本身可见。
    /// </summary>
    /// <param name="element">要检查的 FrameworkElement。</param>
    /// <param name="mainWindow">主窗口，用于确定可视区域。</param>
    /// <returns>如果控件部分或完全在窗口可视区域内且可见，则返回 true；否则返回 false。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="element"/> 或 <paramref name="mainWindow"/> 为 null 时抛出。</exception>
    public static bool IsVisibleInWindow(this FrameworkElement element, Window mainWindow) {
        if (!element.IsVisible) return false;

        try {
            var transform = element.TransformToAncestor(mainWindow);
            var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            var windowRect = new Rect(0, 0, mainWindow.ActualWidth, mainWindow.ActualHeight);
            return windowRect.IntersectsWith(bounds);
        } catch (InvalidOperationException) {
            return false;
        }
    }

    /// <summary>
    /// 检查 TextBlock 是否因 TextTrimming 属性导致文本被截断。
    /// </summary>
    /// <param name="textBlock">要检查的 TextBlock。</param>
    /// <returns>如果文本被截断，则返回 true；否则返回 false。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="textBlock"/> 为 null 时抛出。</exception>
    public static bool IsTextTrimmed(this TextBlock textBlock) {
        if (textBlock.TextTrimming == TextTrimming.None) return false;

        try {
            var formattedText = new FormattedText(
                textBlock.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                textBlock.FlowDirection,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                textBlock.FontSize,
                textBlock.Foreground,
                VisualTreeHelper.GetDpi(textBlock).PixelsPerDip
                );

            return formattedText.Width > textBlock.ActualWidth;
        } catch (Exception) {
            return false;
        }
    }
}
