using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.Lobby;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Tools;

/// <summary>
///     联机大厅页：公告、协议、账号与 NAT/EasyTier 状态、世界发现及大厅生命周期。
///     状态与后端交互由 <see cref="PageToolsGameLinkViewModel" /> 持有，本文件只负责控件事件与剪贴板。
/// </summary>
public partial class PageToolsGameLink : MyPageRight
{
    private const string PrivacyUrl = "https://www.pclc.cc/privacy/personal-info-brief.html";
    private const string TermsUrl = "https://account.naids.com/policy";
    private const string ReportUrl = "https://qm.qq.com/q/zfml1KaPWS";
    private const string EasyTierUrl = "https://easytier.cn/";
    private const string PysioUrl = "https://pysio.online/";

    private readonly MyButton _btnClearCode;
    private readonly MyButton _btnCreate;
    private readonly MyButton _btnEulaAgree;
    private readonly MyTextButton _btnEulaStop;
    private readonly MyIconTextButton _btnFinishCopy;
    private readonly MyIconTextButton _btnFinishCopyIp;
    private readonly MyIconTextButton _btnFinishExit;
    private readonly MyButton _btnInputPort;
    private readonly MyButton _btnJoin;
    private readonly MyIconButton _btnLoadCancel;
    private readonly MyButton _btnPasteCode;
    private readonly MyButton _btnRefreshWorlds;
    private readonly MyHint _hintAnnounce;
    private readonly MyLoading _load;
    private readonly MyLoadingStateSimulator _loadState = new();
    private readonly MyTextBox _txtJoinCode;

    public PageToolsGameLink()
    {
        InitializeComponent();
        PanScroll = PanContent;
        DataContext = ViewModel;

        _load = this.FindControl<MyLoading>("Load")!;
        _hintAnnounce = this.FindControl<MyHint>("HintAnnounce")!;
        _btnLoadCancel = this.FindControl<MyIconButton>("BtnLoadCancel")!;
        _txtJoinCode = this.FindControl<MyTextBox>("TxtJoinCode")!;
        _btnCreate = this.FindControl<MyButton>("BtnCreate")!;
        _btnJoin = this.FindControl<MyButton>("BtnJoin")!;
        _btnInputPort = this.FindControl<MyButton>("BtnInputPort")!;
        _btnRefreshWorlds = this.FindControl<MyButton>("BtnRefreshWorlds")!;
        _btnClearCode = this.FindControl<MyButton>("BtnClearCode")!;
        _btnPasteCode = this.FindControl<MyButton>("BtnPasteCode")!;
        _btnEulaAgree = this.FindControl<MyButton>("BtnEulaAgree")!;
        _btnEulaStop = this.FindControl<MyTextButton>("BtnEulaStop")!;
        _btnFinishCopy = this.FindControl<MyIconTextButton>("BtnFinishCopy")!;
        _btnFinishCopyIp = this.FindControl<MyIconTextButton>("BtnFinishCopyIp")!;
        _btnFinishExit = this.FindControl<MyIconTextButton>("BtnFinishExit")!;

        Current = this;
        ViewModel.PlayerRows.CollectionChanged += (_, _) => RebuildPlayerList();
        _load.AutoRun = true;
        _load.State = _loadState;
        _hintAnnounce.Theme = (MyHint.Themes)ViewModel.AnnouncementThemeIndex;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        this.FindControl<MyCard>("CardLoad")!.PointerReleased += (_, _) =>
        {
            if (ViewModel.IsLoadError)
                _ = ViewModel.RetryInitializeAsync();
        };

        _btnCreate.Click += (_, _) => _ = ViewModel.CreateLobbyAsync();
        _btnJoin.Click += (_, _) => _ = ViewModel.JoinLobbyAsync();
        _btnInputPort.Click += (_, _) => _ = ViewModel.InputManualPortAsync();
        _btnRefreshWorlds.Click += (_, _) => _ = ViewModel.RefreshWorldsAsync();
        _btnClearCode.Click += (_, _) =>
        {
            _txtJoinCode.Text = string.Empty;
            ViewModel.JoinCode = string.Empty;
        };
        _btnPasteCode.Click += (_, _) => _ = _PasteLobbyCodeAsync();
        _btnEulaAgree.Click += (_, _) => ViewModel.AgreeEula();
        _btnEulaStop.Click += (_, _) => _ = ViewModel.RevokeEulaAsync();
        _btnLoadCancel.Click += (_, _) => _ = ViewModel.CancelLoadAsync();
        _btnFinishCopy.Click += (_, _) => _ = _CopyLobbyCodeAsync();
        _btnFinishCopyIp.Click += (_, _) => _ = _ShowCopyIpDialogAsync();
        _btnFinishExit.Click += (_, _) => _ = ViewModel.LeaveLobbyAsync();

        _txtJoinCode.TextChanged +=
            (_, _) => ViewModel.JoinCode = _txtJoinCode.Text ?? string.Empty;
        _txtJoinCode.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter)
                return;
            e.Handled = true;
            _ = ViewModel.JoinLobbyAsync();
        };

        this.FindControl<MyListItem>("ItemEulaPrivacy")!.Click += (_, _) => OpenLink(PrivacyUrl);
        this.FindControl<MyListItem>("ItemEulaTerms")!.Click += (_, _) => OpenLink(TermsUrl);
        this.FindControl<MyTextButton>("BtnLinkPrivacy")!.Click += (_, _) => OpenLink(PrivacyUrl);
        this.FindControl<MyTextButton>("BtnLinkTerms")!.Click += (_, _) => OpenLink(TermsUrl);
        this.FindControl<MyTextButton>("BtnLinkReport")!.Click += (_, _) => OpenLink(ReportUrl);
        this.FindControl<MyTextButton>("BtnLinkEasyTier")!.Click += (_, _) => OpenLink(EasyTierUrl);
        this.FindControl<MyTextButton>("BtnLinkPysio")!.Click += (_, _) => OpenLink(PysioUrl);

        UpdateLoadState();
        Loaded += (_, _) =>
        {
            UpdateLoadState();
            RebuildPlayerList();
            ViewModel.Reload();
        };
        Unloaded += (_, _) => ViewModel.Suspend();
    }

    public static PageToolsGameLink? Current { get; private set; }

    public PageToolsGameLinkViewModel ViewModel { get; } = new();

    private void RebuildPlayerList()
    {
        StackPlayerList.Children.Clear();
        foreach (var row in ViewModel.PlayerRows)
        {
            var placeholder = new MyVirtualizingElement<MyListItem>(() => new MyListItem
            {
                Title = row.Name,
                Info = row.Info,
                SvgIcon = "lucide/user",
                Type = MyListItem.CheckType.Clickable,
                Command = row.ShowInfoCommand
            })
            {
                Height = 42d,
                RecycleOffscreen = true
            };
            StackPlayerList.Children.Add(placeholder);
        }
    }

    public void Reload()
    {
        ViewModel.Reload();
    }

    protected override void RefreshLocalization()
    {
        ViewModel.RefreshLocalization();
        UpdateLoadState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PageToolsGameLinkViewModel.AnnouncementThemeIndex):
                _hintAnnounce.Theme = (MyHint.Themes)ViewModel.AnnouncementThemeIndex;
                break;
            case nameof(PageToolsGameLinkViewModel.IsLoadError):
            case nameof(PageToolsGameLinkViewModel.ShowLoader):
                UpdateLoadState();
                break;
        }
    }

    private void UpdateLoadState()
    {
        _loadState.LoadingState = ViewModel.IsLoadError
            ? LoadingState.Error
            : ViewModel.ShowLoader
                ? LoadingState.Run
                : LoadingState.Stop;
    }

    private static void OpenLink(string url)
    {
        try
        {
            Basics.OpenPath(url);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", $"打开链接失败: {url}");
            HintService.Show(Hints.Error(ex.Message));
        }
    }

    private void CardNatTest_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = ViewModel.RunNatTestAsync();
    }

    private void CardAccount_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = ViewModel.ToggleNatayarkAccountAsync();
    }

    private async Task _CopyLobbyCodeAsync()
    {
        var lobbyCode = LobbyService.CurrentLobbyCode;
        if (string.IsNullOrWhiteSpace(lobbyCode)
            || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Finish.Unavailable")));
            return;
        }

        try
        {
            await clipboard.SetTextAsync(lobbyCode);
            HintService.Show(Hints.Success(Lang.Text("Tools.GameLink.Finish.CopyLobbyId")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "复制大厅编号失败");
            HintService.Show(Hints.Error(ex.Message));
        }
    }

    /// <summary>显示本机大厅转发地址，与 WPF 的复制 IP 弹窗一致。</summary>
    private async Task _ShowCopyIpDialogAsync()
    {
        var ip = ViewModel.GetCopyIpAddress();
        if (ip is null)
        {
            HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Finish.Unavailable")));
            return;
        }

        try
        {
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.GameLink.CopyIp.Title"),
                    Content = Lang.Text("Tools.GameLink.CopyIp.Message", ip),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(
                            Lang.Text("Common.Action.Copy"),
                            () => _ = _CopyTextAsync(ip)),
                        DialogButton.Create(Lang.Text("Tools.GameLink.CopyIp.Back"))),
                    ForceWait = true
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "显示复制 IP 弹窗失败");
            HintService.Show(Hints.Error(ex.Message));
        }
    }

    private async Task _CopyTextAsync(string text)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Finish.Unavailable")));
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "复制大厅地址失败");
            HintService.Show(Hints.Error(ex.Message));
        }
    }

    private async Task _PasteLobbyCodeAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            HintService.Show(Hints.Warning(Lang.Text("Tools.GameLink.Join.InvalidText")));
            return;
        }

        try
        {
            var text = await clipboard.TryGetTextAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                HintService.Show(Hints.Info(Lang.Text("Tools.GameLink.Join.InvalidText")));
                return;
            }

            _txtJoinCode.Text = text.Trim();
            ViewModel.JoinCode = _txtJoinCode.Text;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Link", "读取剪贴板失败");
            HintService.Show(Hints.Error(ex.Message));
        }
    }
}