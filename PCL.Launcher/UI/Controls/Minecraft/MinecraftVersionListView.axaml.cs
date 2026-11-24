using System.Text.Json.Nodes;
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Views.Pages.Download;

namespace PCL.UI.Controls.Minecraft;

public partial class MinecraftVersionListView : UserControl
{
    private MinecraftVersionCatalog? _catalog;
    private bool _showInlineStatus;

    public MinecraftVersionListView()
    {
        InitializeComponent();
        Grid.SetIsSharedSizeScope(PanVersions, true);
    }

    internal MinecraftVersionCatalog Catalog =>
        _catalog ?? throw new InvalidOperationException("版本列表尚未初始化。");

    public bool HasVersions => _catalog?.HasVersions == true;

    public event Action? Rebuilt;

    internal void Initialize(
        DownloadVersionListMode mode,
        Action<JsonObject>? onSelect,
        bool showInlineStatus)
    {
        _showInlineStatus = showInlineStatus;
        _catalog = new MinecraftVersionCatalog(PanVersions, mode, onSelect);
        _catalog.StateChanged += UpdateStatus;
        _catalog.Rebuilt += () =>
        {
            UpdateStatus();
            Rebuilt?.Invoke();
        };
        BtnRetry.Click += (_, _) => _catalog?.Refresh();
        UpdateStatus();
    }

    public void Activate()
    {
        _catalog?.Activate();
        UpdateStatus();
    }

    public void Deactivate()
    {
        _catalog?.Deactivate();
    }

    public void Refresh()
    {
        _catalog?.Refresh();
        UpdateStatus();
    }

    public void Rebuild()
    {
        _catalog?.Rebuild();
        UpdateStatus();
    }

    public void RefreshInstalledMarks()
    {
        _catalog?.RefreshInstalledMarks();
    }

    private void UpdateStatus()
    {
        if (!_showInlineStatus || _catalog is null)
        {
            PanStatus.IsVisible = false;
            return;
        }

        var state = _catalog.State;
        if (state is LoadState.Waiting or LoadState.Loading || _catalog.IsRebuildPending)
        {
            ShowLoading();
            return;
        }

        if (state is LoadState.Failed or LoadState.Aborted)
        {
            ShowError(
                state == LoadState.Failed
                    ? _catalog.Error ?? Lang.Text("Download.Install.Error.OperationFailed")
                    : Lang.Text("Download.Install.State.UnknownUnloaded"));
            return;
        }

        if (_catalog.HasVersions)
        {
            HideStatus();
            return;
        }

        ShowError(
            _catalog.BuildError is { } buildError
                ? $"{Lang.Text("Download.Install.Error.OperationFailed")} {buildError}"
                : Lang.Text("Download.Install.State.NoVersion"));
    }

    private void ShowLoading()
    {
        PanStatus.IsVisible = true;
        LoadVersions.IsVisible = true;
        BtnRetry.IsVisible = false;
        LoadVersions.SetLocalizedValue(
            MyLoading.TextProperty,
            "Download.Version.LoadingList");
        LoadVersions.TextError = "";
        LoadVersions.ShowProgress = false;
        LoadVersions.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
    }

    private void ShowError(string error)
    {
        PanStatus.IsVisible = true;
        LoadVersions.IsVisible = true;
        BtnRetry.IsVisible = true;
        LoadVersions.SetLocalizedValue(
            MyLoading.TextProperty,
            "Download.Version.VersionList");
        LoadVersions.TextError = error;
        LoadVersions.ShowProgress = false;
        LoadVersions.State = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Error
        };
    }

    private void HideStatus()
    {
        PanStatus.IsVisible = false;
        LoadVersions.State = new MyLoadingStateSimulator();
    }
}