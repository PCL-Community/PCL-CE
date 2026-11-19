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
    private static readonly IBrush DefaultForeground = new SolidColorBrush(Color.FromRgb(134, 134, 134));
    private static readonly IBrush DefaultBackground = new SolidColorBrush(Color.FromArgb(17, 0, 0, 0));

    /// <summary>
    ///     用本地化资源键填充标签面板。每个标签绑定到对应资源键，
    ///     语言变更后由 <see cref="PCL.Core.UI.LocalizationTextSource" /> 通知刷新。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="keys">本地化资源键集合；空白键会被忽略。</param>
    /// <param name="foreground">标签文本颜色；为 null 时使用 WPF 的固定灰色。</param>
    public static void FillLocalized(
        StackPanel panel,
        IEnumerable<string>? keys,
        IBrush? foreground = null)
    {
        var list = (keys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => (Key: key, Text: Lang.Text(key)))
            .Where(item => !string.IsNullOrEmpty(item.Text))
            .ToList();

        panel.Children.Clear();
        panel.IsVisible = list.Count > 0;

        foreach (var item in list)
        {
            var label = CreateTextBlock(foreground);
            label.SetLocalizedValue(TextBlock.TextProperty, item.Key);
            panel.Children.Add(CreateBubble(label));
        }
    }

    /// <summary>
    ///     用现成的显示文本填充标签面板。文本不随语言切换变化。
    /// </summary>
    /// <param name="panel">承载标签的面板。</param>
    /// <param name="texts">已经成型的显示文本集合。</param>
    /// <param name="foreground">标签文本颜色；为 null 时使用 WPF 的固定灰色。</param>
    public static void FillPlain(
        StackPanel panel,
        IEnumerable<string>? texts,
        IBrush? foreground = null)
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
            panel.Children.Add(CreateBubble(label));
        }
    }

    private static TextBlock CreateTextBlock(IBrush? foreground)
    {
        var label = new TextBlock
        {
            FontSize = 11d,
            Foreground = foreground ?? DefaultForeground
        };
        label.Classes.Add("ListItemTagText");
        return label;
    }

    private static Border CreateBubble(TextBlock label)
    {
        var bubble = new Border
        {
            Background = DefaultBackground,
            Padding = new Thickness(3d, 1d, 3d, 1d),
            CornerRadius = new CornerRadius(3d),
            Margin = new Thickness(0d, 0d, 4d, 0d),
            Child = label
        };
        bubble.Classes.Add("ListItemTagBubble");
        return bubble;
    }
}