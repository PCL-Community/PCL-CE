using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.UI.Controls;

/// <summary>
///     Minecraft MOTD（<c>§</c> 颜色/格式代码）到内联文本的转换。
///     颜色、粗体、斜体、下划线、删除线、换行与 <c>§k</c> 乱码按 Minecraft 的格式码语义处理。
///     <c>§k</c> 的每个原字符都包在固定宽度的 InlineUIContainer 中，定时替换为随机字符，保持排版稳定。
/// </summary>
internal static class MotdTextRenderer
{
    private const string RandomChars =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";

    private static readonly Random Random = new();

    private static readonly ConditionalWeakTable<TextBlock, ObfuscationState> Obfuscations = new();

    private static readonly Dictionary<char, Color> DarkBackgroundColors = new()
    {
        ['0'] = Color.FromRgb(0x00, 0x00, 0x00),
        ['1'] = Color.FromRgb(0x00, 0x00, 0xAA),
        ['2'] = Color.FromRgb(0x00, 0xAA, 0x00),
        ['3'] = Color.FromRgb(0x00, 0xAA, 0xAA),
        ['4'] = Color.FromRgb(0xAA, 0x00, 0x00),
        ['5'] = Color.FromRgb(0xAA, 0x00, 0xAA),
        ['6'] = Color.FromRgb(0xFF, 0xAA, 0x00),
        ['7'] = Color.FromRgb(0xAA, 0xAA, 0xAA),
        ['8'] = Color.FromRgb(0x55, 0x55, 0x55),
        ['9'] = Color.FromRgb(0x55, 0x55, 0xFF),
        ['a'] = Color.FromRgb(0x55, 0xFF, 0x55),
        ['b'] = Color.FromRgb(0x55, 0xFF, 0xFF),
        ['c'] = Color.FromRgb(0xFF, 0x55, 0x55),
        ['d'] = Color.FromRgb(0xFF, 0x55, 0xFF),
        ['e'] = Color.FromRgb(0xFF, 0xFF, 0x55),
        ['f'] = Color.FromRgb(0xFF, 0xFF, 0xFF)
    };

    private static readonly Dictionary<char, Color> LightBackgroundColors = new()
    {
        ['0'] = Color.FromRgb(0x33, 0x33, 0x33),
        ['1'] = Color.FromRgb(0x00, 0x30, 0x87),
        ['2'] = Color.FromRgb(0x00, 0x80, 0x00),
        ['3'] = Color.FromRgb(0x00, 0x7A, 0x7A),
        ['4'] = Color.FromRgb(0xA1, 0x00, 0x00),
        ['5'] = Color.FromRgb(0x80, 0x00, 0x80),
        ['6'] = Color.FromRgb(0xCC, 0x70, 0x00),
        ['7'] = Color.FromRgb(0x66, 0x66, 0x66),
        ['8'] = Color.FromRgb(0x44, 0x44, 0x44),
        ['9'] = Color.FromRgb(0x00, 0x44, 0xCC),
        ['a'] = Color.FromRgb(0x00, 0x99, 0x00),
        ['b'] = Color.FromRgb(0x00, 0xA1, 0xA1),
        ['c'] = Color.FromRgb(0xCC, 0x00, 0x00),
        ['d'] = Color.FromRgb(0xC2, 0x00, 0xC2),
        ['e'] = Color.FromRgb(0xB3, 0xA0, 0x00),
        ['f'] = Color.FromRgb(0x88, 0x88, 0x88)
    };

    /// <summary>
    ///     按 <paramref name="maxLines" /> 行数渲染 MOTD 到 <paramref name="target" />。
    ///     <paramref name="fontFamily" /> 为空时沿用目标控件当前字体。
    /// </summary>
    public static void Render(
        TextBlock target,
        string? motd,
        bool whiteBackground,
        int maxLines,
        double fontSize,
        FontFamily? fontFamily = null)
    {
        var obfuscation = Obfuscations.GetValue(
            target,
            static owner =>
            {
                var state = new ObfuscationState();
                owner.DetachedFromVisualTree += (_, _) => state.Stop();
                return state;
            });
        obfuscation.Stop();

        var inlines = target.Inlines!;
        inlines.Clear();
        target.FontSize = fontSize;
        target.MaxLines = maxLines;
        target.TextWrapping = TextWrapping.Wrap;
        if (fontFamily is not null)
            target.FontFamily = fontFamily;

        if (string.IsNullOrEmpty(motd))
            return;

        var colors = whiteBackground ? LightBackgroundColors : DarkBackgroundColors;
        var state = new StyleState(colors[whiteBackground ? '0' : 'f']);
        var builder = new StringBuilder();
        var typeface = new Typeface(
            target.FontFamily,
            target.FontStyle,
            target.FontWeight,
            target.FontStretch);

        for (var i = 0; i < motd.Length; i++)
        {
            var c = motd[i];
            switch (c)
            {
                case '\n':
                    Flush();
                    inlines.Add(new LineBreak());
                    continue;

                case '§' when i + 1 < motd.Length:
                {
                    var code = char.ToLowerInvariant(motd[++i]);
                    if (colors.TryGetValue(code, out var color))
                    {
                        Flush();
                        state.Color = color;
                        state.ResetFormats();
                    }
                    else if (ApplyFormat(state, code))
                    {
                        Flush();
                    }
                    else
                    {
                        builder.Append('§').Append(motd[i]);
                    }

                    continue;
                }

                default:
                    if (state.Obfuscated)
                    {
                        Flush();
                        AddObfuscatedCharacter(c);
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        Flush();
        obfuscation.Start(target);
        return;

        void Flush()
        {
            if (builder.Length == 0)
                return;

            inlines.Add(CreateRun(builder.ToString(), state));
            builder.Clear();
        }

        Run CreateRun(string text, StyleState style)
        {
            return new Run
            {
                Text = text,
                Foreground = new SolidColorBrush(style.Color),
                FontWeight = style.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = style.Italic ? FontStyle.Italic : FontStyle.Normal,
                TextDecorations = style.Underline
                    ? TextDecorations.Underline
                    : style.Strikethrough
                        ? TextDecorations.Strikethrough
                        : null
            };
        }

        void AddObfuscatedCharacter(char original)
        {
            var width = MeasureWidth(original.ToString(), typeface, fontSize, state);
            var block = new TextBlock
            {
                Text = NextRandomChar(),
                Width = Math.Max(width, fontSize * 0.25d),
                FontFamily = target.FontFamily,
                FontSize = fontSize,
                FontWeight = state.Bold ? FontWeight.Bold : FontWeight.Normal,
                FontStyle = state.Italic ? FontStyle.Italic : FontStyle.Normal,
                FontStretch = target.FontStretch,
                Foreground = new SolidColorBrush(state.Color),
                TextDecorations = state.Underline
                    ? TextDecorations.Underline
                    : state.Strikethrough
                        ? TextDecorations.Strikethrough
                        : null
            };
            inlines.Add(
                new InlineUIContainer
                {
                    Child = block
                });
            obfuscation.Entries.Add((block, original.ToString()));
        }
    }

    private static double MeasureWidth(
        string text,
        Typeface typeface,
        double fontSize,
        StyleState state)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            new SolidColorBrush(state.Color));
        return formatted.WidthIncludingTrailingWhitespace;
    }

    private static string NextRandomChar()
    {
        return RandomChars[Random.Next(RandomChars.Length)].ToString();
    }

    /// <summary>
    ///     取 Minecraft 颜色代码对应的颜色（用于延迟等单色文本）。
    /// </summary>
    public static bool TryGetColor(char code, out Color color)
    {
        return DarkBackgroundColors.TryGetValue(char.ToLowerInvariant(code), out color);
    }

    private static bool ApplyFormat(StyleState state, char code)
    {
        switch (code)
        {
            case 'l':
                state.Bold = true;
                return true;
            case 'o':
                state.Italic = true;
                return true;
            case 'n':
                state.Underline = true;
                return true;
            case 'm':
                state.Strikethrough = true;
                return true;
            case 'k':
                state.Obfuscated = true;
                return true;
            case 'r':
                state.ResetFormats();
                state.Color = state.DefaultColor;
                return true;
            default:
                return false;
        }
    }

    private sealed class StyleState(Color color)
    {
        public Color DefaultColor { get; } = color;

        public Color Color { get; set; } = color;

        public bool Bold { get; set; }

        public bool Italic { get; set; }

        public bool Underline { get; set; }

        public bool Strikethrough { get; set; }

        public bool Obfuscated { get; set; }

        public void ResetFormats()
        {
            Bold = false;
            Italic = false;
            Underline = false;
            Strikethrough = false;
            Obfuscated = false;
        }
    }

    private sealed class ObfuscationState
    {
        private DispatcherTimer? _timer;

        public List<(TextBlock Block, string Original)> Entries { get; } = [];

        public void Start(TextBlock owner)
        {
            if (Entries.Count == 0 || _timer is not null)
                return;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(20)
            };
            _timer.Tick += (_, _) =>
            {
                if (TopLevel.GetTopLevel(owner) is null)
                {
                    Stop();
                    return;
                }

                foreach (var (block, original) in Entries)
                    block.Text = string.Join(
                        "",
                        Enumerable.Range(0, original.Length).Select(_ => NextRandomChar()));
            };
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer = null;
            Entries.Clear();
        }
    }
}