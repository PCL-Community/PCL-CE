using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     渲染标签气泡的公共逻辑。<br/>
///     标签既可以承载本地化资源键（按当前语言实时求值，切换语言后自动刷新），
///     也可以承载已经成型的显示文本（例如版本号、位数等不应翻译的内容）。
/// </summary>
internal static class TagPanelHelper
{
    /// <summary>
    ///     用本地化资源键填充标签面板。每个标签都绑定到
    ///     <see cref="LocalizationSource" />，因此语言变更后会自动刷新文本。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="keys">本地化资源键集合。空键会被忽略。</param>
    /// <param name="foreground">标签文本颜色。</param>
    /// <param name="backgroundAlpha">标签背景色的透明度（0-255）。</param>
    public static void FillLocalized(
        StackPanel panel,
        IEnumerable<string>? keys,
        Brush foreground,
        byte backgroundAlpha = 17)
    {
        var list = (keys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct()
            .Select(key => (Key: key, Text: Lang.Text(key)))
            .Where(item => !string.IsNullOrEmpty(item.Text))
            .OrderBy(item => item.Text, StringComparer.CurrentCulture)
            .ToList();
        panel.Children.Clear();
        panel.Visibility = list.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var item in list)
        {
            var textBlock = CreateTextBlock(
                foreground,
                new Binding
                {
                    Source = LocalizationSource.Instance,
                    Path = new PropertyPath($"[{item.Key}]"),
                    Mode = BindingMode.OneWay
                });
            panel.Children.Add(CreateBubble(textBlock, backgroundAlpha));
        }
    }

    /// <summary>
    ///     用现成的显示文本填充标签面板。该重载不做本地化，语言变更后不会改变。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="texts">已经成型的显示文本集合。</param>
    /// <param name="foreground">标签文本颜色。</param>
    /// <param name="backgroundAlpha">标签背景色的透明度（0-255）。</param>
    public static void FillPlain(
        StackPanel panel,
        IEnumerable<string>? texts,
        Brush foreground,
        byte backgroundAlpha = 17)
    {
        var list = (texts ?? [])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        panel.Children.Clear();
        panel.Visibility = list.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        foreach (var text in list)
        {
            var textBlock = CreateTextBlock(foreground, null);
            textBlock.Text = text;
            panel.Children.Add(CreateBubble(textBlock, backgroundAlpha));
        }
    }

    private static TextBlock CreateTextBlock(Brush foreground, Binding? textBinding)
    {
        var textBlock = new TextBlock
        {
            Foreground = foreground,
            FontSize = 11d
        };
        if (textBinding is not null)
            textBlock.SetBinding(TextBlock.TextProperty, textBinding);
        return textBlock;
    }

    private static Border CreateBubble(TextBlock textBlock, byte backgroundAlpha)
    {
        var bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, 0, 0, 0)),
            Padding = new Thickness(3d, 1d, 3d, 1d),
            CornerRadius = new CornerRadius(3d),
            Margin = new Thickness(0d, 0d, 3d, 0d),
            SnapsToDevicePixels = true,
            UseLayoutRounding = false,
            Child = textBlock
        };
        return bubble;
    }
}