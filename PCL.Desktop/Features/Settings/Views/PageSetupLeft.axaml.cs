// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Hosting;

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

public sealed class SetupPageChangedEventArgs(SetupPageSubType pageId, MyPageRight page, string? hostPageId = null) : EventArgs
{
    public SetupPageSubType PageId { get; } = pageId;

    public string? HostPageId { get; } = hostPageId;

    public MyPageRight Page { get; } = page;
}

public partial class PageSetupLeft : MyPageLeft
{
    private const string DefaultHostSettingsGroupId = "pcl.settings.extensions";
    private const string LauncherHostSettingsGroupId = HostSettingsPageGroupIds.Launcher;

    private static readonly HostSettingsPageGroupDescriptor DefaultHostSettingsGroup = new(
        DefaultHostSettingsGroupId,
        "扩展",
        "lucide/puzzle",
        500,
        "由 HostModule 或插件注入的设置页。");

    private static readonly HostSettingsPageGroupDescriptor LauncherHostSettingsGroup = new(
        LauncherHostSettingsGroupId,
        "启动器",
        "lucide/monitor-cog",
        0,
        "位于启动器设置分类内的 HostModule 页面。");

    private readonly Dictionary<SetupPageSubType, MyPageRight> _pages = [];
    private readonly Dictionary<string, MyPageRight> _hostPages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HostSettingsPageDescriptor> _hostPageMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<HostSettingsPageDescriptor> _hostSettingsPages;
    private readonly IReadOnlyList<HostSettingsPageGroupDescriptor> _hostSettingsGroups;
    private bool _isLoadedOnce;
    private string? _hostPageId;

    public PageSetupLeft()
    {
        AvaloniaXamlLoader.Load(this);
        _hostSettingsGroups = DesktopHost.Current.SettingsPageGroups.Groups;
        _hostSettingsPages = DesktopHost.Current.SettingsPages.Pages;
        foreach (HostSettingsPageDescriptor page in _hostSettingsPages)
            _hostPageMap[page.Id] = page;
        RegisterHostSettingsPages();
        AnimatedControl = Required<Control>("PanItem");
        InitializeRegisteredPageTags();
        PageId = SetupPageSubType.Launch;
        AttachedToVisualTree += (_, _) =>
        {
            if (this.FindControl<StackPanel>("PanItem") is { } panel)
                DesktopHostUiComposition.Instance.RegisterSlot("pcl.page.settings", "sidebar.after-plugin", panel);
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            Required<MyListItem>("ItemLaunch").SetChecked(true, user: false);
        };
        DetachedFromVisualTree += (_, _) =>
            DesktopHostUiComposition.Instance.UnregisterSlot("pcl.page.settings", "sidebar.after-plugin");
    }

    public event EventHandler<SetupPageChangedEventArgs>? PageChanged;

    public event EventHandler<MyPageRight>? PageCreated;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ResetRequested;

    public SetupPageSubType PageId { get; private set; }

    public string? HostPageId => _hostPageId;

    public MyPageRight GetOrCreateCurrentPage() =>
        _hostPageId is null ? PageGet(PageId) : PageGetHost(_hostPageId);

    public void Reset(object? sender, EventArgs e)
    {
        if (sender is not MyIconButton button || !TryReadPage(button.Tag, out SetupPageSubType page))
            return;

        MyPageRight target = PageGet(page);
        PageChange(page, force: true);
        void Complete(bool confirmed)
        {
            if (!confirmed)
                return;

            if (target is PageSetupLauncherLanguage languagePage)
                languagePage.Reset();
            else
                LauncherSettingsPageBinder.ResetPage(target);
        }

        SettingsConfirmRequestedEventArgs args = new(
            "初始化设置",
            $"确定要将“{GetPageTitle(page)}”恢复为默认设置吗？",
            Complete,
            primaryButton: "初始化",
            isWarn: true);
        if (ResetRequested is { } resetRequested)
            resetRequested.Invoke(this, args);
        else
            Complete(true);
    }

    public void Refresh(object? sender, EventArgs e)
    {
        if (sender is not MyIconButton button)
            return;

        if (TryReadPage(button.Tag, out SetupPageSubType page))
        {
            MyPageRight target = PageGet(page);
            PageChange(page, force: true);
            if (target is IRefreshableSettingsPage refreshable)
                refreshable.RefreshPage();
        }
        else if (TryReadHostPage(button.Tag, out string? hostPageId) && hostPageId is not null)
        {
            MyPageRight target = PageGetHost(hostPageId);
            PageChangeHost(hostPageId, force: true);
            if (target is IRefreshableSettingsPage refreshable)
                refreshable.RefreshPage();
        }
    }

    private void PageCheck(object senderRaw, RouteEventArgs e)
    {
        if (senderRaw is not MyListItem item)
            return;

        if (TryReadHostPage(item.Tag, out string? hostPageId) && hostPageId is not null)
            PageChangeHost(hostPageId);
        else if (TryReadPage(item.Tag, out SetupPageSubType page))
            PageChange(page);
    }

    public MyPageRight PageGet(SetupPageSubType page)
    {
        if (page == SetupPageSubType.Plugin && _hostSettingsPages.Count > 0)
            return PageGetHost(_hostSettingsPages[0].Id);

        if (_pages.TryGetValue(page, out MyPageRight? cached))
            return cached;

        MyPageRight created = SetupPageRegistry.CreatePage(page);
        _pages[page] = created;
        PageCreated?.Invoke(this, created);
        return created;
    }

    private MyPageRight PageGetHost(string hostPageId)
    {
        if (_hostPages.TryGetValue(hostPageId, out MyPageRight? cached))
            return cached;
        if (!_hostPageMap.TryGetValue(hostPageId, out HostSettingsPageDescriptor? descriptor))
            throw new InvalidOperationException($"Host 设置页未注册：{hostPageId}");

        MyPageRight created = HostSettingsPageFactory.Create(descriptor);
        _hostPages[hostPageId] = created;
        PageCreated?.Invoke(this, created);
        return created;
    }

    public void PageChange(SetupPageSubType page, bool force = false)
    {
        if (!force && _hostPageId is null && PageId == page)
            return;

        _hostPageId = null;
        PageId = page;
        MyPageRight target = PageGet(page);
        PageChanged?.Invoke(this, new SetupPageChangedEventArgs(page, target));
    }

    private void PageChangeHost(string hostPageId, bool force = false)
    {
        if (!force && string.Equals(_hostPageId, hostPageId, StringComparison.OrdinalIgnoreCase))
            return;

        _hostPageId = hostPageId;
        PageId = SetupPageSubType.Plugin;
        MyPageRight target = PageGetHost(hostPageId);
        PageChanged?.Invoke(this, new SetupPageChangedEventArgs(SetupPageSubType.Plugin, target, hostPageId));
    }

    private T Required<T>(string name)
        where T : Control =>
        this.FindControl<T>(name)
        ?? throw new InvalidOperationException($"PageSetupLeft 缺少控件：{name}");

    private bool TryReadPage(object? tag, out SetupPageSubType page)
    {
        page = SetupPageSubType.Launch;
        if (tag is SetupPageSubType typedPage && IsPageDefined(typedPage))
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
        if (!IsPageDefined((SetupPageSubType)value))
            return false;

        page = (SetupPageSubType)value;
        return true;
    }

    private bool TryReadHostPage(object? tag, out string? hostPageId)
    {
        hostPageId = null;
        if (tag is not HostSettingsPageTag hostPageTag || !_hostPageMap.ContainsKey(hostPageTag.Id))
            return false;

        hostPageId = hostPageTag.Id;
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

    private void RegisterHostSettingsPages()
    {
        if (_hostSettingsPages.Count == 0 || this.FindControl<Panel>("PanItem") is not { } panel)
            return;

        int insertIndex = panel.Children
            .Select((child, index) => (child, index))
            .FirstOrDefault(pair => pair.child is Control { Name: "TextAboutCategory" })
            .index;
        if (insertIndex <= 0)
            insertIndex = panel.Children.Count;

        HostSettingsGroupView[] groups = BuildHostSettingsGroups();
        HostSettingsGroupView? launcherGroup = groups.FirstOrDefault(group =>
            string.Equals(group.Descriptor.Id, LauncherHostSettingsGroupId, StringComparison.OrdinalIgnoreCase));
        if (launcherGroup is not null)
        {
            foreach (HostSettingsPageDescriptor page in launcherGroup.Pages)
                panel.Children.Insert(insertIndex++, CreateHostPageItem(page));
        }

        foreach (HostSettingsGroupView group in groups)
        {
            if (ReferenceEquals(group, launcherGroup))
                continue;
            panel.Children.Insert(insertIndex++, CreateHostGroupLabel(group.Descriptor));
            foreach (HostSettingsPageDescriptor page in group.Pages)
                panel.Children.Insert(insertIndex++, CreateHostPageItem(page));
        }
    }

    /// <summary>Rebuilds dynamic HostModule settings entries after a runtime visibility switch changes.</summary>
    internal void RefreshHostSettingsPages()
    {
        if (this.FindControl<Panel>("PanItem") is not { } panel)
            return;

        for (int index = panel.Children.Count - 1; index >= 0; index--)
        {
            Control child = panel.Children[index];
            bool isHostGroupLabel = child.Name?.StartsWith("TextHostSettingsGroup_", StringComparison.Ordinal) == true;
            bool isHostPageItem = child is MyListItem item && TryReadHostPage(item.Tag, out _);
            if (isHostGroupLabel || isHostPageItem)
                panel.Children.RemoveAt(index);
        }

        RegisterHostSettingsPages();
    }

    private HostSettingsGroupView[] BuildHostSettingsGroups()
    {
        Dictionary<string, HostSettingsPageGroupDescriptor> groupMap = new(StringComparer.OrdinalIgnoreCase);
        foreach (HostSettingsPageGroupDescriptor group in _hostSettingsGroups)
            groupMap[group.Id] = group;

        Dictionary<string, List<HostSettingsPageDescriptor>> pagesByGroup = new(StringComparer.OrdinalIgnoreCase);
        foreach (HostSettingsPageDescriptor page in _hostSettingsPages)
        {
            if (page.VisibilityPredicate is not null)
            {
                bool isVisible;
                try
                {
                    isVisible = page.VisibilityPredicate();
                }
                catch
                {
                    isVisible = false;
                }

                if (!isVisible)
                    continue;
            }

            string groupId = !string.IsNullOrWhiteSpace(page.GroupId) && groupMap.ContainsKey(page.GroupId)
                ? page.GroupId
                : string.Equals(page.GroupId, LauncherHostSettingsGroupId, StringComparison.OrdinalIgnoreCase)
                    ? LauncherHostSettingsGroupId
                : DefaultHostSettingsGroupId;
            if (!pagesByGroup.TryGetValue(groupId, out List<HostSettingsPageDescriptor>? pages))
            {
                pages = [];
                pagesByGroup[groupId] = pages;
            }

            pages.Add(page);
        }

        List<HostSettingsGroupView> groups = [];
        foreach ((string groupId, List<HostSettingsPageDescriptor> pages) in pagesByGroup)
        {
            HostSettingsPageGroupDescriptor descriptor = groupMap.TryGetValue(groupId, out HostSettingsPageGroupDescriptor? group)
                ? group
                : string.Equals(groupId, LauncherHostSettingsGroupId, StringComparison.OrdinalIgnoreCase)
                    ? LauncherHostSettingsGroup
                    : DefaultHostSettingsGroup;
            groups.Add(new HostSettingsGroupView(
                descriptor,
                pages.OrderBy(static page => page.Order)
                    .ThenBy(static page => page.Title, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(static page => page.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray()));
        }

        return groups
            .OrderBy(static group => group.Descriptor.Order)
            .ThenBy(static group => group.Descriptor.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static group => group.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static TextBlock CreateHostGroupLabel(HostSettingsPageGroupDescriptor group) =>
        new()
        {
            Name = "TextHostSettingsGroup_" + SanitizeName(group.Id),
            Text = group.Title,
            Margin = new Thickness(13, 5, 5, 3),
            Opacity = 0.6,
            FontSize = 12
        };

    private MyListItem CreateHostPageItem(HostSettingsPageDescriptor page)
    {
        MyListItem item = new()
        {
            Name = "ItemHostSettings_" + SanitizeName(page.Id),
            IsScaleAnimationEnabled = false,
            Tag = new HostSettingsPageTag(page.Id),
            MinPaddingRight = 35d,
            Height = 36d,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Title = page.Title,
            Type = MyListItem.CheckType.RadioBox,
            LogoScale = 0.95d,
            SvgIcon = page.Icon
        };
        item.Check += PageCheck;
        return item;
    }

    private bool IsPageDefined(SetupPageSubType page) =>
        SetupPageRegistry.IsDefined(page) ||
        (page == SetupPageSubType.Plugin && _hostSettingsPages.Count > 0);

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

    private string GetPageTitle(SetupPageSubType page) =>
        page == SetupPageSubType.Plugin && _hostSettingsPages.Count > 0
            ? _hostSettingsPages[0].Title
            : SetupPageRegistry.GetTitle(page);

    private static string SanitizeName(string value)
    {
        Span<char> buffer = value.Length <= 256 ? stackalloc char[value.Length] : new char[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            buffer[i] = char.IsLetterOrDigit(c) ? c : '_';
        }

        return new string(buffer);
    }

    private sealed record HostSettingsPageTag(string Id);

    private sealed record HostSettingsGroupView(
        HostSettingsPageGroupDescriptor Descriptor,
        IReadOnlyList<HostSettingsPageDescriptor> Pages);
}
