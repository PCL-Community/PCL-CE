using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Localization;

namespace PCL.UI.Controls;

/// <summary>
///     渲染标签气泡的公共逻辑。
///     本地化重载为每个标签保留对资源键的绑定，切换语言后文本自动刷新；
///     纯文本重载直接写入调用方给出的显示文本，不参与本地化。
/// </summary>
internal static class TagPanelHelper
{
    private static readonly Color TagTextColor = Color.FromRgb(134, 134, 134);

    /// <summary>
    ///     用本地化资源键填充标签面板。每个标签绑定到对应资源键，
    ///     语言变更后由 <see cref="PCL.Core.UI.LocalizationTextSource" /> 通知刷新。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="keys">本地化资源键集合；空白键会被忽略。</param>
    /// <param name="foreground">标签文本颜色。</param>
    /// <param name="backgroundAlpha">标签背景色的透明度（0-255）。</param>
    public static void FillLocalized(
        StackPanel panel,
        IEnumerable<string>? keys,
        IBrush foreground,
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
        panel.IsVisible = list.Count > 0;

        foreach (var item in list)
        {
            var label = CreateTextBlock(foreground);
            label.SetLocalizedValue(TextBlock.TextProperty, item.Key);
            panel.Children.Add(CreateBubble(label, backgroundAlpha));
        }
    }

    /// <summary>
    ///     用现成的显示文本填充标签面板。文本不随语言切换变化。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="texts">已经成型的显示文本集合。</param>
    /// <param name="foreground">标签文本颜色。</param>
    /// <param name="backgroundAlpha">标签背景色的透明度（0-255）。</param>
    public static void FillPlain(
        StackPanel panel,
        IEnumerable<string>? texts,
        IBrush foreground,
        byte backgroundAlpha = 17)
    {
        var list = (texts ?? [])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        panel.Children.Clear();
        panel.IsVisible = list.Count > 0;

        foreach (var text in list)
        {
            var label = CreateTextBlock(foreground);
            label.Text = text;
            panel.Children.Add(CreateBubble(label, backgroundAlpha));
        }
    }

    private static TextBlock CreateTextBlock(IBrush foreground)
    {
        var label = new TextBlock
        {
            // 未指定颜色时使用标签的默认灰色
            Foreground = ReferenceEquals(foreground, null)
                ? new SolidColorBrush(TagTextColor)
                : foreground,
            FontSize = 11d
        };
        label.Classes.Add("ListItemTagText");
        return label;
    }

    private static Border CreateBubble(TextBlock label, byte backgroundAlpha)
    {
        var bubble = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(backgroundAlpha, 0, 0, 0)),
            Padding = new Thickness(3d, 1d, 3d, 1d),
            CornerRadius = new CornerRadius(3d),
            Margin = new Thickness(0d, 0d, 3d, 0d),
            Child = label
        };
        bubble.Classes.Add("ListItemTagBubble");
        return bubble;
    }
}