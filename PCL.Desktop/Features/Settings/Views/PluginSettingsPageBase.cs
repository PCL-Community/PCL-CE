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
using PCL.Desktop.Hosting;

namespace PCL.Desktop.Features.Settings.Views;

internal abstract class PluginSettingsPageBase : MyPageRight, IRefreshableSettingsPage
{
    protected PluginSettingsPageBase(HostSettingsPageDescriptor descriptor)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        PanMain = new StackPanel { Margin = new Thickness(25d, 25d, 25d, 10d) };
        PanBack = new MyScrollViewer
        {
            Name = "PanBack",
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = PanMain
        };
        PanScroll = PanBack;
        Content = PanBack;
    }

    protected HostSettingsPageDescriptor Descriptor { get; }

    protected StackPanel PanMain { get; }

    protected MyScrollViewer PanBack { get; }

    public abstract void RefreshPage();

    protected void AddHeaderCard()
    {
        MyCard card = CreateCard(Descriptor.Title);
        StackPanel content = CreateCardContent(spacing: 10d);
        content.Children.Add(new TextBlock
        {
            Name = "LabHostHeading",
            Text = Descriptor.Heading,
            FontSize = 20d,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Name = "LabHostDescription",
            Text = Descriptor.Description,
            FontSize = 13d,
            TextWrapping = TextWrapping.Wrap
        });
        foreach (HostSettingsHintDescriptor hint in Descriptor.Hints)
            content.Children.Add(CreateHint(hint));
        card.Children.Add(content);
        PanMain.Children.Add(card);
    }

    protected static MyCard CreateCard(string title, bool canSwap = false, bool isSwapped = false)
    {
        MyCard card = new()
        {
            Title = title,
            Margin = new Thickness(0d, 0d, 0d, 15d)
        };
        if (canSwap)
        {
            card.CanSwap = true;
            card.IsSwapped = isSwapped;
        }

        return card;
    }

    protected static StackPanel CreateCardContent(double spacing = 8d) =>
        new() { Margin = new Thickness(25d, 40d, 25d, 20d), Spacing = spacing };

    protected static TextBlock CreateMutedText(string text, double fontSize = 12d) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };

    protected static TextBlock CreateSectionTitle(string text) =>
        new()
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            FontSize = 13d,
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };

    protected static MyHint CreateHint(HostSettingsHintDescriptor hint) =>
        new()
        {
            Text = hint.Text,
            Theme = hint.Kind switch
            {
                HostSettingsHintKind.Warning => MyHint.Themes.Yellow,
                HostSettingsHintKind.Error => MyHint.Themes.Red,
                _ => MyHint.Themes.Blue
            },
            Margin = new Thickness(0d, 4d, 0d, 0d)
        };

    protected static Border CreateRowBorder(byte alpha = 40, byte red = 128, byte green = 128, byte blue = 128) =>
        new()
        {
            BorderBrush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 10, 12, 10)
        };

    protected static WrapPanel CreateButtonWrap() =>
        new()
        {
            ItemHeight = 35d,
            Orientation = Orientation.Horizontal
        };

    protected static bool TryGetCatalog(out IPluginCatalogService? catalog)
    {
        catalog = null;
        if (!PluginCatalogAccess.IsInitialized)
            return false;

        catalog = PluginCatalogAccess.Current;
        return true;
    }

    protected static void ShowInfo(string message) =>
        DesktopPluginHostNotifications.Instance.ShowInformation(message);

    protected static void ShowWarning(string message) =>
        DesktopPluginHostNotifications.Instance.ShowWarning(message);

    protected static string FormatMarketState(IPluginCatalogService catalog) =>
        catalog.IsRemoteMarketConfigured ? "远端市场已配置" : "远端市场 API 已预留（服务器未接入）";

    protected static void SetUnavailable(StackPanel target, string? detail = null)
    {
        target.Children.Clear();
        target.Children.Add(new MyHint
        {
            Text = detail ?? "当前构建未注入 PCL.Plugin 运行时；第三方 .pnp 管理不可用。使用 scripts/run-plugin-ui.ps1 可本地嵌入调试。",
            Theme = MyHint.Themes.Yellow
        });
    }
}
