// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

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
    Online = 7,
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
        PageId = SetupPageSubType.Online;
        AttachedToVisualTree += (_, _) =>
        {
            if (_isLoadedOnce)
                return;

            _isLoadedOnce = true;
            Required<MyListItem>("ItemOnlineAccount").SetChecked(true, user: false);
        };
    }

    public event EventHandler<SetupPageChangedEventArgs>? PageChanged;

    public event EventHandler<MyPageRight>? PageCreated;

    public SetupPageSubType PageId { get; private set; }

    public MyPageRight GetOrCreateCurrentPage() => PageGet(PageId);

    public void ScrollAccountIntoView()
    {
        Required<MyScrollViewer>("PanBack").Offset = Vector.Zero;
        Required<MyListItem>("ItemOnlineAccount").BringIntoView();
    }

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

        MyPageRight created = page switch
        {
            SetupPageSubType.Online => new PageSetupOnline(),
            SetupPageSubType.Launch => new PageSetupLaunch(),
            SetupPageSubType.Ui => new PageSetupUI(),
            SetupPageSubType.GameManage => new PageSetupGameManage(),
            SetupPageSubType.About => new PageSetupAbout(),
            SetupPageSubType.Log => new PageSetupLog(),
            SetupPageSubType.Feedback => new PageSetupFeedback(),
            SetupPageSubType.Update => new PageSetupUpdate(),
            SetupPageSubType.Java => new PageSetupJava(),
            SetupPageSubType.LauncherMisc => new PageSetupLauncherMisc(),
            SetupPageSubType.LauncherLanguage => new PageSetupLauncherLanguage(),
            SetupPageSubType.Plugin => new PageSetupPlugin(),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "未知的设置页面。")
        };
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
        page = SetupPageSubType.Online;
        int value = tag switch
        {
            int intValue => intValue,
            double doubleValue => (int)Math.Round(doubleValue),
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => int.MinValue
        };
        if (!Enum.IsDefined(typeof(SetupPageSubType), value))
            return false;

        page = (SetupPageSubType)value;
        return true;
    }

    private static string GetPageTitle(SetupPageSubType page) => page switch
    {
        SetupPageSubType.Online => "账户",
        SetupPageSubType.Launch => "启动",
        SetupPageSubType.Java => "Java",
        SetupPageSubType.GameManage => "管理",
        SetupPageSubType.Ui => "个性化",
        SetupPageSubType.LauncherLanguage => "语言",
        SetupPageSubType.LauncherMisc => "杂项",
        SetupPageSubType.Plugin => "插件",
        SetupPageSubType.About => "软件信息",
        SetupPageSubType.Update => "软件更新",
        SetupPageSubType.Feedback => "反馈",
        SetupPageSubType.Log => "查看日志",
        _ => "设置"
    };
}
