// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Models;
using PCL.Plugin;

namespace PCL.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
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

    public MainWindowViewModel(DesktopEnvironmentSnapshot environment)
    {
        Environment = environment;
        RuntimeStatus = "运行环境已就绪";
        NavigationItems =
        [
            CreateNavigationItem("首页", "查看启动器状态与最近活动。"),
            CreateNavigationItem("版本管理", "管理本地 Minecraft 版本与启动档案。"),
            CreateNavigationItem("下载", "查找并安装游戏、模组与资源。"),
            CreateNavigationItem("联机", "查看好友与可加入的服务器。"),
            CreateNavigationItem("设置", "调整启动、下载与界面选项。"),
            CreateNavigationItem(PluginPreview.Name, "扩展能力正在准备中。", isComingSoon: true)
        ];

        Select(NavigationItems[0]);
    }

    public IReadOnlyList<NavigationItemViewModel> NavigationItems { get; }

    public DesktopEnvironmentSnapshot Environment { get; }

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

    private NavigationItemViewModel CreateNavigationItem(
        string title,
        string description,
        bool isComingSoon = false) =>
        new(title, description, isComingSoon, Select);

    private void Select(NavigationItemViewModel selected)
    {
        foreach (NavigationItemViewModel item in NavigationItems)
            item.IsSelected = ReferenceEquals(item, selected);

        SelectedTitle = selected.Title;
        SelectedDescription = selected.Description;
    }
}
