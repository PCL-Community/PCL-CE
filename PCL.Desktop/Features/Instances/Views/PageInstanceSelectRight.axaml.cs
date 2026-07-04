// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceSelectRight : MyPageRight, IDisposable
{
    private const int SearchNormalDelayMs = 75;
    private const int SearchQuickDelayMs = 50;
    private readonly DispatcherTimer _reloadTimer;
    private IReadOnlyList<LaunchInstanceInfo> _instances = [];
    private LaunchInstanceInfo? _selectedInstance;
    private DateTime _lastInputTime = DateTime.MinValue;
    private bool _isRefreshing;
    private bool _isLoading;

    public PageInstanceSelectRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _reloadTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(SearchNormalDelayMs)
        };
        _reloadTimer.Tick += ReloadTimer_Tick;
        if (this.FindControl<MySearchBox>("PanVerSearchBox") is { } searchBox)
            searchBox.TextChanged += PanVerSearchBox_TextChanged;
        SetLoadingState(false);
    }

    public event EventHandler? RefreshRequested;

    public event EventHandler? DownloadRequested;

    public event EventHandler<LaunchInstanceInfo>? InstanceSelected;

    public event EventHandler<LaunchInstanceInfo>? InstanceManageRequested;

    public event EventHandler<LaunchInstanceInfo>? InstanceOpenFolderRequested;

    public event EventHandler<LaunchInstanceInfo>? InstanceDeleteRequested;

    public void SetLoadingState(bool isLoading = true)
    {
        _isLoading = isLoading;
        SetVisible("PanLoad", isLoading);
        SetVisible("PanAllBack", !isLoading);
        if (!isLoading)
            ReloadList();
    }

    public void SetInstances(IReadOnlyList<LaunchInstanceInfo> instances, LaunchInstanceInfo? selectedInstance)
    {
        _instances = instances;
        _selectedInstance = selectedInstance;
        SetLoadingState(false);
    }

    public override void Dispose()
    {
        _reloadTimer.Stop();
        _reloadTimer.Tick -= ReloadTimer_Tick;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void PanVerSearchBox_TextChanged(object sender, EventArgs e)
    {
        _lastInputTime = DateTime.Now;
        _isRefreshing = false;

        string text = this.FindControl<MySearchBox>("PanVerSearchBox")?.Text ?? string.Empty;
        int delay = string.IsNullOrWhiteSpace(text) ? SearchQuickDelayMs : SearchNormalDelayMs;
        if (Math.Abs(_reloadTimer.Interval.TotalMilliseconds - delay) > 0.1d)
            _reloadTimer.Interval = TimeSpan.FromMilliseconds(delay);

        if (!_reloadTimer.IsEnabled)
            _reloadTimer.Start();
    }

    private void ReloadTimer_Tick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - _lastInputTime).TotalMilliseconds;
        if (elapsed < _reloadTimer.Interval.TotalMilliseconds || _isRefreshing)
            return;

        _isRefreshing = true;
        ReloadList();
        _isRefreshing = false;
        _reloadTimer.Stop();
    }

    private void ReloadList()
    {
        if (_isLoading)
            return;

        StackPanel? panel = this.FindControl<StackPanel>("PanMain");
        if (panel is null)
            return;

        string searchText = this.FindControl<MySearchBox>("PanVerSearchBox")?.Text?.Trim() ?? string.Empty;
        InstanceEntry[] filteredInstances = _instances
            .Select(static instance => new InstanceEntry(
                instance,
                InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult()))
            .Where(entry => IsSearchMatch(entry, searchText))
            .ToArray();

        panel.Children.Clear();
        if (filteredInstances.Length > 0)
        {
            foreach (IGrouping<int, InstanceEntry> group in filteredInstances
                         .GroupBy(static entry => entry.Metadata.IsStarred ? -1 : Math.Clamp(entry.Metadata.CardType, 0, 5))
                         .OrderBy(static group => group.Key is -1 ? 0 : group.Key + 1))
            {
                panel.Children.Add(CreateInstanceCard(group.ToArray()));
            }
        }

        SetVisible("PanVerSearchBox", _instances.Count > 0);
        if (_instances.Count == 0)
        {
            SetVisible("PanBack", false);
            SetVisible("PanEmpty", true);
            SetVisible("PanEmptySearch", false);
            return;
        }

        if (filteredInstances.Length == 0)
        {
            SetVisible("PanBack", true);
            SetVisible("PanEmpty", false);
            SetVisible("PanEmptySearch", true);
            SetText("LabEmptySearchContent",
                string.IsNullOrWhiteSpace(searchText)
                    ? "请输入版本名称或路径中的关键词。"
                    : $"没有找到包含“{searchText}”的本地版本。");
            return;
        }

        SetVisible("PanBack", true);
        SetVisible("PanEmpty", false);
        SetVisible("PanEmptySearch", false);
    }

    private static bool IsSearchMatch(InstanceEntry entry, string searchText) =>
        string.IsNullOrWhiteSpace(searchText) ||
        entry.Instance.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        entry.Instance.InstanceDirectory.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
        entry.Metadata.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase);

    private MyCard CreateInstanceCard(InstanceEntry[] instances)
    {
        StackPanel stack = new()
        {
            Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            RenderTransform = new TranslateTransform(),
            Tag = instances
        };
        InstanceEntry first = instances[0];
        MyCard card = new()
        {
            Title = InstanceDisplayHelper.GetCardTitle(first.Metadata.CardType, first.Metadata.IsStarred, instances.Length),
            Margin = new Thickness(0d, 0d, 0d, 15d),
            SwapControl = stack
        };
        card.Children.Add(stack);

        void Install(StackPanel target)
        {
            if (target.Tag is not InstanceEntry[] entries)
                return;

            foreach (InstanceEntry entry in entries)
                target.Children.Add(CreateInstanceItem(entry));
        }

        MyCard.StackInstall(ref stack, Install);
        return card;
    }

    private MyListItem CreateInstanceItem(InstanceEntry entry)
    {
        LaunchInstanceInfo instance = entry.Instance;
        MyIconButton btnOpenFolder = new()
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/folder-open",
            ToolTip = "打开版本文件夹"
        };
        btnOpenFolder.Click += (_, _) => InstanceOpenFolderRequested?.Invoke(this, instance);

        MyIconButton btnDelete = new()
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/trash-2",
            ToolTip = "删除版本"
        };
        btnDelete.Click += (_, _) => InstanceDeleteRequested?.Invoke(this, instance);

        MyIconButton btnSettings = new()
        {
            LogoScale = 1.1d,
            SvgIcon = "lucide/settings",
            ToolTip = "版本设置"
        };
        btnSettings.Click += (_, _) => InstanceManageRequested?.Invoke(this, instance);

        MyListItem item = new()
        {
            Title = instance.Name,
            Info = string.IsNullOrWhiteSpace(entry.Metadata.Description)
                ? instance.InstanceDirectory
                : entry.Metadata.Description,
            Height = 42d,
            Tag = instance,
            Type = MyListItem.CheckType.Clickable,
            Logo = InstanceDisplayHelper.ResolveLogo(instance, entry.Metadata),
            LogoScale = 0.85d,
            Buttons = [btnOpenFolder, btnDelete, btnSettings]
        };
        if (_selectedInstance is not null &&
            string.Equals(_selectedInstance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            item.Info = "当前选择 · " + instance.InstanceDirectory;
        }

        item.Click += (_, _) => InstanceSelected?.Invoke(this, instance);
        return item;
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        SetLoadingState();
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnDownload_Click(object? sender, EventArgs e) =>
        DownloadRequested?.Invoke(this, EventArgs.Empty);

    private void SetVisible(string name, bool isVisible)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.IsVisible = isVisible;
    }

    private void SetText(string name, string text)
    {
        if (this.FindControl<TextBlock>(name) is { } block)
            block.Text = text;
    }

    private readonly record struct InstanceEntry(LaunchInstanceInfo Instance, InstanceMetadata Metadata);
}
