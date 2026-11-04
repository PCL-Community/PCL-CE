using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using PCL.UI.Seams;
using PCL.Views.Pages.Homepage;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页右栏：按 <c>Preference.Homepage</c> 配置构建主页内容，另带社区版提示与启动日志卡片。
///     主页来源分为空白、内置“你知道吗”、Minecraft 信息流与运行时 XAML 主页四类；
///     运行时 XAML 主页当前无法渲染，页面会给出带来源与操作入口的降级说明。
/// </summary>
public partial class PageLaunchRight : MyPageRight, ISeamLaunchLogSink, IRefreshable
{
    private readonly TextBlock _labHint1;
    private readonly TextBlock _labHint2;
    private readonly TextBlock _labLog;
    private readonly MyCard _panHint;
    private readonly MyCard _panLog;
    private readonly MyScrollViewer _panBack;
    private readonly StackPanel _panCustom;
    private readonly PageHomepageNewsView _newsView;

    private HomepageContent? _currentContent;
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

        SeamHosts.LaunchLog = this;
        FileDragHandler.CustomHomepageDropped += _ => ForceRefresh();

        _panHint.IsVisible = States.Hint.CEMessage;
        LocalizedText.TryBind(_labHint1, "Text", () => Lang.Text("Launch.Right.CommunityHint.Message"));
        LocalizedText.TryBind(_labHint2, "Text", () => Lang.Text("Launch.Right.CommunityHint.HidePrompt"));
        this.FindControl<MyIconButton>("BtnHintClose")!.Click += (_, _) => HideCommunityHint();

        _panLog.IsVisible = LauncherCommon.ModeDebug;

        Loaded += (_, _) =>
        {
            _panBack.ScrollToHome();
            Refresh();
        };
    }

    public static PageLaunchRight? Current { get; private set; }

    public override void OnNavigatedTo(object? parameter)
    {
    }

    public override void OnNavigatedFrom()
    {
    }

    public void AppendLaunchLog(string text)
    {
        if (Dispatcher.UIThread.CheckAccess())
            _labLog.Text += text;
        else
            Dispatcher.UIThread.Post(() => _labLog.Text += text);
    }

    public void ClearLaunchLog()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _labLog.Text = "";
        else
            Dispatcher.UIThread.Post(() => _labLog.Text = "");
    }

    /// <inheritdoc />
    public void Refresh()
    {
        ForceRefresh();
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
        Task.Run(() =>
        {
            HomepageContent content;
            try
            {
                content = HomepageSource.Resolve(ShowHomepageLoadingHint);
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Launch", "加载 PCL 主页自定义信息失败");
                content = new HomepageContent { Kind = HomepageSourceKind.Blank };
            }

            LauncherCommon.RunInUi(() =>
            {
                if (version != Volatile.Read(ref _refreshVersion))
                    return;
                LoadContent(content);
            });
        });
    }

    private static void ShowHomepageLoadingHint()
    {
        HintService.Show(Hints.Info(Lang.Text("Launch.Homepage.Loading")));
    }

    #region 主页内容

    // TODO(FIX-P3-01): 运行时 XAML 主页（本地文件、联网 URL、预设 2-14）与 CustomLive.json 实时补丁
    // 尚未实现渲染，当前只解析来源并给出带“打开来源/刷新”的降级说明卡（降级卡已在界面上可见告知用户，
    // 不会静默显示空白主页）；补渲染时替换 LoadContent 的 default 分支。

    private void LoadContent(HomepageContent content)
    {
        _currentContent = content;
        _panCustom.Children.Clear();

        switch (content.Kind)
        {
            case HomepageSourceKind.Blank:
                LogWrapper.Info("Launch", "[Page] 实例化：清空主页 UI，来源为空");
                break;

            case HomepageSourceKind.Removed:
                DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text("Launch.Homepage.Preset.EchoCave.Removed"),
                    Buttons = DialogButtons.Ok,
                    ForceWait = true
                });
                break;

            case HomepageSourceKind.Trivia:
                _panCustom.Children.Add(
                    HomepageCardRenderer.CreateTrivia(
                        content.TriviaText ?? "",
                        ForceRefresh,
                        FindBrush));
                break;

            case HomepageSourceKind.NewsFeed:
                _panCustom.Children.Add(_newsView);
                _newsView.Reload();
                break;

            default:
                LogWrapper.Info(
                    "Launch",
                    $"[Page] 运行时 XAML 主页尚未迁移，暂不渲染（来源：{content.SourceText}）");
                break;
        }
    }

    /// <inheritdoc />
    protected override void RefreshLocalization()
    {
        if (_currentContent is not { } content)
            return;
        if (content.Kind is not (HomepageSourceKind.CustomXaml or HomepageSourceKind.Trivia))
            return;
        LoadContent(content);
    }

    private IBrush? FindBrush(string key)
    {
        return this.TryFindResource(key, out var value) ? value as IBrush : null;
    }

    private void HideCommunityHint()
    {
        var result = DialogService.Instance.Show(new InputDialogRequest
        {
            Title = Lang.Text("Launch.Right.CommunityHint.InputTitle"),
            Buttons = DialogButtons.OkCancel,
            HighLight = false,
            ForceWait = true
        });
        var input = result.Result?.InputText ?? "";
        if (string.IsNullOrWhiteSpace(input))
            return;

        input = new string(input.Where(char.IsAsciiLetter).ToArray()).ToLowerInvariant();
        if (!input.Contains("pclcommunity"))
        {
            HintService.Show(Hints.Info(Lang.Text("Launch.Right.CommunityHint.WrongInput")));
            return;
        }

        Animations.AniDispose(_panHint, true);
        States.Hint.CEMessage = false;
    }

    #endregion
}