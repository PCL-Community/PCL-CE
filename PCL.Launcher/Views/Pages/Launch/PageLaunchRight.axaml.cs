using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using PCL.UI.Xaml;
using PCL.Views.Pages.Homepage;
using PCL.Views.Shell;
using PCL.Xaml;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页右栏：按 <c>Preference.Homepage</c> 配置构建主页内容，另带社区版提示与启动日志卡片。
///     主页来源分为空白、内置“你知道吗”、Minecraft 信息流与运行时 XAML 主页四类；
///     运行时内容支持旧 WPF XAML 与 AXAML 两种格式，统一走预设、本地文件、URL、
///     缓存与 CustomLive.json 实时补丁流程。
/// </summary>
public partial class PageLaunchRight : MyPageRight, IRefreshable
{
    private const string HomepageLivePatchFileName = "CustomLive.json";
    private const string HomepageLiveSupportFileName = "CustomLive.supported.json";

    private static readonly Dictionary<string, string> HomepageLiveAllowedProperties =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["text"] = "Text",
            ["title"] = "Title",
            ["info"] = "Info",
            ["tooltip"] = "ToolTip",
            ["visibility"] = "Visibility",
            ["isEnabled"] = "IsEnabled",
            ["opacity"] = "Opacity"
        };

    private readonly TextBlock _labHint1;
    private readonly TextBlock _labHint2;
    private readonly TextBlock _labLog;
    private readonly PageHomepageNewsView _newsView;
    private readonly MyScrollViewer _panBack;
    private readonly StackPanel _panCustom;
    private readonly MyCard _panHint;
    private readonly MyCard _panLog;

    private bool _communityHintDialogOpen;

    private HomepageContent? _currentContent;
    private DispatcherTimer? _homepageLivePatchTimer;
    private FileSystemWatcher? _homepageLiveWatcher;
    private bool _initialContentPending;
    private HomepageContent? _pendingBackgroundContent;
    private int _refreshVersion;

    public PageLaunchRight()
    {
        InitializeComponent();
        Current = this;

        _panBack = this.FindControl<MyScrollViewer>("PanBack")!;
        _panCustom = this.FindControl<StackPanel>("PanCustom")!;
        _panHint = this.FindControl<MyCard>("PanHint")!;
        _panLog = this.FindControl<MyCard>("PanLog")!;
        _labHint1 = this.FindControl<TextBlock>("LabHint1")!;
        _labHint2 = this.FindControl<TextBlock>("LabHint2")!;
        _labLog = this.FindControl<TextBlock>("LabLog")!;
        PanScroll = _panBack;

        _newsView = new PageHomepageNewsView();

        FileDragHandler.CustomHomepageDropped += _ => ForceRefresh();

        _panHint.IsVisible = States.Hint.CEMessage;
        LocalizedText.TryBind(
            _labHint1,
            "Text",
            () => Lang.Text("Launch.Right.CommunityHint.Message"));
        LocalizedText.TryBind(
            _labHint2,
            "Text",
            () => Lang.Text("Launch.Right.CommunityHint.HidePrompt"));
        this.FindControl<MyIconButton>("BtnHintClose")!.Click += (_, _) => HideCommunityHint();

        _panLog.IsVisible = LauncherCommon.ModeDebug;

        Loaded += (_, _) =>
        {
            LauncherCommon.ModeDebugChanged -= OnModeDebugChanged;
            LauncherCommon.ModeDebugChanged += OnModeDebugChanged;
            OnModeDebugChanged(LauncherCommon.ModeDebug);
            _panBack.ScrollToHome();
            EnsureHomepageLiveWatcher();
            // 首次进入不主动清缓存：有缓存时先显示缓存并在后台检查新版本。
            RefreshContent();
        };
        Unloaded += (_, _) =>
        {
            LauncherCommon.ModeDebugChanged -= OnModeDebugChanged;
            DisposeHomepageLiveWatcher();
        };
    }

    public static PageLaunchRight? Current { get; private set; }

    /// <inheritdoc />
    public void Refresh()
    {
        ForceRefresh();
    }

    public void AppendLaunchLog(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _labLog.Text += text;
        else
            Dispatcher.UIThread.Post(() => _labLog.Text += text);
    }

    private void OnModeDebugChanged(bool enabled)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _panLog.IsVisible = enabled;
        else
            Dispatcher.UIThread.Post(() => _panLog.IsVisible = enabled);
    }

    public void ClearLaunchLog()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _labLog.Text = "";
        else
            Dispatcher.UIThread.Post(() => _labLog.Text = "");
    }

    public override void OnNavigatedTo(object? parameter)
    {
    }

    public override void OnNavigatedFrom()
    {
    }

    public void ForceRefresh()
    {
        LogWrapper.Info("Launch", "[Page] 要求强制刷新主页");
        HomepageSource.ClearCache();
        _panBack.ScrollToHome();
        RefreshContent();
    }

    private void RefreshContent()
    {
        var version = Interlocked.Increment(ref _refreshVersion);
        _initialContentPending = true;
        _pendingBackgroundContent = null;
        EnsureHomepageLiveWatcher();

        Task.Run(() =>
        {
            HomepageContent content;
            try
            {
                content = HomepageSource.Resolve(
                    ShowHomepageLoadingHint,
                    updated => LauncherCommon.RunInUi(() =>
                    {
                        if (version != Volatile.Read(ref _refreshVersion))
                            return;
                        ApplyBackgroundUpdate(updated);
                    }));
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Launch", "加载 PCL 主页自定义信息失败");
                content = new HomepageContent
                {
                    Kind = HomepageSourceKind.Blank,
                    ErrorMessage = Lang.Text("Launch.Homepage.LoadFailed.Title")
                };
            }

            LauncherCommon.RunInUi(() =>
            {
                if (version != Volatile.Read(ref _refreshVersion))
                    return;
                _initialContentPending = false;
                try
                {
                    if (content.ErrorMessage is { Length: > 0 } error)
                    {
                        ShowHomepageError(error);
                        return;
                    }

                    if (content.IsLoading)
                    {
                        // 新下载没有可复用的缓存：按 WPF 行为先清空旧页。
                        _panCustom.Children.Clear();
                        return;
                    }

                    LoadContent(content);
                    _panBack.ScrollToHome();
                }
                finally
                {
                    // 极快返回的后台结果可能在初次内容落地前就到了，这里补一次。
                    if (_pendingBackgroundContent is { } pending)
                    {
                        _pendingBackgroundContent = null;
                        ApplyBackgroundUpdate(pending);
                    }
                }
            });
        });
    }

    private static void ShowHomepageLoadingHint()
    {
        HintService.Show(Hints.Info(Lang.Text("Launch.Homepage.Loading")));
    }

    private async void HideCommunityHint()
    {
        if (_communityHintDialogOpen)
            return;

        _communityHintDialogOpen = true;
        var closeButton = this.FindControl<MyIconButton>("BtnHintClose");
        closeButton?.IsEnabled = false;
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Launch.Right.CommunityHint.InputTitle"),
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            var input = result.InputText ?? "";
            if (string.IsNullOrWhiteSpace(input))
                return;

            input = new string(input.Where(char.IsAsciiLetter).ToArray()).ToLowerInvariant();
            if (!input.Contains("pclcommunity"))
            {
                HintService.Show(Hints.Info(Lang.Text("Launch.Right.CommunityHint.WrongInput")));
                return;
            }

            AnimationService.DisposeControl(_panHint, true);
            States.Hint.CEMessage = false;
        }
        finally
        {
            _communityHintDialogOpen = false;
            closeButton?.IsEnabled = true;
        }
    }

    #region 主页内容

    private void ApplyBackgroundUpdate(HomepageContent content)
    {
        // 初次解析（通常是缓存）尚未落到界面时，暂存后台结果，等初次内容落地后再应用。
        if (_initialContentPending)
        {
            _pendingBackgroundContent = content;
            return;
        }

        if (content.ErrorMessage is { Length: > 0 } error)
        {
            // 有缓存/旧页时保留最后一次成功内容，只在完全没有内容时显示错误卡。
            if (_panCustom.Children.Count > 0)
            {
                LogWrapper.Info("Launch", $"[Page] 主页后台刷新失败，保留最后一次成功内容：{error}");
                HintService.Show(Hints.Error(error));
                return;
            }

            ShowHomepageError(error);
            return;
        }

        LoadContent(content);
    }

    private void LoadContent(HomepageContent content)
    {
        _currentContent = content;
        switch (content.Kind)
        {
            case HomepageSourceKind.Blank:
                LogWrapper.Info("Launch", "[Page] 实例化：清空主页 UI，来源为空");
                _panCustom.Children.Clear();
                break;

            case HomepageSourceKind.Removed:
                DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Title"),
                        Content = Lang.Text("Launch.Homepage.Preset.EchoCave.Removed"),
                        Buttons = DialogButtons.Ok,
                        ForceWait = true
                    });
                break;

            case HomepageSourceKind.Trivia:
                _panCustom.Children.Clear();
                _panCustom.Children.Add(
                    HomepageCardRenderer.CreateTrivia(
                        content.TriviaText ?? "",
                        ForceRefresh,
                        key => FindResource(key) as IBrush));
                break;

            case HomepageSourceKind.NewsFeed:
                _panCustom.Children.Clear();
                _panCustom.Children.Add(_newsView);
                _newsView.Reload();
                break;

            case HomepageSourceKind.CustomXaml:
            default:
                LoadCustomXaml(content);
                break;
        }
    }

    private void LoadCustomXaml(HomepageContent content)
    {
        if (string.IsNullOrWhiteSpace(content.Xaml))
        {
            if (content.ErrorMessage is { Length: > 0 } error)
                ShowHomepageError(error);
            else
                ShowHomepageError(
                    Lang.Text(
                        "Launch.Homepage.LoadFailed.Message",
                        Lang.Text("Launch.Homepage.LoadFailed.Title")));

            return;
        }

        var xaml = PageHelpDetailViewModel.ReplaceArguments(content.Xaml);
        var format = HomepageXamlFormatDetector.Detect(xaml);
        if (format == HomepageXamlFormat.Avalonia)
        {
            var avaloniaResult = HomepageAvaloniaRenderer.Render(xaml);
            if (avaloniaResult.Error is { Length: > 0 }
                || avaloniaResult.Root is null)
            {
                var avaloniaError = avaloniaResult.Error ?? "未知渲染错误";
                LogWrapper.Error(
                    new InvalidOperationException(avaloniaError),
                    "Launch",
                    $"[Page] AXAML 主页渲染失败：{content.SourceText}");
                ShowHomepageError(
                    Lang.Text(
                        "Launch.Homepage.LoadFailed.Message",
                        avaloniaError));
                return;
            }

            _panCustom.Children.Clear();
            _panCustom.Children.Add(avaloniaResult.Root);
            ApplyHomepageLivePatchesFromFile();
            return;
        }

        var rendered = RuntimeXamlRenderer.RenderFragment(xaml, FindResource);
        if (!rendered.Success)
        {
            LogWrapper.Error(
                new InvalidOperationException(rendered.Error ?? "未知渲染错误"),
                "Launch",
                $"[Page] 运行时 XAML 主页渲染失败：{content.SourceText}");
            ShowHomepageError(
                Lang.Text(
                    "Launch.Homepage.LoadFailed.Message",
                    rendered.Error ?? Lang.Text("Launch.Homepage.LoadFailed.Title")));
            return;
        }

        if (rendered.Controls.Count == 0)
        {
            ShowHomepageError(
                Lang.Text(
                    "Launch.Homepage.LoadFailed.Message",
                    "主页中没有可显示的内容"));
            return;
        }

        _panCustom.Children.Clear();
        foreach (var control in rendered.Controls)
            _panCustom.Children.Add(control);

        ApplySanitizeHints(rendered);
        ApplyHomepageLivePatchesFromFile();
    }

    private static void ApplySanitizeHints(RuntimeXamlRenderResult result)
    {
        foreach (var unsupported in result.UnsupportedEventTypes.Distinct(StringComparer.Ordinal))
            HintService.Show(
                Hints.Error(Lang.Text("Event.Sanitize.UnsupportedTypeHint", unsupported)));

        foreach (var unknown in result.UnrecognizedEventTypes.Distinct(StringComparer.Ordinal))
            HintService.Show(
                Hints.Error(Lang.Text("Event.Sanitize.UnknownTypeHint", unknown)));
    }

    private void ShowHomepageError(string message)
    {
        LogWrapper.Info("Launch", $"[Page] 主页加载失败：{message}");
        if (LauncherCommon.ModeDebug)
        {
            _ = ShowHomepageErrorDialogAsync(message);
            return;
        }

        HintService.Show(Hints.Error(Lang.Text("Launch.Homepage.LoadFailed.Title")));
    }

    private async Task ShowHomepageErrorDialogAsync(string message)
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Launch.Homepage.LoadFailed.Title"),
                    Content = Lang.Text("Launch.Homepage.LoadFailed.Message", message),
                    Buttons = DialogButtons.Create(
                        "Launch.Homepage.LoadFailed.Retry",
                        "Common.Action.Cancel"),
                    HighLight = false
                });
            if (result.IsConfirmed)
                ForceRefresh();
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", "显示主页错误弹窗失败");
        }
    }

    /// <inheritdoc />
    protected override void RefreshLocalization()
    {
        if (_currentContent is not { } content)
            return;
        if (content.Kind is not (HomepageSourceKind.CustomXaml or HomepageSourceKind.Trivia))
            return;
        if (content.Kind == HomepageSourceKind.CustomXaml
            && string.IsNullOrWhiteSpace(content.Xaml))
            return;
        LoadContent(content);
    }

    private object? FindResource(string key)
    {
        return this.TryFindResource(key, out var value) ? value : null;
    }

    #endregion

    #region CustomLive.json

    private void EnsureHomepageLiveWatcher()
    {
        if (Config.Preference.Homepage.Type != 1)
        {
            if (_homepageLiveWatcher is not null)
                DisposeHomepageLiveWatcher();
            return;
        }

        if (_homepageLiveWatcher is not null)
            return;

        try
        {
            var directory = Paths.Data;
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
            LauncherCommon.Log(
                ex,
                "[Page] 启动自定义主页实时补丁监视失败",
                LauncherLogLevel.Developer);
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
            LauncherCommon.Log(
                ex,
                "[Page] 释放自定义主页实时补丁监视失败",
                LauncherLogLevel.Developer);
        }

        _homepageLiveWatcher = null;

        try
        {
            if (_homepageLivePatchTimer is not null)
            {
                _homepageLivePatchTimer.Stop();
                _homepageLivePatchTimer.Tick -= HomepageLivePatchTimerTick;
                _homepageLivePatchTimer = null;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[Page] 释放自定义主页实时补丁防抖计时器失败",
                LauncherLogLevel.Developer);
        }

        DeleteHomepageLiveSupportMarker();
    }

    private void QueueHomepageLivePatchApply()
    {
        LauncherCommon.RunInUi(() =>
        {
            _homepageLivePatchTimer ??= new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _homepageLivePatchTimer.Tick -= HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Tick += HomepageLivePatchTimerTick;
            _homepageLivePatchTimer.Stop();
            _homepageLivePatchTimer.Start();
        });
    }

    private void HomepageLivePatchTimerTick(object? sender, EventArgs e)
    {
        _homepageLivePatchTimer?.Stop();
        ApplyHomepageLivePatchesFromFile();
    }

    private void ApplyHomepageLivePatchesFromFile()
    {
        if (_panCustom.Children.Count == 0)
            return;
        if (Config.Preference.Homepage.Type != 1)
            return;

        var file = Path.Combine(Paths.Data, HomepageLivePatchFileName);
        if (!File.Exists(file))
            return;

        try
        {
            var token = JsonNode.Parse(
                ReadHomepageLivePatchFile(file),
                new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
            if (token is null)
                return;

            foreach (var patch in EnumerateHomepageLivePatches(token))
                ApplyHomepageLivePatch(patch);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[Page] 应用自定义主页实时补丁失败",
                LauncherLogLevel.Developer);
        }
    }

    private static string ReadHomepageLivePatchFile(string file)
    {
        Exception? lastException = null;
        for (var i = 0; i < 3; i++)
            try
            {
                using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                lastException = ex;
                Thread.Sleep(50);
            }

        throw lastException ?? new IOException("无法读取自定义主页实时补丁文件");
    }

    private static void WriteHomepageLiveSupportMarker(string directory)
    {
        try
        {
            var marker = new JsonObject(
                new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                })
            {
                ["processId"] = Environment.ProcessId,
                ["processPath"] = Environment.ProcessPath ?? "",
                ["patchFile"] = HomepageLivePatchFileName,
                ["startedAt"] = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
            };
            File.WriteAllText(
                Path.Combine(directory, HomepageLiveSupportFileName),
                marker.ToJsonString());
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[Page] 写入自定义主页实时补丁支持标记失败",
                LauncherLogLevel.Developer);
        }
    }

    private static void DeleteHomepageLiveSupportMarker()
    {
        try
        {
            var file = Path.Combine(Paths.Data, HomepageLiveSupportFileName);
            if (!File.Exists(file))
                return;

            var marker = JsonNode.Parse(
                ReadHomepageLivePatchFile(file),
                new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                },
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                }) as JsonObject;
            if (marker?["processId"]?.GetValue<int>() == Environment.ProcessId)
                File.Delete(file);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "[Page] 删除自定义主页实时补丁支持标记失败",
                LauncherLogLevel.Developer);
        }
    }

    private static IEnumerable<JsonObject> EnumerateHomepageLivePatches(JsonNode token)
    {
        switch (token)
        {
            case JsonObject obj
                when TryGetProperty(obj, "patches", out var patchesNode)
                && patchesNode is JsonArray patches:
            {
                foreach (var patch in patches.OfType<JsonObject>())
                    yield return patch;
                yield break;
            }

            case JsonObject obj
                when TryGetString(obj, "target", "tag", "name") is not null:
            {
                yield return obj;
                yield break;
            }

            case JsonObject obj:
            {
                foreach (var property in obj)
                {
                    if (property.Value is not JsonObject patch)
                        continue;

                    var copy = (JsonObject)JsonNode.Parse(
                        patch.ToJsonString(),
                        new JsonNodeOptions
                        {
                            PropertyNameCaseInsensitive = true
                        },
                        new JsonDocumentOptions
                        {
                            CommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true
                        })!;
                    if (!TryGetProperty(copy, "target", out _))
                        copy["target"] = property.Key;
                    yield return copy;
                }

                break;
            }

            case JsonArray array:
            {
                foreach (var patch in array.OfType<JsonObject>())
                    yield return patch;
                break;
            }
        }
    }

    private void ApplyHomepageLivePatch(JsonObject patch)
    {
        var target = TryGetString(patch, "target", "tag", "name");
        if (string.IsNullOrWhiteSpace(target))
            return;

        foreach (var element in FindElementsByTag(_panCustom, target))
            ApplyHomepageLivePatchToElement(element, patch);
    }

    private void ApplyHomepageLivePatchToElement(Control element, JsonObject patch)
    {
        SetPropertyIfPresent(element, patch, "text", "Text");
        SetPropertyIfPresent(element, patch, "title", "Title");
        SetPropertyIfPresent(element, patch, "info", "Info");
        SetPropertyIfPresent(element, patch, "tooltip", "ToolTip");
        SetPropertyIfPresent(element, patch, "toolTip", "ToolTip");
        SetPropertyIfPresent(element, patch, "visibility", "Visibility");
        SetPropertyIfPresent(element, patch, "isEnabled", "IsEnabled");
        SetPropertyIfPresent(element, patch, "opacity", "Opacity");

        if (TryGetProperty(patch, "properties", out var propertiesNode)
            && propertiesNode is JsonObject properties)
            foreach (var property in properties)
                TrySetElementProperty(
                    element,
                    property.Key,
                    property.Value?.ToString() ?? "");

        var childrenXaml = TryGetString(patch, "childrenXaml", "ChildrenXaml");
        if (!string.IsNullOrEmpty(childrenXaml) && element is Panel panel)
            ReplacePanelChildren(panel, childrenXaml);
    }

    private static void SetPropertyIfPresent(
        Control element,
        JsonObject patch,
        string jsonName,
        string propertyName)
    {
        if (TryGetProperty(patch, jsonName, out var value))
            TrySetElementProperty(element, propertyName, value?.ToString() ?? "");
    }

    private static bool TrySetElementProperty(
        Control element,
        string propertyName,
        string value)
    {
        if (!HomepageLiveAllowedProperties.TryGetValue(propertyName, out var allowedPropertyName))
        {
            LogWrapper.Info("Launch", $"[Page] 忽略不支持的主页补丁属性：{propertyName}");
            return false;
        }

        try
        {
            switch (allowedPropertyName)
            {
                case "ToolTip":
                    ToolTip.SetTip(element, value);
                    return true;

                case "Visibility":
                    element.IsVisible = value.Trim().ToLowerInvariant() switch
                    {
                        "visible" or "true" or "1" => true,
                        "collapsed" or "hidden" or "false" or "0" => false,
                        _ => element.IsVisible
                    };
                    return true;
            }

            var property = element
                .GetType()
                .GetProperty(
                    allowedPropertyName,
                    BindingFlags.Instance | BindingFlags.Public);
            if (property is null || !property.CanWrite)
                return false;

            var propertyType = Nullable.GetUnderlyingType(property.PropertyType)
                ?? property.PropertyType;
            object convertedValue;
            var trimmedValue = value.Trim();
            if (propertyType == typeof(string))
                convertedValue = value;
            else if (propertyType == typeof(bool) && bool.TryParse(trimmedValue, out var boolValue))
                convertedValue = boolValue;
            else if (propertyType == typeof(int)
                     && int.TryParse(
                         trimmedValue,
                         NumberStyles.Integer,
                         CultureInfo.InvariantCulture,
                         out var intValue))
                convertedValue = intValue;
            else if (propertyType == typeof(double)
                     && double.TryParse(
                         trimmedValue,
                         NumberStyles.Float | NumberStyles.AllowThousands,
                         CultureInfo.InvariantCulture,
                         out var doubleValue))
                convertedValue = doubleValue;
            else if (propertyType.IsEnum
                     && Enum.TryParse(propertyType, trimmedValue, true, out var enumValue))
                convertedValue = enumValue;
            else
                return false;

            property.SetValue(element, convertedValue);
            return true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                $"[Page] 设置主页补丁属性失败：{propertyName}",
                LauncherLogLevel.Developer);
            return false;
        }
    }

    private void ReplacePanelChildren(Panel panel, string childrenXaml)
    {
        var rendered = RuntimeXamlRenderer.RenderFragment(
            PageHelpDetailViewModel.ReplaceArguments(childrenXaml),
            FindResource);
        if (!rendered.Success)
        {
            LauncherCommon.Log(
                new InvalidOperationException(rendered.Error ?? "未知错误"),
                "[Page] 替换主页子内容失败",
                LauncherLogLevel.Developer);
            return;
        }

        // MyCard 的前三个子元素是阴影、背景与标题层；只替换真正的页面内容。
        var firstContentIndex = panel is MyCard ? Math.Min(3, panel.Children.Count) : 0;
        for (var i = panel.Children.Count - 1; i >= firstContentIndex; i--)
            panel.Children.RemoveAt(i);

        foreach (var child in rendered.Controls)
            panel.Children.Add(child);
    }

    private static IEnumerable<Control> FindElementsByTag(Control root, string tag)
    {
        if (root.Tag is { } rootTag
            && string.Equals(rootTag.ToString(), tag, StringComparison.OrdinalIgnoreCase))
            yield return root;

        foreach (var child in root.GetLogicalChildren().OfType<Control>())
        foreach (var element in FindElementsByTag(child, tag))
            yield return element;
    }

    private static string? TryGetString(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
            if (TryGetProperty(obj, name, out var value))
                return value?.ToString();

        return null;
    }

    private static bool TryGetProperty(JsonObject obj, string name, out JsonNode? value)
    {
        foreach (var property in obj)
            if (property.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = null;
        return false;
    }

    #endregion
}