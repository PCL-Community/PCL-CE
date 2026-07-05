// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Settings.Views;

public enum SetupPageSubType
{
    Launch = 0,
    Ui = 1,
    GameManage = 2,
    About = 4,
    Log = 5,
    Feedback = 6,
    Update = 8,
    Java = 9,
    LauncherMisc = 10,
    LauncherLanguage = 11,
    Plugin = 12
}

public sealed class SetupPageChangedEventArgs(SetupPageSubType pageId, MyPageRight page) : EventArgs
{
    public SetupPageSubType PageId { get; } = pageId;

    public MyPageRight Page { get; } = page;
}

public partial class PageSetupLeft : MyPageLeft
{
    private readonly Dictionary<SetupPageSubType, MyPageRight> _pages = [];
    private bool _isLoadedOnce;

    public PageSetupLeft()
    {
        AvaloniaXamlLoader.Load(this);
        AnimatedControl = Required<Control>("PanItem");
        InitializeRegisteredPageTags();
        PageId = SetupPageSubType.Launch;
        AttachedToVisualTree += (_, _) =>
        {
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            Required<MyListItem>("ItemLaunch").SetChecked(true, user: false);
        };
    }

    public event EventHandler<SetupPageChangedEventArgs>? PageChanged;

    public event EventHandler<MyPageRight>? PageCreated;

    public SetupPageSubType PageId { get; private set; }

    public MyPageRight GetOrCreateCurrentPage() => PageGet(PageId);

    public void Reset(object? sender, EventArgs e)
    {
        if (sender is MyIconButton button && TryReadPage(button.Tag, out SetupPageSubType page))
            PageChange(page);
    }

    public void Refresh(object? sender, EventArgs e)
    {
        if (sender is MyIconButton button && TryReadPage(button.Tag, out SetupPageSubType page))
        {
            MyPageRight target = PageGet(page);
            PageChange(page, force: true);
            if (target is IRefreshableSettingsPage refreshable)
                refreshable.RefreshPage();
        }
    }

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is MyListItem item && TryReadPage(item.Tag, out SetupPageSubType page))
            PageChange(page);
    }

    public MyPageRight PageGet(SetupPageSubType page)
    {
        if (_pages.TryGetValue(page, out MyPageRight? cached))
            return cached;

        MyPageRight created = SetupPageRegistry.CreatePage(page);
        _pages[page] = created;
        PageCreated?.Invoke(this, created);
        return created;
    }

    public void PageChange(SetupPageSubType page, bool force = false)
    {
        if (!force && PageId == page)
            return;

        PageId = page;
        MyPageRight target = PageGet(page);
        PageChanged?.Invoke(this, new SetupPageChangedEventArgs(page, target));
    }

    private T Required<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"PageSetupLeft 缺少控件：{name}");

    private static bool TryReadPage(object? tag, out SetupPageSubType page)
    {
        page = SetupPageSubType.Launch;
        if (tag is SetupPageSubType typedPage && SetupPageRegistry.IsDefined(typedPage))
        {
            page = typedPage;
            return true;
        }

        int value = tag switch
        {
            int intValue => intValue,
            double doubleValue => (int)Math.Round(doubleValue),
            string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) => parsed,
            _ => int.MinValue
        };
        if (!SetupPageRegistry.IsDefined((SetupPageSubType)value))
            return false;

        page = (SetupPageSubType)value;
        return true;
    }

    private void InitializeRegisteredPageTags()
    {
        foreach (MyListItem item in GetItems())
        {
            if (TryReadPage(item.Tag, out SetupPageSubType page))
                item.Tag = page;

            foreach (MyIconButton button in item.Buttons)
            {
                if (TryReadPage(button.Tag, out SetupPageSubType buttonPage))
                    button.Tag = buttonPage;
            }
        }
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

    private static string GetPageTitle(SetupPageSubType page) => SetupPageRegistry.GetTitle(page);
}
