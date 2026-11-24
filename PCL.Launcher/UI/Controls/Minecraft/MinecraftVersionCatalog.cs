using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Views.Pages.Download;

namespace PCL.UI.Controls.Minecraft;

internal sealed class MinecraftVersionCatalog(
    StackPanel host,
    DownloadVersionListMode mode,
    Action<JsonObject>? onSelect = null)
{
    private readonly Dictionary<string, MyListItem> _items = new(StringComparer.OrdinalIgnoreCase);

    private bool _active;

    public LoadState State => GameDownload.dlClientListLoader.State;

    public string? Error => GameDownload.dlClientListLoader.Error?.Message;

    public string? BuildError { get; private set; }

    public bool IsRebuildPending { get; private set; }

    public bool HasVersions => _items.Count > 0;

    public IReadOnlyDictionary<string, MyListItem> Items => _items;

    public event Action? StateChanged;

    public event Action? Rebuilt;

    public void Activate()
    {
        if (_active)
            return;
        _active = true;
        GameDownload.dlClientListLoader.PreviewFinish += OnVersionListPreviewFinish;
        GameDownload.dlClientListLoader.OnStateChangedUi += OnVersionListStateChanged;
        GameFolders.McFolderListLoader.PreviewFinish += OnFolderListPreviewFinish;
        Rebuild();
    }

    public void Deactivate()
    {
        if (!_active)
            return;
        _active = false;
        GameDownload.dlClientListLoader.PreviewFinish -= OnVersionListPreviewFinish;
        GameDownload.dlClientListLoader.OnStateChangedUi -= OnVersionListStateChanged;
        GameFolders.McFolderListLoader.PreviewFinish -= OnFolderListPreviewFinish;
    }

    public void Refresh()
    {
        try
        {
            GameDownload.dlClientListLoader.Start(null, true);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "加载 Minecraft 版本列表失败");
            BuildError = ex.Message;
            RaiseStateChanged();
        }
    }

    public void Rebuild()
    {
        RebuildCore(false);
    }

    private void RebuildCore(bool force)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RebuildCore(force));
            return;
        }

        if (!force
            && State is LoadState.Waiting or LoadState.Loading
            && host.Children.Count > 0)
        {
            IsRebuildPending = false;
            RaiseStateChanged();
            return;
        }

        IsRebuildPending = false;
        BuildError = null;
        try
        {
            _items.Clear();
            host.Children.Clear();
            if (GameDownload.dlClientListLoader.output.Value?["versions"] is JsonArray
                {
                    Count: > 0
                } versions)
                DownloadVersionList.Build(host, versions, onSelect, mode, _items);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "构建 Minecraft 版本列表失败");
            BuildError = ex.Message;
            _items.Clear();
            host.Children.Clear();
        }

        Rebuilt?.Invoke();
        RaiseStateChanged();
    }

    public void RefreshInstalledMarks()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshInstalledMarks);
            return;
        }

        if (_items.Count == 0)
            return;
        if (GameDownload.dlClientListLoader.output.Value?["versions"] is not JsonArray versions)
            return;

        try
        {
            var installed = DownloadVersionList.GetInstalledVersionNames();
            foreach (var node in versions)
            {
                if (node is not JsonObject version)
                    continue;
                var versionId = version["id"]?.ToString();
                if (string.IsNullOrEmpty(versionId)
                    || !_items.TryGetValue(versionId, out var item))
                    continue;
                DownloadVersionList.ApplyVersionTags(
                    item,
                    version,
                    installed.Contains(versionId));
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新版本已安装标记失败");
        }
    }

    private void OnVersionListPreviewFinish(Loading.LoaderBase loader)
    {
        IsRebuildPending = true;
        Dispatcher.UIThread.Post(() => RebuildCore(true));
    }

    private void OnVersionListStateChanged(
        Loading.LoaderBase loader,
        LoadState newState,
        LoadState oldState)
    {
        RaiseStateChanged();
    }

    private void OnFolderListPreviewFinish(Loading.LoaderBase loader)
    {
        Dispatcher.UIThread.Post(RefreshInstalledMarks);
    }

    private void RaiseStateChanged()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => StateChanged?.Invoke());
            return;
        }

        StateChanged?.Invoke();
    }
}