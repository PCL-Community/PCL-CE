// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public partial class PageLaunchRight : MyPageRight, IRefreshable, IDisposable
{
    private const string HomepageLivePatchFileName = "CustomLive.json";
    private const string HomepageLiveSupportFileName = "CustomLive.supported.json";
    private readonly string[] _fallbackHints =
    [
        "可以把整合包放在启动器目录中，后续迁移会继续接入自动安装流程。",
        "启动页右侧的 PanCustom 会保留给主页扩展和公告内容。",
        "Avalonia 版本会优先补齐跨平台核心，再逐步恢复 WPF 的完整交互。"
    ];
    private FileSystemWatcher? _homepageLiveWatcher;
    private bool _disposed;
    private int _loadedContentHash = -1;

    public PageLaunchRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        AttachedToVisualTree += (_, _) =>
        {
            Refresh();
            EnsureHomepageLiveWatcher();
        };
        DetachedFromVisualTree += (_, _) => DisposeHomepageLiveWatcher();
    }

    public StackPanel? CustomPanel => this.FindControl<StackPanel>("PanCustom");

    public bool IsDebugLogVisible
    {
        get => this.FindControl<MyCard>("PanLog")?.IsVisible == true;
        set
        {
            if (this.FindControl<MyCard>("PanLog") is { } log)
                log.IsVisible = value;
        }
    }

    public void Refresh()
    {
        IsDebugLogVisible = false;
        RefreshTrivia();
        AppendLog("启动页已就绪。");
    }

    public void ForceRefresh()
    {
        ClearCache();
        if (PanScroll is not null)
            PanScroll.Offset = Vector.Zero;
        Refresh();
    }

    public void AddCustomContent(Control control)
    {
        CustomPanel?.Children.Add(control);
    }

    public void SetCustomContent(IEnumerable<Control> controls)
    {
        if (CustomPanel is not { } panel)
            return;

        panel.Children.Clear();
        foreach (Control control in controls)
            panel.Children.Add(control);
    }

    public void ClearCustomContent() => CustomPanel?.Children.Clear();

    public void LoadTextContent(string content)
    {
        if (CustomPanel is not { } panel)
            return;

        int hash = content.GetHashCode(StringComparison.Ordinal);
        if (hash == _loadedContentHash)
        {
            ApplyHomepageLivePatchesFromFile();
            return;
        }

        _loadedContentHash = hash;
        panel.Children.Clear();
        if (string.IsNullOrWhiteSpace(content))
            return;

        panel.Children.Add(new MyCard
        {
            Title = "自定义主页",
            Margin = new Thickness(0d, 0d, 0d, 15d),
            Children =
            {
                new TextBlock
                {
                    Text = content,
                    Margin = new Thickness(25d, 38d, 23d, 15d),
                    FontSize = 13.5d,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                }
            }
        });
        ApplyHomepageLivePatchesFromFile();
    }

    public void ClearCache()
    {
        _loadedContentHash = -1;
    }

    public void AppendLog(string message)
    {
        if (this.FindControl<TextBlock>("LabLog") is not { } log)
            return;

        string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        log.Text = string.IsNullOrEmpty(log.Text)
            ? $"[{timestamp}] {message}"
            : log.Text + Environment.NewLine + $"[{timestamp}] {message}";
    }

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        string[] lines = LoadExternalHints();
        if (lines.Length == 0)
        {
            lines =
            [
                "PCL N 的跨平台界面正在按 WPF 结构逐步迁移。",
                "启动页保留了主页扩展区，后续公告和自定义内容会继续放在这里。",
                "没有本地版本时，启动按钮会引导到下载页。"
            ];
        }

        if (enableLengthLimit)
        {
            string[] shortLines = lines.Where(line => line.Length < 50).ToArray();
            if (shortLines.Length > 0)
                lines = shortLines;
        }

        string hint = lines[Random.Shared.Next(lines.Length)];
        return raw ? hint : hint.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeHomepageLiveWatcher();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void BtnHintClose_Click(object? sender, EventArgs e)
    {
        if (this.FindControl<MyCard>("PanHint") is { } hint)
            hint.IsVisible = false;
    }

    private void BtnRefreshTrivia_Click(object? sender, EventArgs e) => RefreshTrivia();

    private void RefreshTrivia()
    {
        if (this.FindControl<TextBlock>("LabTrivia") is { } trivia)
            trivia.Text = _fallbackHints[Random.Shared.Next(_fallbackHints.Length)];
    }

    private void EnsureHomepageLiveWatcher()
    {
        if (_homepageLiveWatcher is not null)
            return;

        try
        {
            string directory = GetHomepageLiveDirectory();
            Directory.CreateDirectory(directory);
            WriteHomepageLiveSupportMarker(directory);
            _homepageLiveWatcher = new FileSystemWatcher(directory, HomepageLivePatchFileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName
            };
            _homepageLiveWatcher.Changed += (_, _) => ApplyHomepageLivePatchesFromFile();
            _homepageLiveWatcher.Created += (_, _) => ApplyHomepageLivePatchesFromFile();
            _homepageLiveWatcher.Renamed += (_, _) => ApplyHomepageLivePatchesFromFile();
            _homepageLiveWatcher.EnableRaisingEvents = true;
            ApplyHomepageLivePatchesFromFile();
        }
        catch (Exception ex)
        {
            AppendLog("主页 live patch 监听启动失败：" + ex.Message);
        }
    }

    private void DisposeHomepageLiveWatcher()
    {
        try
        {
            _homepageLiveWatcher?.Dispose();
        }
        catch (Exception ex)
        {
            AppendLog("主页 live patch 监听释放失败：" + ex.Message);
        }

        _homepageLiveWatcher = null;
        DeleteHomepageLiveSupportMarker();
    }

    private void ApplyHomepageLivePatchesFromFile()
    {
        if (CustomPanel is not { Children.Count: > 0 })
            return;

        string file = Path.Combine(GetHomepageLiveDirectory(), HomepageLivePatchFileName);
        if (!File.Exists(file))
            return;

        try
        {
            using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using JsonDocument document = JsonDocument.Parse(stream);
            foreach (JsonElement patch in EnumeratePatches(document.RootElement))
                ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            AppendLog("主页 live patch 应用失败：" + ex.Message);
        }
    }

    private static IEnumerable<JsonElement> EnumeratePatches(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement patch in root.EnumerateArray())
                yield return patch;
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("patches", out JsonElement patches) && patches.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement patch in patches.EnumerateArray())
                yield return patch;
            yield break;
        }

        if (TryGetString(root, out _, "target", "tag", "name"))
        {
            yield return root;
            yield break;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
                yield return property.Value;
        }
    }

    private void ApplyHomepageLivePatch(JsonElement patch)
    {
        if (!TryGetString(patch, out string? target, "target", "tag", "name") || string.IsNullOrWhiteSpace(target))
            return;

        foreach (Control element in FindElementsByTag(CustomPanel!, target))
            ApplyHomepageLivePatchToElement(element, patch);
    }

    private static void ApplyHomepageLivePatchToElement(Control element, JsonElement patch)
    {
        if (TryGetString(patch, out string? text, "text") && element is TextBlock textBlock)
            textBlock.Text = text;
        if (TryGetString(patch, out string? title, "title") && !string.IsNullOrEmpty(title) && element is MyCard card)
            card.Title = title;
        if (TryGetString(patch, out string? opacity, "opacity") &&
            double.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out double opacityValue))
            element.Opacity = Math.Clamp(opacityValue, 0d, 1d);
        if (TryGetString(patch, out string? isEnabled, "isEnabled") &&
            bool.TryParse(isEnabled, out bool enabledValue))
            element.IsEnabled = enabledValue;
        if (TryGetString(patch, out string? isVisible, "isVisible", "visibility"))
            element.IsVisible = !string.Equals(isVisible, "Collapsed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(isVisible, "False", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Control> FindElementsByTag(Control root, string tag)
    {
        if (string.Equals(root.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            yield return root;

        switch (root)
        {
            case Panel panel:
                foreach (Control child in panel.Children)
                {
                    foreach (Control nested in FindElementsByTag(child, tag))
                        yield return nested;
                }
                break;
            case ContentControl { Content: Control content }:
                foreach (Control nested in FindElementsByTag(content, tag))
                    yield return nested;
                break;
        }
    }

    private static bool TryGetString(JsonElement element, out string? value, params string[] names)
    {
        value = null;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        foreach (string name in names)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                value = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.ToString();
                return true;
            }
        }

        return false;
    }

    private static string[] LoadExternalHints()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "PCL", "hints.txt");
        if (!File.Exists(file))
            return [];

        try
        {
            return File.ReadAllLines(file)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim())
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string GetHomepageLiveDirectory() => Path.Combine(AppContext.BaseDirectory, "PCL");

    private static void WriteHomepageLiveSupportMarker(string directory)
    {
        string markerPath = Path.Combine(directory, HomepageLiveSupportFileName);
        string processPath = EscapeJsonString(Environment.ProcessPath ?? string.Empty);
        string startedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
        File.WriteAllText(
            markerPath,
            $$"""{"processId":{{Environment.ProcessId}},"processPath":"{{processPath}}","patchFile":"{{HomepageLivePatchFileName}}","startedAt":"{{startedAt}}"}""");
    }

    private static string EscapeJsonString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static void DeleteHomepageLiveSupportMarker()
    {
        string markerPath = Path.Combine(GetHomepageLiveDirectory(), HomepageLiveSupportFileName);
        if (File.Exists(markerPath))
            File.Delete(markerPath);
    }
}
