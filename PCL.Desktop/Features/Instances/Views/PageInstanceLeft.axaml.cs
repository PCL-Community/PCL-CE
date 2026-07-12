// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Globalization;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public enum InstancePageSubType
{
    Overall = 0,
    Setup = 1,
    Export = 2,
    Saves = 3,
    Screenshots = 4,
    Mods = 5,
    ModsDisabled = 6,
    ResourcePacks = 7,
    Shaders = 8,
    Schematics = 9,
    Install = 10,
    Servers = 11
}

public partial class PageInstanceLeft : MyPageLeft
{
    private LaunchInstanceInfo? _instance;
    private bool _isModable;
    private bool _hasShaderSupport;
    private bool _hasSchematicSupport;

    public PageInstanceLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = this.FindControl<Control>("PanItem");
        InitializeRegisteredPageTags();
    }

    public event EventHandler<InstancePageSubType>? PageChanged;

    public event EventHandler<InstancePageSubType>? RefreshRequested;

    public event EventHandler? ResetRequested;

    public InstancePageSubType PageId { get; private set; } = InstancePageSubType.Overall;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        _isModable = InstanceDisplayHelper.IsModable(instance);
        _hasShaderSupport = _isModable && InstanceDisplayHelper.HasShaderSupport(instance);
        _hasSchematicSupport = _isModable && InstanceDisplayHelper.HasSchematicSupport(instance);
        RefreshResourceNavVisibility();
    }

    public InstancePageSubType NormalizePage(InstancePageSubType page)
    {
        if (page == InstancePageSubType.Mods && !_isModable)
            return InstancePageSubType.ModsDisabled;
        if (page == InstancePageSubType.Shaders && !_hasShaderSupport)
            return InstancePageSubType.Overall;
        if (page == InstancePageSubType.Schematics && !_hasSchematicSupport)
            return InstancePageSubType.Overall;
        if (page == InstancePageSubType.ModsDisabled && _isModable)
            return InstancePageSubType.Mods;
        return page;
    }

    public void PageChange(InstancePageSubType page, bool force = false)
    {
        page = NormalizePage(page);
        if (!force && PageId == page)
            return;

        PageId = page;
        PageChanged?.Invoke(this, page);
    }

    public void SelectPage(InstancePageSubType page)
    {
        page = NormalizePage(page);
        PageId = page;
        foreach (MyListItem item in GetItems())
        {
            if (TryGetPage(item, out InstancePageSubType itemPage))
                item.SetChecked(itemPage == page, user: false, animate: false);
        }
    }

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is MyListItem item && TryGetPage(item, out InstancePageSubType page))
            PageChange(page);
    }

    private void RefreshResourceNavVisibility()
    {
        // WPF PageVersionLeft:
        // - Mod only when a mod loader is installed (else show disabled stub)
        // - 光影 only when Iris/OptiFine/… present
        // - 投影 only when Litematica/… present
        if (this.FindControl<MyListItem>("ItemMod") is { } itemMod)
            itemMod.IsVisible = _instance is null || _isModable;

        if (this.FindControl<MyListItem>("ItemModDisabled") is { } itemModDisabled)
            itemModDisabled.IsVisible = _instance is not null && !_isModable;

        if (this.FindControl<MyListItem>("ItemShader") is { } itemShader)
            itemShader.IsVisible = _instance is null || _hasShaderSupport;

        if (this.FindControl<MyListItem>("ItemSchematic") is { } itemSchematic)
            itemSchematic.IsVisible = _instance is null || _hasSchematicSupport;
    }

    private void RefreshButton_Click(object? sender, EventArgs e)
    {
        if (sender is MyIconButton button && TryNormalizePageTag(button.Tag, out InstancePageSubType page))
            RefreshRequested?.Invoke(this, page);
    }

    private void Reset(object? sender, EventArgs e)
    {
        ResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<MyListItem> GetItems()
    {
        if (this.FindControl<Panel>("PanItem") is not { } panel)
            yield break;

        foreach (Control child in panel.Children)
        {
            if (child is MyListItem item)
                yield return item;
        }
    }

    private static bool TryGetPage(MyListItem item, out InstancePageSubType page)
    {
        return TryNormalizePageTag(item.Tag, out page);
    }

    private void InitializeRegisteredPageTags()
    {
        foreach (MyListItem item in GetItems())
        {
            if (TryNormalizePageTag(item.Tag, out InstancePageSubType page))
                item.Tag = page;

            foreach (MyIconButton button in item.Buttons)
            {
                if (TryNormalizePageTag(button.Tag, out InstancePageSubType buttonPage))
                    button.Tag = buttonPage;
            }
        }
    }

    private static bool TryNormalizePageTag(object? tag, out InstancePageSubType page)
    {
        page = InstancePageSubType.Overall;
        return tag switch
        {
            int value when InstancePageRegistry.IsDefined((InstancePageSubType)value) => SetPage((InstancePageSubType)value, out page),
            InstancePageSubType value when InstancePageRegistry.IsDefined(value) => SetPage(value, out page),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
                             InstancePageRegistry.IsDefined((InstancePageSubType)value) =>
                SetPage((InstancePageSubType)value, out page),
            _ => false
        };
    }

    private static bool SetPage(InstancePageSubType value, out InstancePageSubType page)
    {
        page = value;
        return true;
    }
}
