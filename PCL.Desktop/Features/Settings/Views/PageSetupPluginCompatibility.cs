// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

/// <summary>Offline compatibility observations generated from plugin patch conflicts.</summary>
internal sealed class PageSetupPluginCompatibility : PluginSettingsPageBase
{
    private readonly TextBlock _statusLabel = CreateMutedText("尚未读取兼容性记录。");
    private readonly StackPanel _recordPanel = new() { Spacing = 8d };

    public PageSetupPluginCompatibility(HostSettingsPageDescriptor descriptor)
        : base(descriptor)
    {
        AddHeaderCard();
        MyCard card = CreateCard("离线兼容性记录");
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(_statusLabel);
        content.Children.Add(_recordPanel);
        card.Children.Add(content);
        PanMain.Children.Add(card);
        RefreshPage();
    }

    public override void RefreshPage()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
        {
            _statusLabel.Text = "插件目录未初始化。";
            SetUnavailable(_recordPanel);
            return;
        }

        try
        {
            IReadOnlyList<PluginCompatibilityRecord> records = catalog.ListCompatibility();
            _statusLabel.Text = records.Count == 0
                ? "暂无离线兼容性观察记录。"
                : $"已记录 {records.Count} 条离线兼容性观察。";
            _recordPanel.Children.Clear();
            if (records.Count == 0)
            {
                _recordPanel.Children.Add(CreateMutedText("当两个插件的 UI Patch 发生错误级冲突时，会在这里留下本地记录。"));
                return;
            }

            foreach (PluginCompatibilityRecord record in records
                .OrderByDescending(static value => value.ObservedAt)
                .Take(100))
            {
                Border border = CreateRowBorder();
                StackPanel content = new() { Spacing = 3d };
                content.Children.Add(new TextBlock
                {
                    Text = $"{record.PluginA}  ↔  {record.PluginB}",
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 14d
                });
                content.Children.Add(CreateMutedText($"{record.Result} · {record.Target} · {record.ObservedAt.LocalDateTime:g}"));
                content.Children.Add(new TextBlock
                {
                    Text = record.Evidence,
                    FontSize = 12d,
                    Opacity = 0.75,
                    TextWrapping = TextWrapping.Wrap
                });
                border.Child = content;
                _recordPanel.Children.Add(border);
            }
            if (records.Count > 100)
                _recordPanel.Children.Add(CreateMutedText($"仅显示最新 100 条（共 {records.Count} 条）。"));
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "读取兼容性记录失败：" + ex.Message;
            _recordPanel.Children.Clear();
        }
    }
}
