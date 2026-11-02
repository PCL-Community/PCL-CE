using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.UI.Controls;
using PCL.UI.Seams;

namespace PCL.UI.Dialogs;

/// <summary>
///     Markdown 到控件树的渲染器。
///     支持标题、段落、粗体、斜体、删除线、行内代码、链接、有序/无序及多级嵌套列表、引用、
///     围栏代码块、分隔线、图片与表格；链接只打开 http/https 地址，其余协议忽略。
/// </summary>
internal static class MyMsgMarkdownRenderer
{
    private const double ListFontSize = DialogTheme.BodyFontSize;
    private const double TableFontSize = DialogTheme.BodyFontSize - 1d;
    private const double ImageMaxHeight = 320d;
    private const char Backtick = '\u0060';

    private static readonly FontFamily CodeFont = new(
        "Consolas, Menlo, DejaVu Sans Mono, monospace");

    private static Cursor? _handCursor;

    private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);

    private static readonly Regex UnorderedPattern = new(
        @"^(\s*)[-*+]\s+(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex OrderedPattern = new(
        @"^(\s*)(\d{1,9})[.)]\s+(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex RulePattern = new(
        @"^\s*(?:-{3,}|\*{3,}|_{3,})\s*$",
        RegexOptions.Compiled);

    private static readonly Regex FencePattern = new(
        @"^\s*\u0060\u0060\u0060",
        RegexOptions.Compiled);

    private static readonly Regex QuotePattern = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);

    private static readonly Regex ImagePattern = new(
        @"!\[(?<alt>[^\]]*)\]\(\s*(?<src><[^>]*>|[^)\s]+)(?:\s+""(?<title>[^""]*)""\s*)?\)",
        RegexOptions.Compiled);

    private static readonly Regex TableSeparatorPattern = new(
        @"^\s*\|?\s*:?-{1,}:?\s*(?:\|\s*:?-{1,}:?\s*)+\|?\s*$",
        RegexOptions.Compiled);

    /// <summary>把 Markdown 正文渲染为可放入滚动区的控件树。</summary>
    public static Control Render(string? markdown)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 6d,
            VerticalAlignment = VerticalAlignment.Top
        };
        if (string.IsNullOrEmpty(markdown)) return panel;

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (FencePattern.IsMatch(line))
            {
                panel.Children.Add(RenderCodeBlock(lines, ref index));
                continue;
            }

            if (TryRenderHeading(line, out var heading))
            {
                panel.Children.Add(heading);
                index++;
                continue;
            }

            if (IsTableStart(lines, index))
            {
                panel.Children.Add(RenderTable(lines, ref index));
                continue;
            }

            if (RulePattern.IsMatch(line))
            {
                panel.Children.Add(RenderRule());
                index++;
                continue;
            }

            if (QuotePattern.IsMatch(line))
            {
                panel.Children.Add(RenderQuote(lines, ref index));
                continue;
            }

            if (UnorderedPattern.IsMatch(line) || OrderedPattern.IsMatch(line))
            {
                panel.Children.Add(RenderList(lines, ref index));
                continue;
            }

            panel.Children.Add(RenderParagraph(lines, ref index));
        }

        return panel;
    }

    private static bool IsBlockStart(string[] lines, int index)
    {
        var line = lines[index];
        return HeadingPattern.IsMatch(line)
            || FencePattern.IsMatch(line)
            || RulePattern.IsMatch(line)
            || QuotePattern.IsMatch(line)
            || UnorderedPattern.IsMatch(line)
            || OrderedPattern.IsMatch(line)
            || IsTableStart(lines, index);
    }

    private static Control RenderCodeBlock(string[] lines, ref int index)
    {
        var builder = new StringBuilder();
        index++;
        while (index < lines.Length && !FencePattern.IsMatch(lines[index]))
        {
            builder.AppendLine(lines[index]);
            index++;
        }

        if (index < lines.Length) index++;

        var code = new SelectableTextBlock
        {
            Text = builder.ToString().TrimEnd('\n'),
            FontFamily = CodeFont,
            FontSize = 13d,
            LineHeight = 18d,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        DialogTheme.BindBrush(code, TextBlock.ForegroundProperty, "PageBrushText");

        var border = new Border
        {
            CornerRadius = new CornerRadius(4d),
            Padding = new Thickness(8d, 6d),
            Child = code
        };
        DialogTheme.BindBrush(border, Border.BackgroundProperty, "PageBrushItemHover");
        return border;
    }

    private static bool TryRenderHeading(string line, out Control control)
    {
        var match = HeadingPattern.Match(line);
        if (!match.Success)
        {
            control = null!;
            return false;
        }

        var level = match.Groups[1].Value.Length;
        var text = match.Groups[2].Value.Trim().TrimEnd('#').TrimEnd();
        text = ImagePattern.Replace(text, image => FormatImageAlt(image.Groups["alt"].Value));
        var block = CreateTextBlock(HeadingFontSize(level), FontWeight.Bold);
        block.Margin = new Thickness(0d, level <= 2 ? 4d : 2d, 0d, 0d);
        AddRichText(block, text, block.FontSize, true);
        control = block;
        return true;
    }

    private static Control RenderParagraph(string[] lines, ref int index)
    {
        var builder = new StringBuilder();
        while (index < lines.Length
               && !string.IsNullOrWhiteSpace(lines[index])
               && !IsBlockStart(lines, index))
        {
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(lines[index].Trim());
            index++;
        }

        var text = string.IsNullOrEmpty(builder.ToString()) ? " " : builder.ToString();
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4d,
            VerticalAlignment = VerticalAlignment.Top
        };
        AddRichTextWithImages(panel, text, DialogTheme.BodyFontSize, FontWeight.Normal);
        return panel;
    }

    private static Control RenderQuote(string[] lines, ref int index)
    {
        var builder = new StringBuilder();
        while (index < lines.Length)
        {
            var match = QuotePattern.Match(lines[index]);
            if (!match.Success) break;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(match.Groups[1].Value);
            index++;
        }

        var content = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 4d
        };
        AddRichTextWithImages(
            content,
            builder.ToString(),
            DialogTheme.BodyFontSize,
            FontWeight.Normal);

        var bar = new Border
        {
            BorderThickness = new Thickness(3d, 0d, 0d, 0d),
            Padding = new Thickness(10d, 2d, 0d, 2d),
            Child = content
        };
        DialogTheme.BindBrush(bar, Border.BorderBrushProperty, "PageBrushDivider");
        return bar;
    }

    private static Control RenderRule()
    {
        var line = new Border
        {
            Height = 1d,
            Margin = new Thickness(0d, 4d)
        };
        DialogTheme.BindBrush(line, Border.BackgroundProperty, "PageBrushDivider");
        return line;
    }

    // =======================================
    // 列表
    // =======================================

    private static Control RenderList(string[] lines, ref int index)
    {
        var roots = new List<ListEntry>();
        var stack = new List<ListEntry>();
        bool? rootOrdered = null;

        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                if (index + 1 < lines.Length
                    && TryParseListItem(lines[index + 1], out _, out _, out _, out _))
                {
                    index++;
                    continue;
                }

                break;
            }

            if (!TryParseListItem(
                    line,
                    out var indent,
                    out var ordered,
                    out var marker,
                    out var content))
            {
                // 缩进更深的普通行视为上一项的续行
                if (stack.Count > 0
                    && !IsBlockStart(lines, index)
                    && MeasureIndent(line) > stack[^1].Indent)
                {
                    stack[^1].Content = stack[^1].Content + " " + line.Trim();
                    index++;
                    continue;
                }

                break;
            }

            while (stack.Count > 0 && stack[^1].Indent >= indent) stack.RemoveAt(stack.Count - 1);

            if (stack.Count == 0)
            {
                rootOrdered ??= ordered;
                // 同级上无序与有序列表互相切换时断开，交给后续块重新渲染
                if (rootOrdered != ordered) break;
            }

            var entry = new ListEntry(marker, content, indent);
            if (stack.Count == 0) roots.Add(entry);
            else stack[^1].Children.Add(entry);
            stack.Add(entry);
            index++;
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d
        };
        AddListEntries(panel, roots);
        return panel;
    }

    private static void AddListEntries(StackPanel target, IReadOnlyList<ListEntry> entries)
    {
        foreach (var entry in entries)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                VerticalAlignment = VerticalAlignment.Top
            };

            var bullet = new TextBlock
            {
                Text = entry.Marker,
                FontSize = ListFontSize,
                LineHeight = Math.Max(ListFontSize + 5d, DialogTheme.BodyLineHeight),
                Margin = new Thickness(0d, 0d, 6d, 0d),
                VerticalAlignment = VerticalAlignment.Top
            };
            DialogTheme.BindBrush(bullet, TextBlock.ForegroundProperty, "PageBrushTextSecondary");

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2d
            };
            AddRichTextWithImages(content, entry.Content, ListFontSize, FontWeight.Normal);

            if (entry.Children.Count > 0)
            {
                var nested = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 2d,
                    Margin = new Thickness(0d, 1d, 0d, 0d)
                };
                AddListEntries(nested, entry.Children);
                content.Children.Add(nested);
            }

            Grid.SetColumn(bullet, 0);
            Grid.SetColumn(content, 1);
            row.Children.Add(bullet);
            row.Children.Add(content);
            target.Children.Add(row);
        }
    }

    private static bool TryParseListItem(
        string line,
        out int indent,
        out bool ordered,
        out string marker,
        out string content)
    {
        var unordered = UnorderedPattern.Match(line);
        if (unordered.Success)
        {
            indent = MeasureIndent(line);
            ordered = false;
            marker = "•";
            content = unordered.Groups[2].Value;
            return true;
        }

        var orderedMatch = OrderedPattern.Match(line);
        if (orderedMatch.Success)
        {
            indent = MeasureIndent(line);
            ordered = true;
            marker = orderedMatch.Groups[2].Value + ".";
            content = orderedMatch.Groups[3].Value;
            return true;
        }

        indent = 0;
        ordered = false;
        marker = string.Empty;
        content = string.Empty;
        return false;
    }

    private static int MeasureIndent(string line)
    {
        var width = 0;
        foreach (var current in line)
            if (current == ' ') width++;
            else if (current == '\t') width += 4;
            else break;

        return width;
    }

    // =======================================
    // 表格
    // =======================================

    private static bool IsTableStart(string[] lines, int index)
    {
        if (index + 1 >= lines.Length) return false;
        if (!lines[index].Contains('|')) return false;
        if (!TableSeparatorPattern.IsMatch(lines[index + 1])) return false;
        return SplitTableCells(lines[index]).Count == SplitTableCells(lines[index + 1]).Count;
    }

    private static Control RenderTable(string[] lines, ref int index)
    {
        var header = SplitTableCells(lines[index]);
        var separators = SplitTableCells(lines[index + 1]);
        var columnCount = Math.Min(header.Count, separators.Count);
        var alignments = new TextAlignment[columnCount];
        for (var column = 0; column < columnCount; column++)
            alignments[column] = ParseAlignment(separators[column]);

        index += 2;

        var rows = new List<string[]>();
        while (index < lines.Length)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line) || !line.Contains('|')) break;
            if (IsBlockStart(lines, index)) break;
            rows.Add(SplitTableCells(line).ToArray());
            index++;
        }

        var weights = ComputeColumnWeights(header, rows, columnCount);
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top
        };
        for (var column = 0; column < columnCount; column++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(weights[column], GridUnitType.Star));

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var row = 0; row < rows.Count; row++)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var column = 0; column < columnCount; column++)
            AddTableCell(grid, header[column], 0, column, true, alignments[column]);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        for (var column = 0; column < columnCount; column++)
            AddTableCell(
                grid,
                column < rows[rowIndex].Length ? rows[rowIndex][column] : string.Empty,
                rowIndex + 1,
                column,
                false,
                alignments[column]);

        return grid;
    }

    private static void AddTableCell(
        Grid grid,
        string text,
        int row,
        int column,
        bool header,
        TextAlignment alignment)
    {
        var cell = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d
        };
        AddRichTextWithImages(
            cell,
            text,
            TableFontSize,
            header ? FontWeight.Bold : FontWeight.Normal);
        foreach (var child in cell.Children)
            if (child is TextBlock block)
                block.TextAlignment = alignment;

        var border = new Border
        {
            Padding = new Thickness(7d, 5d, 7d, 5d),
            BorderThickness = new Thickness(0d, 0d, 0d, 1d),
            Child = cell,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        DialogTheme.BindBrush(border, Border.BorderBrushProperty, "PageBrushDivider");
        if (header) DialogTheme.BindBrush(border, Border.BackgroundProperty, "PageBrushItemHover");
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        grid.Children.Add(border);
    }

    private static List<string> SplitTableCells(string line)
    {
        var text = line.Trim();
        if (text.StartsWith('|')) text = text[1..];
        if (text.EndsWith('|')) text = text[..^1];

        var cells = new List<string>();
        var builder = new StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '\\' && index + 1 < text.Length && text[index + 1] == '|')
            {
                builder.Append('|');
                index++;
                continue;
            }

            if (current == '|')
            {
                cells.Add(builder.ToString().Trim());
                builder.Clear();
                continue;
            }

            builder.Append(current);
        }

        cells.Add(builder.ToString().Trim());
        return cells;
    }

    private static TextAlignment ParseAlignment(string separator)
    {
        var cell = separator.Trim();
        var left = cell.StartsWith(':');
        var right = cell.EndsWith(':');
        return left && right ? TextAlignment.Center :
            right ? TextAlignment.Right : TextAlignment.Left;
    }

    private static double[] ComputeColumnWeights(
        IReadOnlyList<string> header,
        IReadOnlyList<string[]> rows,
        int columnCount)
    {
        var weights = new double[columnCount];
        for (var column = 0; column < columnCount; column++)
        {
            var longest = header[column].Length;
            longest = (
                    from row in rows
                    where column < row.Length
                    select row[column].Length)
                .Prepend(longest)
                .Max();

            weights[column] = Math.Clamp(longest, 4, 40);
        }

        return weights;
    }

    // =======================================
    // 图片
    // =======================================

    private static Control RenderImage(string alt, string source)
    {
        var image = new MarkdownImage
        {
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
            IsVisible = false
        };

        var placeholderText = new TextBlock
        {
            Text = FormatImageAlt(alt),
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        DialogTheme.BindBrush(
            placeholderText,
            TextBlock.ForegroundProperty,
            "PageBrushTextSecondary");

        var placeholder = new Border
        {
            Padding = new Thickness(10d, 8d),
            CornerRadius = new CornerRadius(4d),
            MinWidth = 140d,
            Child = placeholderText
        };
        DialogTheme.BindBrush(placeholder, Border.BackgroundProperty, "PageBrushItemHover");
        ToolTip.SetTip(
            placeholder,
            string.IsNullOrWhiteSpace(alt) ? source : alt.Trim() + " · " + source);

        var container = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        container.Children.Add(image);
        container.Children.Add(placeholder);

        _ = LoadImageAsync(image, placeholderText, placeholder, source, alt);
        return container;
    }

    private static async Task LoadImageAsync(
        MarkdownImage image,
        TextBlock placeholderText,
        Border placeholder,
        string source,
        string alt)
    {
        var fallback = "无法显示图片："
            + (string.IsNullOrWhiteSpace(alt) ? ShortenSource(source) : alt.Trim());
        try
        {
            var bitmap = await LoadBitmapAsync(source).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (bitmap is null)
                {
                    placeholderText.Text = fallback;
                    return;
                }

                image.Source = bitmap;
                image.IsVisible = true;
                placeholder.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "UI.Dialogs", "Markdown 图片加载失败：" + source);
            await Dispatcher.UIThread.InvokeAsync(() => placeholderText.Text = fallback);
        }
    }

    private static async Task<Bitmap?> LoadBitmapAsync(string source)
    {
        var path = source.Trim();
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            path = await MyImage.DownloadImageAsync(path).ConfigureAwait(false);
            if (string.IsNullOrEmpty(path)) return null;
        }

        return await Task.Run(() => DecodeBitmap(path)).ConfigureAwait(false);
    }

    private static Bitmap DecodeBitmap(string source)
    {
        using var stream = OpenImageStream(source);
        return new Bitmap(stream);
    }

    private static Stream OpenImageStream(string source)
    {
        if (source.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return AssetLoader.Open(new Uri(source, UriKind.Absolute));

        if (source.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            return File.OpenRead(new Uri(source, UriKind.Absolute).LocalPath);

        return File.OpenRead(source);
    }

    /// <summary>去掉图片地址两侧的尖括号，使被尖括号包裹的路径也能加载。</summary>
    private static string NormalizeImageSource(string source)
    {
        var value = source.Trim();
        if (value.Length >= 2 && value[0] == '<' && value[^1] == '>') value = value[1..^1].Trim();
        return value;
    }

    private static string FormatImageAlt(string alt)
    {
        return string.IsNullOrWhiteSpace(alt) ? "图片" : alt.Trim();
    }

    private static string ShortenSource(string source)
    {
        return source.Length <= 60 ? source : source[..60] + "…";
    }

    /// <summary>按可用宽度等比例缩放的图片控件。</summary>
    private sealed class MarkdownImage : Image
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var source = Source;
            if (source is null || source.Size.Width <= 0d || source.Size.Height <= 0d)
                return base.MeasureOverride(availableSize);

            var availableWidth = double.IsInfinity(availableSize.Width)
                ? source.Size.Width
                : Math.Max(1d, availableSize.Width);
            var scale = Math.Min(
                1d,
                Math.Min(
                    availableWidth / source.Size.Width,
                    ImageMaxHeight / source.Size.Height));
            return new Size(source.Size.Width * scale, source.Size.Height * scale);
        }
    }

    // =======================================
    // 内联文本
    // =======================================

    private static TextBlock CreateTextBlock(double fontSize, FontWeight weight)
    {
        var block = new TextBlock
        {
            FontSize = fontSize,
            FontWeight = weight,
            LineHeight = Math.Max(fontSize + 5d, DialogTheme.BodyLineHeight),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top
        };
        DialogTheme.BindBrush(block, TextBlock.ForegroundProperty, "PageBrushText");
        return block;
    }

    private static TextBlock CreateRichText(string text, double fontSize, FontWeight weight)
    {
        var block = CreateTextBlock(fontSize, weight);
        AddRichText(block, text, fontSize, weight == FontWeight.Bold);
        return block;
    }

    private static void AddRichText(
        TextBlock block,
        string text,
        double fontSize,
        bool bold)
    {
        var links = new List<LinkSpan>();
        var position = 0;
        AddInlines(block.Inlines!, text, fontSize, bold, false, false, links, ref position);
        if (links.Count > 0) AttachLinkInteraction(block, links);
    }

    /// <summary>把可能含有图片的文本按图片拆成若干文本块与图片块。</summary>
    private static void AddRichTextWithImages(
        StackPanel panel,
        string text,
        double fontSize,
        FontWeight weight)
    {
        var added = 0;
        var start = 0;
        foreach (Match match in ImagePattern.Matches(text))
        {
            var before = text[start..match.Index];
            if (!string.IsNullOrWhiteSpace(before))
            {
                panel.Children.Add(CreateRichText(before.Trim(), fontSize, weight));
                added++;
            }

            panel.Children.Add(
                RenderImage(
                    match.Groups["alt"].Value,
                    NormalizeImageSource(match.Groups["src"].Value)));
            added++;
            start = match.Index + match.Length;
        }

        var tail = text[start..];
        if (!string.IsNullOrWhiteSpace(tail))
        {
            panel.Children.Add(CreateRichText(tail.Trim(), fontSize, weight));
            added++;
        }

        if (added == 0) panel.Children.Add(CreateRichText(" ", fontSize, weight));
    }

    private static double HeadingFontSize(int level)
    {
        return level switch
        {
            1 => 21d,
            2 => 18d,
            3 => 16d,
            _ => DialogTheme.BodyFontSize
        };
    }

    private static void AddInlines(
        InlineCollection target,
        string text,
        double fontSize,
        bool bold,
        bool italic,
        bool strike,
        List<LinkSpan> links,
        ref int position)
    {
        var buffer = new StringBuilder();

        for (var index = 0; index < text.Length;)
        {
            var current = text[index];

            if (current == '\\' && index + 1 < text.Length)
            {
                buffer.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (current == Backtick)
            {
                var codeEnd = text.IndexOf(Backtick, index + 1);
                if (codeEnd > index + 1)
                {
                    var codeText = text[(index + 1)..codeEnd];
                    Flush(ref position);
                    var code = CreateRun(codeText, fontSize - 1d, false, false, false);
                    code.FontFamily = CodeFont;
                    DialogTheme.BindBrush(
                        code,
                        TextElement.BackgroundProperty,
                        "PageBrushItemHover");
                    target.Add(code);
                    position += codeText.Length;
                    index = codeEnd + 1;
                    continue;
                }
            }

            if (index + 1 < text.Length
                && (current == '*' || current == '_')
                && text[index + 1] == current)
            {
                var marker = new string(current, 2);
                var boldEnd = text.IndexOf(marker, index + 2, StringComparison.Ordinal);
                if (boldEnd > index + 1)
                {
                    Flush(ref position);
                    var span = new Bold();
                    AddInlines(
                        span.Inlines!,
                        text[(index + 2)..boldEnd],
                        fontSize,
                        true,
                        italic,
                        strike,
                        links,
                        ref position);
                    target.Add(span);
                    index = boldEnd + 2;
                    continue;
                }
            }

            if (current is '*' or '_')
            {
                var italicEnd = text.IndexOf(current, index + 1);
                if (italicEnd > index + 1)
                {
                    Flush(ref position);
                    var span = new Italic();
                    AddInlines(
                        span.Inlines!,
                        text[(index + 1)..italicEnd],
                        fontSize,
                        bold,
                        true,
                        strike,
                        links,
                        ref position);
                    target.Add(span);
                    index = italicEnd + 1;
                    continue;
                }
            }

            if (index + 1 < text.Length && current == '~' && text[index + 1] == '~')
            {
                var strikeEnd = text.IndexOf("~~", index + 2, StringComparison.Ordinal);
                if (strikeEnd > index + 1)
                {
                    Flush(ref position);
                    var span = new Span
                    {
                        TextDecorations = TextDecorations.Strikethrough
                    };
                    AddInlines(
                        span.Inlines!,
                        text[(index + 2)..strikeEnd],
                        fontSize,
                        bold,
                        italic,
                        true,
                        links,
                        ref position);
                    target.Add(span);
                    index = strikeEnd + 2;
                    continue;
                }
            }

            if (current == '[')
            {
                var labelEnd = text.IndexOf(']', index + 1);
                if (labelEnd > index + 1
                    && labelEnd + 1 < text.Length
                    && text[labelEnd + 1] == '(')
                {
                    var urlEnd = text.IndexOf(')', labelEnd + 2);
                    if (urlEnd > labelEnd + 2)
                    {
                        var label = text[(index + 1)..labelEnd];
                        var url = text[(labelEnd + 2)..urlEnd].Trim();
                        var display = string.IsNullOrEmpty(label) ? url : label;
                        Flush(ref position);
                        target.Add(CreateLink(display, fontSize, bold, italic, strike));
                        links.Add(new LinkSpan(position, display.Length, url));
                        position += display.Length;
                        index = urlEnd + 1;
                        continue;
                    }
                }
            }

            buffer.Append(current);
            index++;
        }

        Flush(ref position);
        return;

        void Flush(ref int offset)
        {
            if (buffer.Length == 0) return;
            target.Add(CreateRun(buffer.ToString(), fontSize, bold, italic, strike));
            offset += buffer.Length;
            buffer.Clear();
        }
    }

    private static Run CreateRun(
        string text,
        double fontSize,
        bool bold,
        bool italic,
        bool strike,
        string brushKey = "PageBrushText")
    {
        var run = new Run
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal
        };
        if (strike) run.TextDecorations = TextDecorations.Strikethrough;
        DialogTheme.BindBrush(run, TextElement.ForegroundProperty, brushKey);
        return run;
    }

    private static Inline CreateLink(
        string display,
        double fontSize,
        bool bold,
        bool italic,
        bool strike)
    {
        var span = new Span
        {
            TextDecorations = TextDecorations.Underline
        };
        DialogTheme.BindBrush(span, TextElement.ForegroundProperty, "PageBrushAccent");
        var run = CreateRun(display, fontSize, bold, italic, strike, "PageBrushAccent");
        if (strike) run.TextDecorations = TextDecorations.Strikethrough;
        span.Inlines!.Add(run);
        return span;
    }

    private static void AttachLinkInteraction(TextBlock block, IReadOnlyList<LinkSpan> links)
    {
        block.PointerMoved += (_, e) =>
        {
            var link = FindLink(block, links, e.GetPosition(block));
            if (link is null)
            {
                block.Cursor = null;
                ToolTip.SetTip(block, null);
                return;
            }

            block.Cursor = _handCursor ??= new Cursor(StandardCursorType.Hand);
            ToolTip.SetTip(block, link.Url);
        };

        block.PointerPressed += (_, e) =>
        {
            var link = FindLink(block, links, e.GetPosition(block));
            if (link is null) return;
            e.Handled = true;
            OpenLink(link.Url);
        };
    }

    private static LinkSpan? FindLink(TextBlock block, IReadOnlyList<LinkSpan> links, Point point)
    {
        var layout = block.TextLayout;
        if (layout is null) return null;

        var hit = layout.HitTestPoint(point);
        if (!hit.IsInside) return null;

        var index = hit.CharacterHit.FirstCharacterIndex;
        return links.FirstOrDefault(link =>
            index >= link.Start && index < link.Start + link.Length);
    }

    private static void OpenLink(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            LogWrapper.Debug("UI.Dialogs", "已忽略非 http(s) 的 Markdown 链接：" + url);
            return;
        }

        UiSeamInteraction.OpenWebsite(url);
    }

    private sealed class LinkSpan(
        int start,
        int length,
        string url)
    {
        public int Start { get; } = start;

        public int Length { get; } = length;

        public string Url { get; } = url;
    }

    private sealed class ListEntry(
        string marker,
        string content,
        int indent)
    {
        public string Marker { get; } = marker;

        public string Content { get; set; } = content;

        public int Indent { get; } = indent;

        public List<ListEntry> Children { get; } = [];
    }
}