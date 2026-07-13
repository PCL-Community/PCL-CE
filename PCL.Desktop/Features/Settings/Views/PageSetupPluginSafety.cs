// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

internal sealed class PageSetupPluginSafety : PluginSettingsPageBase
{
    private readonly TextBlock _statusLabel = CreateMutedText("正在读取安全模式设置……");
    private readonly StackPanel _safetyPanel = new() { Spacing = 8 };
    private CheckBox? _pluginSafeModeBox;
    private CheckBox? _uiSafeModeBox;
    private bool _isRefreshing;

    public PageSetupPluginSafety(HostSettingsPageDescriptor descriptor)
        : base(descriptor)
    {
        AddHeaderCard();
        MyCard card = CreateCard("安全开关");
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(_statusLabel);
        _pluginSafeModeBox = new CheckBox
        {
            Content = "Plugin Safe Mode（不加载第三方插件）",
            FontSize = 13d
        };
        _pluginSafeModeBox.IsCheckedChanged += (_, _) => PersistSafetyFromUi();
        _uiSafeModeBox = new CheckBox
        {
            Content = "UI Safe Mode（跳过 modify/replace/raw 类 Patch）",
            FontSize = 13d
        };
        _uiSafeModeBox.IsCheckedChanged += (_, _) => PersistSafetyFromUi();
        _safetyPanel.Children.Add(_pluginSafeModeBox);
        _safetyPanel.Children.Add(_uiSafeModeBox);
        content.Children.Add(_safetyPanel);
        card.Children.Add(content);
        PanMain.Children.Add(card);
        RefreshPage();
    }

    public override void RefreshPage()
    {
        if (!TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null)
        {
            _statusLabel.Text = "插件目录未初始化。";
            SetUnavailable(_safetyPanel);
            return;
        }

        try
        {
            PluginSafetySettings safety = catalog.Safety;
            _isRefreshing = true;
            if (_pluginSafeModeBox is not null)
                _pluginSafeModeBox.IsChecked = safety.PluginSafeMode;
            if (_uiSafeModeBox is not null)
                _uiSafeModeBox.IsChecked = safety.UiSafeMode;
            _statusLabel.Text =
                $"PluginSafe={(safety.PluginSafeMode ? "开" : "关")} · UiSafe={(safety.UiSafeMode ? "开" : "关")} · {FormatMarketState(catalog)}";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "刷新失败：" + ex.Message;
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void PersistSafetyFromUi()
    {
        if (_isRefreshing || !TryGetCatalog(out IPluginCatalogService? catalog) || catalog is null ||
            _pluginSafeModeBox is null || _uiSafeModeBox is null)
        {
            return;
        }

        try
        {
            catalog.SetSafety(new PluginSafetySettings(
                _pluginSafeModeBox.IsChecked == true,
                _uiSafeModeBox.IsChecked == true));
            ShowInfo("安全模式设置已保存");
        }
        catch (Exception ex)
        {
            ShowWarning(ex.Message);
        }
        finally
        {
            RefreshPage();
        }
    }
}
