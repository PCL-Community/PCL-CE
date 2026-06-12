// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System.Windows.Controls;

namespace PCL;

public partial class PageOnlineLeft
{
    public FormMain.PageSubType PageID => pageID;
    private FormMain.PageSubType pageID = FormMain.PageSubType.OnlineLobby;

    public PageOnlineLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => ItemLobby.SetChecked(true, false, false);
    }

    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    public object PageGet(FormMain.PageSubType? id = null)
    {
        return new PageOnline();
    }

    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id) return;
        pageID = id;
        PageChangeRun((MyPageRight)PageGet(id));
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        if (ModMain.frmMain.PanMainRight.Child is MyPageRight current)
            current.PageOnExit();
        ModMain.frmMain.PanMainRight.Child = target;
        target.PageOnEnter();
    }
}
