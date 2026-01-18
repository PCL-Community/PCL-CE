using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL.Core.UI.Controls;

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.Windows.Media.Imaging;

public partial class MotdRenderer {
    // Default Color for originalColorMap: #808080
    // Minecraft color code mapping
    private static readonly Dictionary<string, Brush> ColorMapWithBlackBackground = new() {
        { "0", Brushes.Black }, // Black
        { "1", new SolidColorBrush(Color.FromRgb(0, 0, 170)) }, // Dark Blue
        { "2", new SolidColorBrush(Color.FromRgb(0, 170, 0)) }, // Dark Green
        { "3", new SolidColorBrush(Color.FromRgb(0, 170, 170)) }, // Cyan
        { "4", new SolidColorBrush(Color.FromRgb(170, 0, 0)) }, // Dark Red
        { "5", new SolidColorBrush(Color.FromRgb(170, 0, 170)) }, // Purple
        { "6", new SolidColorBrush(Color.FromRgb(255, 170, 0)) }, // Gold
        { "7", Brushes.LightGray }, // Gray
        { "8", Brushes.DarkGray }, // Dark Gray
        { "9", Brushes.Blue }, // Blue
        { "a", Brushes.Lime }, // Green
        { "b", Brushes.Cyan }, // Cyan
        { "c", Brushes.Red }, // Red
        { "d", Brushes.Magenta }, // Magenta
        { "e", Brushes.Yellow }, // Yellow
        { "f", Brushes.White } // White
    };

    // Color code mapping optimized for white background (#f3f6fa)
    private static readonly Dictionary<string, Brush> ColorMapWithWhiteBackground = new() {
        { "0", new SolidColorBrush(Color.FromRgb(51, 51, 51)) }, // Deep Gray #333333
        { "1", new SolidColorBrush(Color.FromRgb(0, 48, 135)) }, // Navy Blue #003087
        { "2", new SolidColorBrush(Color.FromRgb(0, 128, 0)) }, // Forest Green #008000
        { "3", new SolidColorBrush(Color.FromRgb(0, 122, 122)) }, // Cyan #007A7A
        { "4", new SolidColorBrush(Color.FromRgb(161, 0, 0)) }, // Deep Red #A10000
        { "5", new SolidColorBrush(Color.FromRgb(128, 0, 128)) }, // Deep Purple #800080
        { "6", new SolidColorBrush(Color.FromRgb(204, 112, 0)) }, // Deep Orange #CC7000
        { "7", new SolidColorBrush(Color.FromRgb(102, 102, 102)) }, // Medium Gray #666666
        { "8", new SolidColorBrush(Color.FromRgb(68, 68, 68)) }, // Charcoal #444444
        { "9", new SolidColorBrush(Color.FromRgb(0, 68, 204)) }, // Royal Blue #0044CC
        { "a", new SolidColorBrush(Color.FromRgb(0, 153, 0)) }, // Green #009900
        { "b", new SolidColorBrush(Color.FromRgb(0, 161, 161)) }, // Cyan #00A1A1
        { "c", new SolidColorBrush(Color.FromRgb(204, 0, 0)) }, // Red #CC0000
        { "d", new SolidColorBrush(Color.FromRgb(194, 0, 194)) }, // Magenta #C200C2
        { "e", new SolidColorBrush(Color.FromRgb(179, 160, 0)) }, // Deep Yellow #B3A000
        { "f", new SolidColorBrush(Color.FromRgb(136, 136, 136)) } // White
    };

    // Format code mapping
    private readonly Dictionary<string, bool> _formatMap = new() {
        { "l", true }, // Bold
        { "o", true }, // Italic
        { "n", true }, // Underline
        { "m", true }, // Strikethrough
        { "k", true }, // Obfuscated (not supported)
        { "r", false } // Reset
    };

    // Store TextBlock and original text for §k obfuscated text
    private readonly List<(TextBlock TextBlock, string OriginalText)> _obfuscatedTextBlocks = [];

    private readonly Random _random = new();
    private const string RandomChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()";
    private readonly Color _backgroundColor = Color.FromRgb(243, 246, 250); // #f3f6fa

    public MotdRenderer() {
        InitializeComponent(); // 初始化 XAML 定义的控件
        // Start timer to update §k text
        var timer = new DispatcherTimer {
            Interval = TimeSpan.FromMilliseconds(20)
        };
        timer.Tick += _UpdateObfuscatedText;
        timer.Start();
    }

    private void _UpdateObfuscatedText(object? sender, EventArgs e) {
        foreach (var (textBlock, originalText) in _obfuscatedTextBlocks) {
            // Generate random characters of the same length as the original text
            var obfuscated = string.Join("",
                Enumerable.Range(0, originalText.Length).Select(_ => RandomChars[_random.Next(RandomChars.Length)]));
            textBlock.Text = obfuscated;
        }
    }

    private static double _GetRelativeLuminance(Color color) {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var rL = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        var gL = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        var bL = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        return 0.2126 * rL + 0.7152 * gL + 0.0722 * bL;
    }

    private static double _GetContrastRatio(Color foreground, Color background) {
        var l1 = _GetRelativeLuminance(foreground);
        var l2 = _GetRelativeLuminance(background);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private Color _AdjustColorForContrast(Color inputColor) {
        var contrastRatio = _GetContrastRatio(inputColor, _backgroundColor);
        if (contrastRatio >= 4.5) return inputColor; // Contrast is sufficient

        // Convert RGB to HSL
        var r = inputColor.R / 255.0;
        var g = inputColor.G / 255.0;
        var b = inputColor.B / 255.0;
        var max = Math.Max(Math.Max(r, g), b);
        var min = Math.Min(Math.Min(r, g), b);
        var l = (max + min) / 2.0;
        double s;
        double h;

        if (Math.Abs(max - min) < double.Epsilon) {
            h = 0.0;
            s = 0.0;
        } else {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            h = max switch {
                _ when Math.Abs(max - r) < double.Epsilon => (g - b) / d + (g < b ? 6.0 : 0.0),
                _ when Math.Abs(max - g) < double.Epsilon => (b - r) / d + 2.0,
                _ => (r - g) / d + 4.0
            };
            h /= 6.0;
        }

        // Decrease lightness until contrast ratio ≥ 4.5:1
        var newL = l;
        var adjustedColor = inputColor;
        while (newL > 0.1 && _GetContrastRatio(adjustedColor, _backgroundColor) < 4.5) {
            newL -= 0.05; // Gradually decrease lightness
            double newR, newG, newB;
            if (s == 0) {
                newR = newL;
                newG = newL;
                newB = newL;
            } else {
                var q = newL < 0.5 ? newL * (1.0 + s) : newL + s - newL * s;
                var p = 2.0 * newL - q;
                newR = _HueToRgb(p, q, h + 1.0 / 3.0);
                newG = _HueToRgb(p, q, h);
                newB = _HueToRgb(p, q, h - 1.0 / 3.0);
            }

            adjustedColor = Color.FromRgb((byte)(newR * 255), (byte)(newG * 255), (byte)(newB * 255));
        }

        // If contrast is still insufficient, use default color #555555
        return _GetContrastRatio(adjustedColor, _backgroundColor) < 4.5 ? Color.FromRgb(85, 85, 85) : adjustedColor;
    }

    private static double _HueToRgb(double p, double q, double t) {
        if (t < 0) t += 1.0;
        if (t > 1) t -= 1.0;
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 0.5) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    public static bool TryGetColorFromCode(string code, bool isDarkMode, out String? color) {
        var colorMap = isDarkMode ? ColorMapWithBlackBackground : ColorMapWithWhiteBackground;
        var success = colorMap.TryGetValue(code.ToLower(), out var brush);
        if (!success) {
            color = null;
            return false;
        }
        var solidColorBrush = ((SolidColorBrush)brush!).Color;
        color = $"#{solidColorBrush.R:X2}{solidColorBrush.G:X2}{solidColorBrush.B:X2}";
        return success;
    }

    public void RenderMotd(string motd, bool isDarkMode = true, int maxLines = int.MaxValue, double fontSize = 12, bool isCentered = true) {
        MotdCanvas.Children.Clear();
        _obfuscatedTextBlocks.Clear();

        var colorMap = isDarkMode ? ColorMapWithBlackBackground : ColorMapWithWhiteBackground;
        var font = Config.UI.MotdFont;
        var fontFamily = new FontFamily(string.IsNullOrWhiteSpace(font)
            ? "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI"
            : font);
        var canvasWidth = MotdCanvas.ActualWidth > 0 ? MotdCanvas.ActualWidth : 300; // Prevent zero width
        var canvasHeight = MotdCanvas.ActualHeight > 0 ? MotdCanvas.ActualHeight : 34; // Prevent zero height
        double y = 10;

        // Split multi-line MOTD
        motd = motd.Replace("\n", "\r\n");
        var lines = motd.Split("\r\n");
        var currentColor = colorMap["f"];
        var isBold = false;
        var isItalic = false;
        var isUnderline = false;
        var isStrikethrough = false;
        var isObfuscated = false;

        // 限制显示的行数不超过maxLines
        var lineCount = Math.Min(lines.Length, maxLines);
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++) {
            var line = lines[lineIndex].Trim();
            var parts = RegexPatterns.MotdCode.Split(line);

            // Calculate line width
            double lineWidth = 0;
            double lineHeight = 0;
            double tempX = 0; // Temporary x-coordinate for width calculation
            var textBlocks = new List<TextBlock>(); // Store TextBlocks for the line
            var positions = new List<double>(); // Store x-coordinates for each TextBlock

            // 找出第一个和最后一个不是格式符的文本部分的索引
            var firstNonFormatPartIndex = -1;
            var lastNonFormatPartIndex = -1;
            for (var j = 0; j < parts.Length; j++)
            {
                var part = parts[j];
                if (!string.IsNullOrEmpty(part) && !(part.StartsWith('§') && part.Length == 2) && !RegexPatterns.HexColor.IsMatch(part))
                {
                    if (firstNonFormatPartIndex == -1)
                    {
                        firstNonFormatPartIndex = j;
                    }
                    lastNonFormatPartIndex = j;
                }
            }

            for (var i = 0; i < parts.Length; i++) {
                var part = parts[i];
                var partTrimmed = part;
                if (i == firstNonFormatPartIndex) {
                    partTrimmed = part.TrimStart();
                } else if (i == lastNonFormatPartIndex) {
                    partTrimmed = part.TrimEnd();
                }
                if (string.IsNullOrEmpty(partTrimmed)) continue;

                // Handle § color codes
                if (partTrimmed.StartsWith('§') && partTrimmed.Length == 2) {
                    var code = partTrimmed[1..].ToLower();
                    if (colorMap.TryGetValue(code, out var brush)) {
                        currentColor = brush;
                        isBold = false;
                        isItalic = false;
                        isUnderline = false;
                        isStrikethrough = false;
                        isObfuscated = false;
                    } else if (_formatMap.ContainsKey(code)) {
                        switch (code) {
                            case "l":
                                isBold = true;
                                break;
                            case "o":
                                isItalic = true;
                                break;
                            case "n":
                                isUnderline = true;
                                break;
                            case "m":
                                isStrikethrough = true;
                                break;
                            case "k":
                                isObfuscated = true;
                                break;
                            case "r":
                                currentColor = colorMap["f"];
                                isBold = false;
                                isItalic = false;
                                isUnderline = false;
                                isStrikethrough = false;
                                isObfuscated = false;
                                break;
                        }
                    }

                    continue;
                }

                // Handle RGB color codes
                if (RegexPatterns.HexColor.IsMatch(partTrimmed)) {
                    try {
                        var hex = partTrimmed[1..];
                        var r = Convert.ToByte(hex[..2], 16);
                        var g = Convert.ToByte(hex.Substring(2, 2), 16);
                        var b = Convert.ToByte(hex.Substring(4, 2), 16);
                        var inputColor = Color.FromRgb(r, g, b);
                        currentColor = new SolidColorBrush(_AdjustColorForContrast(inputColor));
                        isBold = false;
                        isItalic = false;
                        isUnderline = false;
                        isStrikethrough = false;
                        isObfuscated = false;
                    } catch {
                        // Invalid RGB color, keep current color
                    }

                    continue;
                }

                // Render text, always use original text for width calculation
                var displayText = partTrimmed;
                TextBlock textBlock;
                if (isObfuscated) {
                    // Generate initial random characters for §k text
                    foreach (var singleChar in partTrimmed) {
                        displayText = RandomChars[_random.Next(RandomChars.Length)].ToString();
                        textBlock = _RenderText(displayText, fontFamily, fontSize, currentColor, isBold, isItalic,
                            isUnderline, isStrikethrough, tempX, y, true,
                            _MeasureTextWidth(singleChar.ToString(), fontFamily, fontSize, isBold, isItalic));
                        _obfuscatedTextBlocks.Add((textBlock, singleChar.ToString()));
                        textBlocks.Add(textBlock);
                        positions.Add(tempX);
                        tempX += _MeasureTextWidth(singleChar.ToString(), fontFamily, fontSize, isBold, isItalic);
                    }
                } else {
                    textBlock = _RenderText(displayText, fontFamily, fontSize, currentColor, isBold, isItalic,
                        isUnderline, isStrikethrough, tempX, y);
                    textBlocks.Add(textBlock);
                    positions.Add(tempX);
                }

                // Update tempX coordinate using original text width
                if (!isObfuscated) {
                    tempX += _MeasureTextWidth(partTrimmed, fontFamily, fontSize, isBold, isItalic);
                }

                var textHeight = _MeasureTextHeight(partTrimmed, fontFamily, fontSize, isBold, isItalic);
                lineHeight = textHeight > lineHeight ? textHeight : lineHeight;
                lineWidth = tempX; // Update line width
            }

            if (isCentered) {
                // Center-align: Adjust x-coordinates for each TextBlock
                var offsetX = (canvasWidth - lineWidth) / 2;
                for (var i = 0; i < textBlocks.Count; i++) {
                    Canvas.SetLeft(textBlocks[i], positions[i] + offsetX);
                }
            }

            // 计算当前行的垂直位置
            double offsetY;
            if (lineCount == 1) {
                // 单行文本居中
                offsetY = (canvasHeight - lineHeight) / 2;
            } else {
                // 多行文本的位置计算
                var totalHeight = lineCount * lineHeight; // 使用实际显示的行数计算总高度
                var startOffset = (canvasHeight - totalHeight) / 2;
                offsetY = startOffset + (lineIndex * lineHeight);
            }

            // 设置所有文本块的垂直位置
            foreach (var textBlock in textBlocks) {
                Canvas.SetTop(textBlock, offsetY);
            }
        }
    }

    private TextBlock _RenderText(string text, FontFamily fontFamily, double fontSize, Brush color,
        bool isBold, bool isItalic, bool isUnderline, bool isStrikethrough,
        double x, double y, bool withClip = false, double clipWidth = 15) {
        var textBlock = new TextBlock {
            Text = text,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Foreground = color,
            FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = isItalic ? FontStyles.Italic : FontStyles.Normal
        };

        if (isUnderline || isStrikethrough) {
            textBlock.TextDecorations = new TextDecorationCollection();
            if (isUnderline) textBlock.TextDecorations.Add(TextDecorations.Underline);
            if (isStrikethrough) textBlock.TextDecorations.Add(TextDecorations.Strikethrough);
        }

        if (withClip) {
            var clipRect = new RectangleGeometry {
                Rect = new Rect(0, 0, clipWidth, _MeasureTextHeight(text, fontFamily, fontSize, isBold, isItalic))
            };
            textBlock.Clip = clipRect;
        }

        Canvas.SetLeft(textBlock, x);
        Canvas.SetTop(textBlock, y);
        if (Content is Canvas canvas) {
            canvas.Children.Add(textBlock);
        }

        return textBlock;
    }

    private static FormattedText _CreateFormattedText(string text, FontFamily fontFamily, double fontSize, bool isBold, bool isItalic) {
        return new FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(fontFamily, isItalic ? FontStyles.Italic : FontStyles.Normal,
                isBold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
            fontSize,
            Brushes.White,
            96);
    }

    private static double _MeasureTextWidth(string text, FontFamily fontFamily, double fontSize, bool isBold, bool isItalic) {
        return _CreateFormattedText(text, fontFamily, fontSize, isBold, isItalic).WidthIncludingTrailingWhitespace;
    }

    private static double _MeasureTextHeight(string text, FontFamily fontFamily, double fontSize, bool isBold, bool isItalic) {
        return _CreateFormattedText(text, fontFamily, fontSize, isBold, isItalic).Height;
    }

    public void RenderCanvas() {
        // Ensure Canvas is rendered
        MotdCanvas.UpdateLayout();

        // Generate static random characters for §k text
        foreach (var (textBlock, originalText) in _obfuscatedTextBlocks) {
            textBlock.Text = string.Join("",
                Enumerable.Range(0, originalText.Length).Select(_ => RandomChars[_random.Next(RandomChars.Length)]));
        }

        // Capture Canvas using RenderTargetBitmap
        var rtb = new RenderTargetBitmap(
            (int)MotdCanvas.Width, (int)MotdCanvas.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(MotdCanvas);
    }

    public void ClearCanvas() {
        MotdCanvas.Children.Clear();
        _obfuscatedTextBlocks.Clear();
    }
}
