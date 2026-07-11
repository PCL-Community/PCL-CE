// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Launching.Views;

public partial class PageLaunchRight : MyPageRight, IRefreshable, IDisposable
{
    private const string HomepageLivePatchFileName = "CustomLive.json";
    private const string HomepageLiveSupportFileName = "CustomLive.supported.json";
    private static readonly Dictionary<string, string> HomepageLiveAllowedProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "Text",
        ["title"] = "Title",
        ["info"] = "Info",
        ["tooltip"] = "ToolTip",
        ["visibility"] = "IsVisible",
        ["isVisible"] = "IsVisible",
        ["isEnabled"] = "IsEnabled",
        ["opacity"] = "Opacity"
    };
    private FileSystemWatcher? _homepageLiveWatcher;
    private DispatcherTimer? _homepageLivePatchTimer;
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
        SetCommunityHintText();
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
                "准备好后，点击启动游戏即可进入 Minecraft。",
                "可以在版本管理中查看、修复或导出当前游戏版本。",
                "没有本地版本时，启动按钮会引导你前往下载页。"
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

    private void SetCommunityHintText()
    {
        if (this.FindControl<TextBlock>("LabHint1") is { } first)
            first.Text = "感谢使用 PCL N Edition。此版本由社区维护，并会持续同步上游许可要求。";
        if (this.FindControl<TextBlock>("LabHint2") is { } second)
            second.Text = "你可以在设置中查看相关许可与项目信息。";
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
            _homepageLiveWatcher.Changed += (_, _) => QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Created += (_, _) => QueueHomepageLivePatchApply();
            _homepageLiveWatcher.Renamed += (_, _) => QueueHomepageLivePatchApply();
            _homepageLiveWatcher.EnableRaisingEvents = true;
            QueueHomepageLivePatchApply();
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
        if (_homepageLivePatchTimer is not null)
        {
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Tick -= HomepageLivePatchTimer_Tick;
            _homepageLivePatchTimer = null;
        }
        DeleteHomepageLiveSupportMarker();
    }

    private void QueueHomepageLivePatchApply()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_homepageLiveWatcher is null || _disposed)
                return;

            _homepageLivePatchTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _homepageLivePatchTimer.Tick -= HomepageLivePatchTimer_Tick;
            _homepageLivePatchTimer.Tick += HomepageLivePatchTimer_Tick;
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Start();
        });
    }

    private void HomepageLivePatchTimer_Tick(object? sender, EventArgs e)
    {
        _homepageLivePatchTimer?.Stop();
        ApplyHomepageLivePatchesFromFile();
    }

    private void ApplyHomepageLivePatchesFromFile()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyHomepageLivePatchesFromFile);
            return;
        }

        if (CustomPanel is not { Children.Count: > 0 })
            return;

        string file = Path.Combine(GetHomepageLiveDirectory(), HomepageLivePatchFileName);
        if (!File.Exists(file))
            return;

        try
        {
            using JsonDocument document = JsonDocument.Parse(ReadHomepageLivePatchFile(file));
            foreach (HomepageLivePatch patch in EnumeratePatches(document.RootElement))
                ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            AppendLog("主页 live patch 应用失败：" + ex.Message);
        }
    }

    private static IEnumerable<HomepageLivePatch> EnumeratePatches(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement patch in root.EnumerateArray())
                if (patch.ValueKind == JsonValueKind.Object)
                    yield return new HomepageLivePatch(patch, null);
            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty("patches", out JsonElement patches) && patches.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement patch in patches.EnumerateArray())
                if (patch.ValueKind == JsonValueKind.Object)
                    yield return new HomepageLivePatch(patch, null);
            yield break;
        }

        if (TryGetString(root, out _, "target", "tag", "name"))
        {
            yield return new HomepageLivePatch(root, null);
            yield break;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
                yield return new HomepageLivePatch(property.Value, property.Name);
        }
    }

    private void ApplyHomepageLivePatch(HomepageLivePatch patch)
    {
        string? target = TryGetString(patch.Content, out string? explicitTarget, "target", "tag", "name")
            ? explicitTarget
            : patch.ImpliedTarget;
        if (string.IsNullOrWhiteSpace(target))
            return;

        foreach (Control element in FindElementsByTag(CustomPanel!, target))
            ApplyHomepageLivePatchToElement(element, patch.Content);
    }

    private static void ApplyHomepageLivePatchToElement(Control element, JsonElement patch)
    {
        SetPropertyIfPresent(element, patch, "text", "Text");
        SetPropertyIfPresent(element, patch, "title", "Title");
        SetPropertyIfPresent(element, patch, "info", "Info");
        SetPropertyIfPresent(element, patch, "tooltip", "ToolTip");
        SetPropertyIfPresent(element, patch, "toolTip", "ToolTip");
        SetPropertyIfPresent(element, patch, "visibility", "IsVisible");
        SetPropertyIfPresent(element, patch, "isVisible", "IsVisible");
        SetPropertyIfPresent(element, patch, "isEnabled", "IsEnabled");
        SetPropertyIfPresent(element, patch, "opacity", "Opacity");

        if (TryGetProperty(patch, "properties", out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in properties.EnumerateObject())
                TrySetElementProperty(element, property.Name, property.Value.ToString());
        }
    }

    private static void SetPropertyIfPresent(Control element, JsonElement patch, string jsonName, string propertyName)
    {
        if (TryGetProperty(patch, jsonName, out JsonElement value))
            TrySetElementProperty(element, propertyName, value.ToString());
    }

    private static bool TrySetElementProperty(Control element, string propertyName, string value)
    {
        if (!HomepageLiveAllowedProperties.TryGetValue(propertyName, out string? allowedPropertyName))
            return false;

        if (string.Equals(allowedPropertyName, "ToolTip", StringComparison.Ordinal))
        {
            ToolTip.SetTip(element, value);
            return true;
        }

        var property = element.GetType().GetProperty(allowedPropertyName);
        if (property is null || !property.CanWrite)
            return false;

        try
        {
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            string trimmedValue = value.Trim();
            object convertedValue;
            if (propertyType == typeof(string) || propertyType == typeof(object))
                convertedValue = value;
            else if (propertyType == typeof(bool) && string.Equals(allowedPropertyName, "IsVisible", StringComparison.Ordinal))
                convertedValue = !string.Equals(trimmedValue, "Collapsed", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(trimmedValue, "Hidden", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(trimmedValue, "False", StringComparison.OrdinalIgnoreCase);
            else if (propertyType == typeof(bool) && bool.TryParse(trimmedValue, out bool boolValue))
                convertedValue = boolValue;
            else if (propertyType == typeof(int) && int.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
                convertedValue = intValue;
            else if (propertyType == typeof(double) && double.TryParse(trimmedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double doubleValue))
                convertedValue = string.Equals(allowedPropertyName, "Opacity", StringComparison.Ordinal)
                    ? Math.Clamp(doubleValue, 0d, 1d)
                    : doubleValue;
            else if (propertyType.IsEnum && Enum.TryParse(propertyType, trimmedValue, true, out object? enumValue) && enumValue is not null)
                convertedValue = enumValue;
            else
                return false;

            property.SetValue(element, convertedValue);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
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

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private static string ReadHomepageLivePatchFile(string file)
    {
        Exception? lastException = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using StreamReader reader = new(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastException = ex;
                Thread.Sleep(50);
            }
        }

        throw lastException ?? new IOException("Unable to read custom homepage live patch file.");
    }

    private static string[] LoadExternalHints()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "PCL", "hints.txt");
        if (!File.Exists(file))
            return [];

        try
        {
            return File.ReadLines(file)
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
        using FileStream stream = new(
            markerPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4 * 1024,
            useAsync: false);
        using Utf8JsonWriter writer = new(stream);
        writer.WriteStartObject();
        writer.WriteNumber("processId", Environment.ProcessId);
        writer.WriteString("processPath", Environment.ProcessPath ?? string.Empty);
        writer.WriteString("patchFile", HomepageLivePatchFileName);
        writer.WriteString("startedAt", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteEndObject();
        writer.Flush();
    }

    private static void DeleteHomepageLiveSupportMarker()
    {
        string markerPath = Path.Combine(GetHomepageLiveDirectory(), HomepageLiveSupportFileName);
        if (!File.Exists(markerPath))
            return;

        try
        {
            using JsonDocument marker = JsonDocument.Parse(ReadHomepageLivePatchFile(markerPath));
            if (TryGetProperty(marker.RootElement, "processId", out JsonElement processId) &&
                processId.TryGetInt32(out int markerProcessId) &&
                markerProcessId == Environment.ProcessId)
            {
                File.Delete(markerPath);
            }
        }
        catch (Exception)
        {
        }
    }

    private readonly record struct HomepageLivePatch(JsonElement Content, string? ImpliedTarget);
}
