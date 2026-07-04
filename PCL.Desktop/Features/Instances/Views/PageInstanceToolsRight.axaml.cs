// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceToolsRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstancePageSubType _page;

    public PageInstanceToolsRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
    }

    public event EventHandler<string>? OpenFolderRequested;

    public void SetContext(LaunchInstanceInfo instance, InstancePageSubType page)
    {
        _instance = instance;
        _page = page;
        Reload();
    }

    public void Reload()
    {
        if (_instance is null || this.FindControl<StackPanel>("PanMain") is not { } panel)
            return;

        panel.Children.Clear();
        switch (_page)
        {
            case InstancePageSubType.Mods:
            case InstancePageSubType.ResourcePacks:
            case InstancePageSubType.Shaders:
            case InstancePageSubType.Schematics:
                AddFolderPage(panel, _instance, _page);
                break;
        }
    }

    private void AddFolderPage(StackPanel panel, LaunchInstanceInfo instance, InstancePageSubType page)
    {
        string root = GetMinecraftRootFromInstance(instance);
        string relativePath = GetFolderRelativePath(page);
        string title = GetPageTitle(page);
        string folder = string.IsNullOrWhiteSpace(relativePath)
            ? root
            : Path.Combine(root, relativePath);
        int itemCount = CountFileSystemEntries(folder);

        panel.Children.Add(CreateInfoCard(
            title,
            $"{GetPageDescription(page)}\n当前路径：{folder}\n已找到 {itemCount} 个项目。",
            ("打开文件夹", () => OpenFolderRequested?.Invoke(this, folder)),
            ("刷新", Reload)));
    }

    private static MyCard CreateInfoCard(string title, string text, params (string Text, Action Action)[] buttons)
    {
        MyCard card = new()
        {
            Title = title,
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        StackPanel stack = new()
        {
            Margin = new Thickness(25d, 40d, 25d, 15d)
        };
        stack.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 0d, 0d, 12d)
        });

        WrapPanel buttonPanel = new()
        {
            Margin = new Thickness(0d, -5d, -20d, 0d)
        };
        foreach ((string textValue, Action action) in buttons)
        {
            MyButton button = new()
            {
                Text = textValue,
                MinWidth = 140d,
                Height = 35d,
                Padding = new Thickness(13d, 0d),
                Margin = new Thickness(0d, 7d, 20d, 0d)
            };
            button.Click += (_, _) => action();
            buttonPanel.Children.Add(button);
        }

        stack.Children.Add(buttonPanel);
        card.Children.Add(stack);
        return card;
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo versionsDirectory = versionDirectory.Parent
            ?? throw new InvalidOperationException("无法确定 versions 目录。");
        return versionsDirectory.Parent?.FullName
               ?? throw new InvalidOperationException("无法确定 Minecraft 根目录。");
    }

    private static string GetFolderRelativePath(InstancePageSubType page) =>
        page switch
        {
            InstancePageSubType.Saves => "saves",
            InstancePageSubType.Screenshots => "screenshots",
            InstancePageSubType.Mods => "mods",
            InstancePageSubType.ResourcePacks => "resourcepacks",
            InstancePageSubType.Shaders => "shaderpacks",
            InstancePageSubType.Schematics => "schematics",
            InstancePageSubType.Servers => string.Empty,
            _ => string.Empty
        };

    private static int CountFileSystemEntries(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? Directory.EnumerateFileSystemEntries(folder).Count() : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static string GetPageTitle(InstancePageSubType page) =>
        page switch
        {
            InstancePageSubType.Saves => "存档",
            InstancePageSubType.Screenshots => "截图",
            InstancePageSubType.Mods => "Mod",
            InstancePageSubType.ResourcePacks => "资源包",
            InstancePageSubType.Shaders => "光影",
            InstancePageSubType.Schematics => "投影",
            InstancePageSubType.Servers => "服务器",
            _ => "资源"
        };

    private static string GetPageDescription(InstancePageSubType page) =>
        page switch
        {
            InstancePageSubType.Saves => "管理当前 Minecraft 根目录下的游戏存档。",
            InstancePageSubType.Screenshots => "查看当前 Minecraft 根目录下的截图文件。",
            InstancePageSubType.Mods => "管理当前 Minecraft 根目录下的 Mod 文件。",
            InstancePageSubType.ResourcePacks => "管理当前 Minecraft 根目录下的资源包。",
            InstancePageSubType.Shaders => "管理当前 Minecraft 根目录下的光影包。",
            InstancePageSubType.Schematics => "管理当前 Minecraft 根目录下的投影文件。",
            InstancePageSubType.Servers => "管理当前 Minecraft 根目录下的服务器列表文件。",
            _ => "管理当前 Minecraft 根目录下的资源文件。"
        };
}
