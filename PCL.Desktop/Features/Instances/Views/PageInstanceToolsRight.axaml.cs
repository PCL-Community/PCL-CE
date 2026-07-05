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
        if (InstancePageRegistry.UsesGenericFolderPage(_page))
            AddFolderPage(panel, _instance, _page);
    }

    private void AddFolderPage(StackPanel panel, LaunchInstanceInfo instance, InstancePageSubType page)
    {
        string root = GetMinecraftRootFromInstance(instance);
        string relativePath = InstancePageRegistry.GetFolderRelativePath(page);
        string title = InstancePageRegistry.GetTitle(page);
        string folder = string.IsNullOrWhiteSpace(relativePath)
            ? root
            : Path.Combine(root, relativePath);
        int itemCount = CountFileSystemEntries(folder);

        panel.Children.Add(CreateInfoCard(
            title,
            $"{InstancePageRegistry.GetDescription(page)}\n当前路径：{folder}\n已找到 {itemCount} 个项目。",
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

}
