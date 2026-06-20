// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using PCL.Application.Logging;
using PCL.Desktop.Models;
using PCL.Desktop.Services;
using PCL.Desktop.ViewModels.Feedback;
using PCL.Desktop.ViewModels.Home;
using PCL.Desktop.ViewModels.Log;
using PCL.Desktop.ViewModels.Common;
using PCL.Desktop.ViewModels.Tools;
using PCL.Plugin;
using PCL.UI.Abstractions;

namespace PCL.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly PluginManifest PluginPreview = new(
        "pcl.plugin",
        "插件",
        new Version(0, 1, 0),
        "PCL N",
        "扩展启动器功能",
        []);

    private string _selectedTitle = string.Empty;
    private string _selectedDescription = string.Empty;
    private object? _currentPage;

    public MainWindowViewModel(
        DesktopEnvironmentSnapshot environment,
        InAppNotificationService notificationService,
        IDialogService dialogService,
        IThemeService themeService,
        ILauncherLogSource logSource,
        IUiScheduler scheduler,
        IClipboardService clipboardService,
        IFileDialogService fileDialogService)
    {
        Environment = environment;
        Messages = notificationService.Messages;
        RuntimeStatus = "运行环境已就绪";
        HomePageViewModel homePage = new(environment);
        ControlsGalleryViewModel controlsGallery = new(
            dialogService,
            notificationService,
            notificationService,
            themeService);
        _logSource = logSource;
        _logPage = new LogPageViewModel(
            logSource,
            scheduler,
            clipboardService,
            fileDialogService,
            notificationService);
        NavigationItems =
        [
            CreateNavigationItem(
                "首页",
                "查看启动器状态与最近活动。",
                "lucide/home",
                homePage),
            CreatePlaceholder(
                "启动",
                "选择账户、版本并启动游戏。",
                "lucide/play"),
            CreatePlaceholder(
                "版本管理",
                "管理本地 Minecraft 版本与启动档案。",
                "lucide/boxes"),
            CreatePlaceholder(
                "下载",
                "查找并安装游戏、模组与资源。",
                "lucide/download"),
            CreatePlaceholder(
                "联机",
                "查看好友与可加入的服务器。",
                "lucide/users"),
            CreateNavigationItem(
                "日志",
                "查看、筛选和导出启动器运行日志。",
                "lucide/terminal",
                _logPage),
            CreatePlaceholder(
                "设置",
                "调整启动、下载与界面选项。",
                "lucide/settings"),
            CreateNavigationItem(
                "界面组件",
                "检查主题、控件、提示和对话框。",
                "lucide/palette",
                controlsGallery),
            CreateNavigationItem(
                PluginPreview.Name,
                "扩展能力正在准备中。",
                "lucide/package",
                new PlaceholderPageViewModel(
                    PluginPreview.Name,
                    "插件功能正在准备中，后续将在此管理扩展。",
                    "lucide/package",
                    true),
                isComingSoon: true)
        ];

        Select(NavigationItems[0]);
    }

    private readonly ILauncherLogSource _logSource;
    private readonly LogPageViewModel _logPage;

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public DesktopEnvironmentSnapshot Environment { get; }

    public ReadOnlyObservableCollection<InAppMessageViewModel> Messages { get; }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public string SelectedTitle
    {
        get => _selectedTitle;
        private set => SetProperty(ref _selectedTitle, value);
    }

    public string SelectedDescription
    {
        get => _selectedDescription;
        private set => SetProperty(ref _selectedDescription, value);
    }

    public string RuntimeStatus { get; }

    public void Dispose()
    {
        _logPage.Dispose();
        _logSource.Dispose();
    }

    private NavigationItemViewModel CreateNavigationItem(
        string title,
        string description,
        string iconKey,
        object page,
        bool isComingSoon = false) =>
        new(
            title,
            description,
            iconKey,
            page,
            isComingSoon,
            Select);

    private NavigationItemViewModel CreatePlaceholder(
        string title,
        string description,
        string iconKey) =>
        CreateNavigationItem(
            title,
            description,
            iconKey,
            new PlaceholderPageViewModel(
                title,
                description,
                iconKey,
                false));

    private void Select(NavigationItemViewModel selected)
    {
        foreach (NavigationItemViewModel item in NavigationItems)
            item.IsSelected = ReferenceEquals(item, selected);

        SelectedTitle = selected.Title;
        SelectedDescription = selected.Description;
        CurrentPage = selected.Page;
    }
}
