// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Diagnostics;
using PCL.Desktop.Features.Settings.Views;
using PCL.Desktop.Localization;

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
    private int _maximumLogLines = 500;
    private bool _showDebugLog;

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
        IsDebugLogVisible = _showDebugLog;
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
        DesktopFileLog.Write(message);
        if (this.FindControl<TextBlock>("LabLog") is not { } log)
            return;

        string timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        log.Text = string.IsNullOrEmpty(log.Text)
            ? $"[{timestamp}] {message}"
            : log.Text + Environment.NewLine + $"[{timestamp}] {message}";
        if (_maximumLogLines != int.MaxValue)
        {
            string[] lines = log.Text.Split(Environment.NewLine);
            if (lines.Length > _maximumLogLines)
                log.Text = string.Join(Environment.NewLine, lines[^_maximumLogLines..]);
        }
    }

    public void SetMaximumLogLines(int maximumLogLines)
    {
        _maximumLogLines = maximumLogLines <= 0 ? 1 : maximumLogLines;
    }

    public void ConfigureDebugLog(bool isVisible)
    {
        _showDebugLog = isVisible;
        IsDebugLogVisible = isVisible;
    }

    public static string GetRandomHint(bool enableLengthLimit = false, bool raw = false)
    {
        // WPF-aligned tip pool: external PCL/hints.txt overrides built-in community tips.
        string[] lines = LoadExternalHints();
        if (lines.Length == 0)
            lines = BuiltInCommunityHints;

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

    /// <summary>
    /// Built-in tips when PCL/hints.txt is absent — tone matches classic PCL community tips.
    /// </summary>
    private static readonly string[] BuiltInCommunityHints =
    [
        "今天也要元气满满地启动 Minecraft 哦！",
        "版本设置里可以单独给某个版本指定 Java 和内存。",
        "启动前请确认账户档案已选好，否则会跳转到登录页。",
        "没有本地版本时，点启动会引导你前往下载页安装游戏。",
        "在社区页可以搜索 Mod、整合包、资源包与光影。",
        "下载社区资源不会打断当前页面，任务进度在右下角查看。",
        "游戏崩溃时，可在版本文件夹的 logs/latest.log 查看日志。",
        "实例隔离开启后，Mod 与配置会写在版本文件夹内。",
        "自定义 JVM 参数请谨慎填写，错误参数可能导致无法启动。",
        "正版登录后可在账户页查看与刷新皮肤。",
        "想快速进服？在版本设置的服务器页添加地址后一键启动。",
        "光影需要 Iris / OptiFine 等支持，否则版本页不会显示光影入口。",
        "投影（.litematic 等）需要安装 Litematica 等投影类 Mod。",
        "设置 → 个性化 可以调整主题色与窗口背景。",
        "任务管理页可以取消正在进行的下载与安装任务。",
        "感谢使用 PCL N，也欢迎向社区反馈问题与建议！"
    ];

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
        // WPF: permanent hide requires typing the PCL N developer name (MUXUE1230).
        CommunityHintHideRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when user clicks close on the N Edition notice card.</summary>
    public event EventHandler? CommunityHintHideRequested;

    private void SetCommunityHintText()
    {
        if (IsCommunityHintPermanentlyHidden())
        {
            if (this.FindControl<MyCard>("PanHint") is { } hidden)
                hidden.IsVisible = false;
            return;
        }

        // WPF PageLaunchRight: fixed N Edition notice (not random tips).
        // Optional second line from hide prompt.
        string message = AvaloniaLocalizationManager.GetText(
            "Launch.Right.CommunityHint.Message",
            "你正在使用 PCL N Edition！\n\n此版本由独立开发者维护，与官方 PCL 的开发路径与体验并不相同。\n\n若你误下载了 N 版，强烈建议改用官方 PCL 做长期使用；并将 N 版问题提交到 N 版仓库，不要反馈给官方仓库。");
        string[] parts = message.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (this.FindControl<TextBlock>("LabHint1") is { } first)
            first.Text = parts.Length > 0 ? parts[0] : message;
        if (this.FindControl<TextBlock>("LabHint2") is { } second)
        {
            if (parts.Length > 1)
                second.Text = string.Join("\n\n", parts.Skip(1));
            else
            {
                second.Text = AvaloniaLocalizationManager.GetText(
                    "Launch.Right.CommunityHint.HidePrompt",
                    "若要永久隐藏此提示，请点击右上角关闭并输入正确的 PCL N 开发者名称。");
            }
        }

        if (this.FindControl<MyCard>("PanHint") is { } card)
            card.IsVisible = true;
    }

    internal static bool IsCommunityHintPermanentlyHidden()
    {
        try
        {
            LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
            return settings.GetBooleanOption("UiHideNEditionHint", false);
        }
        catch
        {
            return false;
        }
    }

    internal static void SetCommunityHintPermanentlyHidden(bool hidden)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetBooleanOption("UiHideNEditionHint", hidden);
        LauncherSettingsPageBinder.SaveSettings(settings);
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

        try
        {
            string trimmedValue = value.Trim();
            if (string.Equals(allowedPropertyName, "IsVisible", StringComparison.Ordinal))
            {
                element.IsVisible = !string.Equals(trimmedValue, "Collapsed", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(trimmedValue, "Hidden", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(trimmedValue, "False", StringComparison.OrdinalIgnoreCase);
                return true;
            }

            if (string.Equals(allowedPropertyName, "IsEnabled", StringComparison.Ordinal) &&
                bool.TryParse(trimmedValue, out bool enabled))
            {
                element.IsEnabled = enabled;
                return true;
            }

            if (string.Equals(allowedPropertyName, "Opacity", StringComparison.Ordinal) &&
                double.TryParse(trimmedValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double opacity))
            {
                element.Opacity = Math.Clamp(opacity, 0d, 1d);
                return true;
            }

            return allowedPropertyName switch
            {
                "Text" => TrySetText(element, value),
                "Title" => TrySetTitle(element, value),
                "Info" => TrySetInfo(element, value),
                _ => false
            };
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TrySetText(Control element, string value)
    {
        switch (element)
        {
            case TextBlock textBlock:
                textBlock.Text = value;
                return true;
            case TextBox textBox:
                textBox.Text = value;
                return true;
            case MyButton button:
                button.Text = value;
                return true;
            case MyExtraTextButton extraButton:
                extraButton.Text = value;
                return true;
            case MyCheckBox checkBox:
                checkBox.Text = value;
                return true;
            default:
                return false;
        }
    }

    private static bool TrySetTitle(Control element, string value)
    {
        switch (element)
        {
            case MyCard card:
                card.Title = value;
                return true;
            case MyListItem item:
                item.Title = value;
                return true;
            default:
                return false;
        }
    }

    private static bool TrySetInfo(Control element, string value)
    {
        if (element is not MyListItem item)
            return false;
        item.Info = value;
        return true;
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

    private static string[]? _cachedHints;
    private static readonly object HintCacheGate = new();

    /// <summary>
    /// Cached once — never re-hit disk on the UI thread during ShowLaunching.
    /// </summary>
    private static string[] LoadExternalHints()
    {
        lock (HintCacheGate)
        {
            if (_cachedHints is not null)
                return _cachedHints;
        }

        string file = Path.Combine(AppContext.BaseDirectory, "PCL", "hints.txt");
        string[] loaded;
        try
        {
            loaded = File.Exists(file)
                ? File.ReadLines(file)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToArray()
                : [];
        }
        catch
        {
            loaded = [];
        }

        lock (HintCacheGate)
            _cachedHints = loaded;
        return loaded;
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
