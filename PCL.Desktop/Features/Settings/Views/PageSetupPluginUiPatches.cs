// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

/// <summary>Displays the effective plugin UI patch plan and persists conflict resolutions.</summary>
internal sealed class PageSetupPluginUiPatches : PluginSettingsPageBase
{
    private readonly TextBlock _statusLabel = CreateMutedText("尚未计算 UI Patch 计划。");
    private readonly StackPanel _resultPanel = new() { Spacing = 8d };

    public PageSetupPluginUiPatches(HostSettingsPageDescriptor descriptor)
        : base(descriptor)
    {
        AddHeaderCard();

        MyCard card = CreateCard("UI Patch 计划");
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(_statusLabel);
        MyButton applyButton = new()
        {
            Text = "重新应用",
            MinWidth = 96d,
            Height = 32d,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        applyButton.Click += (_, _) => RefreshPage();
        content.Children.Add(applyButton);
        content.Children.Add(_resultPanel);
        card.Children.Add(content);
        PanMain.Children.Add(card);
        RefreshPage();
    }

    public override void RefreshPage()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
        {
            _statusLabel.Text = "插件目录未初始化。";
            SetUnavailable(_resultPanel);
            return;
        }

        try
        {
            PluginUiPatchApplyResult result = catalog.ApplyUiPatches();
            _statusLabel.Text =
                $"UiSafe={(result.UiSafeMode ? "开" : "关")} · 已应用 {result.AppliedGlobalIds.Count} · " +
                $"视觉生效 {result.VisuallyAppliedGlobalIds.Count} · 冲突 {result.Conflicts.Count}";

            _resultPanel.Children.Clear();
            AddPatchIds("已应用", result.AppliedGlobalIds, "没有已应用的 Patch。");
            AddPatchIds("视觉已生效", result.VisuallyAppliedGlobalIds, "没有直接改变页面视觉的 Patch。");
            AddPatchIds("安全模式跳过", result.BlockedBySafeMode, "没有被安全模式拦截的 Patch。");
            AddPatchIds("冲突拦截", result.BlockedByConflict, "没有被冲突策略拦截的 Patch。");
            AddConflicts(catalog, result.Conflicts);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "读取 UI Patch 失败：" + ex.Message;
            _resultPanel.Children.Clear();
        }
    }

    private void AddPatchIds(string title, IReadOnlyList<string> ids, string emptyText)
    {
        _resultPanel.Children.Add(CreateSectionTitle(title));
        if (ids.Count == 0)
        {
            _resultPanel.Children.Add(CreateMutedText(emptyText));
            return;
        }

        foreach (string id in ids.Take(100))
            _resultPanel.Children.Add(CreateMutedText("• " + id));
        if (ids.Count > 100)
            _resultPanel.Children.Add(CreateMutedText($"另有 {ids.Count - 100} 条。"));
    }

    private void AddConflicts(IPluginCatalogService catalog, IReadOnlyList<PluginUiConflictSummary> conflicts)
    {
        _resultPanel.Children.Add(CreateSectionTitle("冲突"));
        if (conflicts.Count == 0)
        {
            _resultPanel.Children.Add(CreateMutedText("当前没有检测到冲突。"));
            return;
        }

        foreach (PluginUiConflictSummary conflict in conflicts)
        {
            Border border = CreateRowBorder(
                alpha: 80,
                red: string.Equals(conflict.Severity, "Error", StringComparison.OrdinalIgnoreCase) ? (byte)188 : (byte)180,
                green: string.Equals(conflict.Severity, "Error", StringComparison.OrdinalIgnoreCase) ? (byte)68 : (byte)140,
                blue: 55);
            StackPanel content = new() { Spacing = 5d };
            content.Children.Add(new TextBlock
            {
                Text = $"{conflict.Kind} · {conflict.Severity} · {conflict.Target}",
                FontWeight = FontWeight.SemiBold,
                FontSize = 13d
            });
            content.Children.Add(new TextBlock
            {
                Text = conflict.Message,
                FontSize = 12d,
                TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(CreateMutedText($"{conflict.LeftGlobalId}  ↔  {conflict.RightGlobalId}"));
            if (!string.IsNullOrWhiteSpace(conflict.Resolution))
                content.Children.Add(CreateMutedText("当前策略：" + conflict.Resolution));

            WrapPanel actions = CreateButtonWrap();
            actions.Children.Add(CreateResolutionButton(catalog, conflict, "禁用左侧", PluginConflictResolution.DisableLeft));
            actions.Children.Add(CreateResolutionButton(catalog, conflict, "禁用右侧", PluginConflictResolution.DisableRight));
            actions.Children.Add(CreateResolutionButton(catalog, conflict, "强制共存", PluginConflictResolution.ForceBoth));
            content.Children.Add(actions);
            border.Child = content;
            _resultPanel.Children.Add(border);
        }
    }

    private MyButton CreateResolutionButton(
        IPluginCatalogService catalog,
        PluginUiConflictSummary conflict,
        string text,
        PluginConflictResolution resolution)
    {
        MyButton button = new()
        {
            Text = text,
            MinWidth = 82d,
            Height = 30d,
            Margin = new Thickness(0d, 0d, 8d, 4d)
        };
        button.Click += (_, _) =>
        {
            try
            {
                catalog.ResolveUiConflict(conflict.LeftGlobalId, conflict.RightGlobalId, resolution);
                ShowInfo("冲突策略已保存");
            }
            catch (Exception ex)
            {
                ShowWarning("保存冲突策略失败：" + ex.Message);
            }
            finally
            {
                RefreshPage();
            }
        };
        return button;
    }
}
