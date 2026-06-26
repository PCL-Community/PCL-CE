// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Windows.Controls;

namespace PCL;

public partial class PageOnlineLeft : IRefreshable
{
    public FormMain.PageSubType PageID => pageID;
    private FormMain.PageSubType pageID = FormMain.PageSubType.OnlineLobby;
    private readonly Dictionary<FormMain.PageSubType, MyPageRight> pages = new();

    public PageOnlineLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => SelectCurrentItem();
    }

    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetId = id ?? pageID;
        if (!pages.TryGetValue(targetId, out var page))
        {
            page = targetId switch
            {
                FormMain.PageSubType.OnlineServerList => new PageOnlineServerList(),
                FormMain.PageSubType.OnlineFriendList => new PageOnlineFriendList(),
                FormMain.PageSubType.OnlineAddFriend => new PageOnlineAddFriend(),
                _ => new PageOnlineBlank(targetId)
            };
            pages[targetId] = page;
        }

        return page;
    }

    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id) return;
        pageID = id;
        PageChangeRun((MyPageRight)PageGet(id));
    }

    public void SetPage(FormMain.PageSubType id)
    {
        pageID = id;
        SelectCurrentItem();
    }

    private void SelectCurrentItem()
    {
        foreach (var item in PanItem.Children)
        {
            if (item is MyListItem listItem &&
                listItem.Tag is not null &&
                ModBase.Val(listItem.Tag) == (double)pageID)
            {
                listItem.SetChecked(true, false, false);
                return;
            }
        }
    }

    public void Refresh()
    {
        Refresh(pageID);
    }

    private void RefreshButton_Click(object sender, EventArgs e)
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType id)
    {
        if (PageGet(id) is IRefreshable refreshable)
            refreshable.Refresh();
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}
