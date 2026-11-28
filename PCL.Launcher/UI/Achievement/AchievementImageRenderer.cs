using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.IO.Net;
using PCL.Core.Logging;

namespace PCL.UI.Achievement;

/// <summary>成就卡片的本地渲染：图标 + 文本 → 位图，任意语言都能出图。</summary>
public static class AchievementImageRenderer
{
    private static readonly string[] _OnlineIconSources =
    [
        "https://blockrender.dev/render/item/{0}.png?size=256",
        "https://mcitemgallery.com/images-v2/1.21.1/{0}.png"
    ];

    private static readonly ConcurrentDictionary<string, Bitmap> _OnlineIconCache = new();

    private readonly record struct TextLine(FormattedText Text, double Offset);

    /// <summary>依次尝试本地 JAR、在线源，返回图标、「是否确认找不到」与「位图是否归调用方释放」。</summary>
    public static async Task<(Bitmap? Icon, bool NotFound, bool OwnsIcon)> LoadIconAsync(string itemId)
    {
        var (idNamespace, path) = _SplitItemId(itemId);

        // 原版贴图与在线源都只认 minecraft 命名空间；其他命名空间的物品直接标为找不到，
        // 否则会拿路径部分去查原版数据，把别的物品图标显示出来
        if (!string.Equals(idNamespace, "minecraft", StringComparison.OrdinalIgnoreCase))
            return (null, true, false);

        // 实例解析要访问实例列表，留在调用线程；读 JAR 放后台，避免慢盘卡住界面
        string? jarPath = null;
        if (!string.IsNullOrWhiteSpace(path) && !string.IsNullOrEmpty(GameFolders.mcFolderSelected))
        {
            var instance = InstanceList.McMcInstanceSelected;
            if (instance is not null) jarPath = _ResolveClientJarPath(instance);
        }

        if (jarPath is not null)
        {
            var jar = jarPath;
            var itemPath = path;
            var localBytes = await Task.Run(() => _ReadJarEntryBytes(jar, itemPath));
            var local = localBytes is null ? null : _DecodeIcon(localBytes);
            if (local is not null) return (local, false, true);
        }

        var online = await Task.Run(() => _TryLoadOnlineIcon(path));
        if (online is not null) return (online, false, false);

        // 取不到图标时返回 null，由调用方画「禁止」占位，并提示物品 ID 可能不存在
        return (null, true, false);
    }

    /// <summary>把图标与文本渲染成成就卡片位图，调用方负责释放。</summary>
    public static Bitmap Render(Bitmap? icon, bool iconNotFound, string itemId, string rawTitle,
        string rawLine1, string rawLine2, FontFamily fontFamily)
    {
        const double contentWidth = 320;
        const double minContentHeight = 64;
        const double borderWidth = 2;
        const double cornerRadius = 5;
        const double padding = 12;
        const double iconSize = 32;
        const double textSpacing = 4;
        const double titleYAdjust = -3; // 修正像素字体的基线偏移
        const double fontSize = 16;
        const int superSample = 3;

        var backgroundColor = Color.FromArgb(220, 42, 42, 42);
        var borderDarkColor = Color.FromArgb(255, 25, 25, 25);
        var borderLightColor = Color.FromArgb(255, 85, 85, 85);
        var shadowColor = Color.FromArgb(255, 10, 10, 10);
        var titleColor = Color.FromArgb(255, 0xFF, 0xFF, 0x55); // MC 成就名的标准黄
        var descColor = Color.FromArgb(255, 255, 255, 255);

        // 每个字段单独截断，避免较长的上一行把下一行挤掉
        var title = _TruncateText(_StripMinecraftCodes(rawTitle));
        var line1 = _TruncateText(_StripMinecraftCodes(rawLine1));
        var line2 = _TruncateText(_StripMinecraftCodes(rawLine2));
        var description = line1.Length == 0 ? line2
            : line2.Length == 0 ? line1
            : line1 + "\n" + line2;

        var typeface = new Typeface(fontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);

        var textX = padding + iconSize + padding;
        var maxTextWidth = contentWidth - textX - padding;

        // 每行是独立段落，方向也按行判定；Avalonia 不会把 RTL 段落自动推到右侧，
        // 所以要自己算「右边缘 − 文本宽度」的偏移
        List<TextLine> MakeLines(string text, Color color)
        {
            var lines = new List<TextLine>();
            foreach (var line in _WrapTextByWidth(text, typeface, fontSize, maxTextWidth).Split('\n'))
            {
                var flow = _GetFlowDirection(line);
                var formatted = new FormattedText(line, CultureInfo.CurrentUICulture, flow, typeface,
                    fontSize, new SolidColorBrush(color))
                {
                    MaxTextWidth = maxTextWidth,
                    Trimming = TextTrimming.None
                };
                var offset = flow == FlowDirection.RightToLeft
                    ? Math.Max(0, maxTextWidth - formatted.WidthIncludingTrailingWhitespace)
                    : 0;
                lines.Add(new TextLine(formatted, offset));
            }

            return lines;
        }

        var titleLines = MakeLines(title, titleColor);
        var titleShadowLines = MakeLines(title, shadowColor);
        var descLines = string.IsNullOrEmpty(description) ? null : MakeLines(description, descColor);
        var descShadowLines = descLines is null ? null : MakeLines(description, shadowColor);

        var titleHeight = titleLines.Sum(line => line.Text.Height);
        var descHeight = descLines?.Sum(line => line.Text.Height) ?? 0;
        var totalTextHeight = titleHeight + (descLines != null ? textSpacing + descHeight : 0);
        var contentHeight = Math.Max(minContentHeight, totalTextHeight + padding * 2);

        var totalWidth = contentWidth + borderWidth * 2;
        var totalHeight = contentHeight + borderWidth * 2;

        // 3 倍超采样：像素尺寸放大，DPI 同比放大，绘制仍按 DIP 坐标
        var target = new RenderTargetBitmap(
            new PixelSize((int)Math.Ceiling(totalWidth * superSample), (int)Math.Ceiling(totalHeight * superSample)),
            new Vector(96 * superSample, 96 * superSample));

        using (var dc = target.CreateDrawingContext())
        {
            var outerRadius = cornerRadius + borderWidth;
            dc.DrawRectangle(new SolidColorBrush(borderDarkColor), null,
                new RoundedRect(new Rect(0, 0, totalWidth, totalHeight), outerRadius));
            dc.DrawRectangle(new SolidColorBrush(borderLightColor), null,
                new RoundedRect(new Rect(1, 1, totalWidth - 2, totalHeight - 2), outerRadius - 1));
            dc.DrawRectangle(new SolidColorBrush(backgroundColor), null,
                new RoundedRect(new Rect(borderWidth, borderWidth, contentWidth, contentHeight), cornerRadius));

            var iconRect = new Rect(padding + borderWidth, (totalHeight - iconSize) / 2, iconSize, iconSize);
            if (icon is not null)
                dc.DrawImage(icon, iconRect);
            else if (iconNotFound)
                _DrawBarrierIcon(dc, iconRect.TopLeft, iconSize);
            else
                _DrawAchievementIcon(dc, iconRect.TopLeft, iconSize, itemId);

            void DrawLines(List<TextLine> lines, List<TextLine> shadows, double startY)
            {
                var y = startY;
                for (var i = 0; i < lines.Count; i++)
                {
                    var x = textX + borderWidth + lines[i].Offset;
                    dc.DrawText(shadows[i].Text, new Point(x + 1, y + 1));
                    dc.DrawText(lines[i].Text, new Point(x, y));
                    y += lines[i].Text.Height;
                }
            }

            var textStartY = (totalHeight - totalTextHeight) / 2;
            DrawLines(titleLines, titleShadowLines, textStartY + titleYAdjust);
            if (descLines is not null && descShadowLines is not null)
                DrawLines(descLines, descShadowLines, textStartY + titleHeight + textSpacing);
        }

        return target;
    }

    /// <summary>拆分命名空间前缀，缺省为 minecraft。</summary>
    private static (string Namespace, string Path) _SplitItemId(string? itemId)
    {
        var text = (itemId ?? "").Trim();
        if (text.Length > 128) text = text[..128]; // 拼接超长文本时不值得整段复制
        text = text.ToLowerInvariant();

        var colon = text.IndexOf(':');
        return colon < 0
            ? ("minecraft", text)
            : (text[..colon], text[(colon + 1)..]);
    }

    /// <summary>定位实例实际加载的客户端 JAR，规则与启动器的真实实例解析一致。</summary>
    private static string? _ResolveClientJarPath(McInstance instance)
    {
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder)) return null;

        try
        {
            // 有 jar 覆盖字段就用被指向的实例；否则沿 inheritsFrom 走到底层，
            // 现代 Forge/NeoForge（Drop ≥ 170）例外，它们自己的 JAR 才是最终产物
            var jarName = instance.JsonObject["jar"]?.ToString();
            var real = instance;
            if (instance.IsHmclFormatJson || string.IsNullOrEmpty(jarName))
            {
                if (!((instance.Info.HasForge || instance.Info.HasNeoForge) && instance.Info.Drop >= 170))
                    real = _FollowInheritance(instance, folder);
            }
            else if (jarName != instance.Name)
            {
                real = new McInstance(jarName);
            }

            if (!File.Exists(real.PathInstance + real.Name + ".json")) real = instance;

            var jar = real.PathInstance + real.Name + ".jar";
            return File.Exists(jar) ? jar : null;
        }
        catch (Exception ex)
        {
            // McInstance 惰性加载：构造只记路径，访问 JsonObject 才读盘，父版本 JSON 损坏时异常在这里
            // 用 Debug 而非 Error，否则会弹「发生意外错误」，把正常兜底变成一次报错
            LogWrapper.Debug(ex, "Tools", "解析客户端 JAR 失败：" + instance.Name);
            return null;
        }
    }

    private static McInstance _FollowInheritance(McInstance instance, string folder)
    {
        // 自引用或成环时按「目录 + 名字」去重，避免无限 new McInstance
        var current = instance;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrEmpty(current.InheritInstanceName))
        {
            if (current.InheritInstanceName == current.Name) break;
            if (!visited.Add(current.PathInstance + current.Name)) break;
            current = new McInstance(Path.Combine(folder, "versions", current.InheritInstanceName));
        }

        return current;
    }

    private static byte[]? _ReadJarEntryBytes(string jarPath, string itemId)
    {
        try
        {
            using var jar = new ZipArchive(new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            foreach (var folder in new[] { "item", "block", "items", "blocks" })
            {
                var entry = jar.GetEntry($"assets/minecraft/textures/{folder}/{itemId}.png");
                if (entry is null) continue;

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
        catch
        {
            // 找不到贴图不该让整张图生成失败，静默退回内置占位
        }

        return null;
    }

    private static Bitmap? _DecodeIcon(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private static byte[]? _DownloadBytes(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var bytes = NetworkService.GetClient()
                .GetByteArrayAsync(url, cts.Token)
                .GetAwaiter().GetResult();
            return bytes is { Length: > 0 } ? bytes : null;
        }
        catch (OperationCanceledException)
        {
            LogWrapper.Debug("Tools", "在线图标获取超时：" + url);
            return null;
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Tools", "在线图标获取失败：" + url); // 404 属于正常情况
            return null;
        }
    }

    // 与游戏内屏障图标逐像素一致的点阵，用代码描述以避免随仓库分发游戏贴图
    private static readonly string[] _BarrierArt =
    [
        "................",
        "...caaaaaaaac...",
        "..aaaaaaaaaaab..",
        ".caac......aaab.",
        ".aac......aacab.",
        ".aa......aac.ab.",
        ".aa.....aac..ab.",
        ".aa....aac...ab.",
        ".aa...aac....ab.",
        ".aa..aac.....ab.",
        ".aa.aac......ab.",
        ".aaaac......aab.",
        ".caac......aaab.",
        "..baaaaaaaaaab..",
        "...bbbbbbbbbb...",
        "................"
    ];

    /// <summary>绘制「取不到图标」的禁止标记，按 16×16 点阵逐格填充。</summary>
    /// <remarks>画笔必须就地创建：<see cref="SolidColorBrush" /> 绑定创建线程，静态复用会在后台渲染线程抛跨线程异常。</remarks>
    private static void _DrawBarrierIcon(DrawingContext dc, Point origin, double size)
    {
        var red = new SolidColorBrush(Color.FromRgb(227, 0, 0));
        var darkRed = new SolidColorBrush(Color.FromRgb(177, 0, 0));
        var edgeRed = new SolidColorBrush(Color.FromRgb(190, 1, 1));

        var cell = size / _BarrierArt.Length;
        for (var row = 0; row < _BarrierArt.Length; row++)
        for (var col = 0; col < _BarrierArt[row].Length; col++)
        {
            var brush = _BarrierArt[row][col] switch
            {
                'a' => red,
                'b' => darkRed,
                'c' => edgeRed,
                _ => null
            };
            if (brush is null) continue;
            dc.DrawRectangle(brush, null,
                new Rect(origin.X + col * cell, origin.Y + row * cell, cell, cell));
        }
    }

    private static Bitmap? _TryLoadOnlineIcon(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        if (_OnlineIconCache.TryGetValue(itemId, out var cached)) return cached;

        foreach (var template in _OnlineIconSources)
        {
            var url = string.Format(template, Uri.EscapeDataString(itemId));
            var bytes = _DownloadBytes(url);
            if (bytes is null) continue;

            var bitmap = _DecodeIcon(bytes);
            if (bitmap is null) continue;

            _OnlineIconCache[itemId] = bitmap;
            return bitmap;
        }

        return null;
    }

    /// <summary>绘制由物品 ID 取色的占位立方体。</summary>
    private static void _DrawAchievementIcon(DrawingContext dc, Point origin, double size, string itemId)
    {
        var baseColor = _PickAchievementIconColor(itemId);
        var halfWidth = size / 2;
        var quarterHeight = size / 4;

        var top = new Point(origin.X + halfWidth, origin.Y);
        var rightTop = new Point(origin.X + size, origin.Y + quarterHeight);
        var rightBottom = new Point(origin.X + size, origin.Y + size - quarterHeight);
        var bottom = new Point(origin.X + halfWidth, origin.Y + size);
        var leftBottom = new Point(origin.X, origin.Y + size - quarterHeight);
        var leftTop = new Point(origin.X, origin.Y + quarterHeight);
        var center = new Point(origin.X + halfWidth, origin.Y + halfWidth);

        var edgePen = new Pen(new SolidColorBrush(_ShadeAchievementColor(baseColor, 0.55)), 1);

        _FillAchievementPolygon(dc, [top, rightTop, center, leftTop],
            _ShadeAchievementColor(baseColor, 1.25), edgePen);
        _FillAchievementPolygon(dc, [leftTop, center, bottom, leftBottom],
            _ShadeAchievementColor(baseColor, 1.0), edgePen);
        _FillAchievementPolygon(dc, [center, rightTop, rightBottom, bottom],
            _ShadeAchievementColor(baseColor, 0.75), edgePen);
    }

    private static void _FillAchievementPolygon(DrawingContext dc, Point[] points, Color color, IPen pen)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true);
            for (var i = 1; i < points.Length; i++)
                ctx.LineTo(points[i], true);
            ctx.EndFigure(true);
        }

        dc.DrawGeometry(new SolidColorBrush(color), pen, geometry);
    }

    private static Color _ShadeAchievementColor(Color color, double factor)
    {
        return Color.FromRgb(
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }

    private static readonly Color[] _AchievementIconPalette =
    [
        Color.FromRgb(0x8B, 0x8B, 0x8B), // 石头灰
        Color.FromRgb(0x8B, 0x5A, 0x2B), // 木头棕
        Color.FromRgb(0x4C, 0x8B, 0x3C), // 草绿
        Color.FromRgb(0x4A, 0x6B, 0x9C), // 水蓝
        Color.FromRgb(0xA0, 0x3C, 0x3C), // 红石红
        Color.FromRgb(0xC8, 0xA8, 0x3C), // 金
        Color.FromRgb(0x6B, 0x4A, 0x8B), // 紫水晶
        Color.FromRgb(0x3C, 0x3C, 0x3C), // 煤炭黑
        Color.FromRgb(0xDC, 0xDC, 0xDC), // 铁白
        Color.FromRgb(0x8B, 0x3C, 0x6B), // 品红
        Color.FromRgb(0x2E, 0x7A, 0x7A), // 青绿
        Color.FromRgb(0x9C, 0x6B, 0x3C) // 铜
    ];

    private static Color _PickAchievementIconColor(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return _AchievementIconPalette[0];

        var hash = 0;
        foreach (var c in itemId)
            hash = hash * 31 + c;
        return _AchievementIconPalette[(hash & 0x7FFFFFFF) % _AchievementIconPalette.Length];
    }

    /// <summary>按像素宽度断行，优先在空格处断开。</summary>
    private static string _WrapTextByWidth(string text, Typeface typeface, double fontSize, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new StringBuilder(text.Length + 8);
        var current = "";

        // 按 Rune 遍历，按 char 会把代理项对拆开，emoji 会变成替换符
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                result.Append(current).Append('\n');
                current = "";
                continue;
            }

            var candidate = current + rune.ToString();
            if (current.Length > 0 && _MeasureTextWidth(candidate, typeface, fontSize) > maxWidth)
            {
                var breakIndex = current.LastIndexOf(' ');
                if (breakIndex > 0)
                {
                    result.Append(current[..breakIndex]).Append('\n');
                    current = current[(breakIndex + 1)..] + rune.ToString();
                }
                else
                {
                    result.Append(current).Append('\n');
                    current = rune.ToString();
                }
            }
            else
            {
                current = candidate;
            }
        }

        result.Append(current);
        return _HardSplitOverflow(result.ToString(), typeface, fontSize, maxWidth);
    }

    /// <summary>兜住没有空格的长串（URL、长 ID），保证不溢出卡片。</summary>
    private static string _HardSplitOverflow(string text, Typeface typeface, double fontSize, double maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var builder = new StringBuilder(text.Length + 8);
        foreach (var line in text.Split('\n'))
        {
            if (_MeasureTextWidth(line, typeface, fontSize) <= maxWidth)
            {
                builder.Append(line).Append('\n');
                continue;
            }

            var chunk = new StringBuilder();
            foreach (var rune in line.EnumerateRunes())
            {
                var candidate = chunk + rune.ToString();
                if (chunk.Length > 0 && _MeasureTextWidth(candidate, typeface, fontSize) > maxWidth)
                {
                    builder.Append(chunk).Append('\n');
                    chunk.Clear();
                }

                chunk.Append(rune.ToString());
            }

            builder.Append(chunk).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    /// <summary>按第一个强方向字符判断书写方向，混排时不能用「含任意 RTL 字符」。</summary>
    private static FlowDirection _GetFlowDirection(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is >= 0x0590 and <= 0x08FF
                or >= 0xFB1D and <= 0xFDFD
                or >= 0xFE70 and <= 0xFEFF)
                return FlowDirection.RightToLeft;
            if (Rune.IsLetter(rune))
                return FlowDirection.LeftToRight;
        }

        return FlowDirection.LeftToRight;
    }

    private static double _MeasureTextWidth(string text, Typeface typeface, double fontSize)
    {
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface,
            fontSize, new SolidColorBrush(Colors.Black));

        return formatted.WidthIncludingTrailingWhitespace;
    }

    /// <summary>超过 67 个 UTF-16 单元时截断并追加省略号，按 Rune 切避免拆坏 emoji。</summary>
    private static string _TruncateText(string text)
    {
        const int maxLength = 67;
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;

        var builder = new StringBuilder(maxLength + 3);
        foreach (var rune in text.EnumerateRunes())
        {
            if (builder.Length + rune.Utf16SequenceLength > maxLength) break;
            builder.Append(rune.ToString());
        }

        return builder.Append("...").ToString();
    }

    /// <summary>移除 §c、§l 这类颜色代码。</summary>
    private static string _StripMinecraftCodes(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('§'))
            return text;

        // 后续只会保留 67 个字符，剥到 96 个就够，粘贴超长文本时不必整段复制
        const int keepLength = 96;

        var builder = new StringBuilder(Math.Min(text.Length, keepLength));
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '§')
            {
                i++;
                continue;
            }

            builder.Append(text[i]);
            if (builder.Length >= keepLength) break;
        }

        return builder.ToString();
    }

    /// <summary>把标题转换成合法的默认文件名，非法字符与超长部分都会被去掉。</summary>
    public static string SanitizeFileName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "achievement";

        if (text.Length > 200)
        {
            text = text[..200];
            if (char.IsHighSurrogate(text[^1])) text = text[..^1]; // 别切在代理项对中间
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
            if (Array.IndexOf(invalid, c) < 0 && c is not ('\r' or '\n'))
                builder.Append(c);

        var name = builder.ToString().Trim().TrimEnd('.');
        if (name.Length > 50)
        {
            // 按 Rune 截断，避免切在代理项对中间留下半个 emoji
            var trimmed = new StringBuilder(50);
            foreach (var rune in name.EnumerateRunes())
            {
                if (trimmed.Length + rune.Utf16SequenceLength > 50) break;
                trimmed.Append(rune.ToString());
            }

            name = trimmed.ToString().TrimEnd();
        }
        return string.IsNullOrEmpty(name) ? "achievement" : name;
    }
}
