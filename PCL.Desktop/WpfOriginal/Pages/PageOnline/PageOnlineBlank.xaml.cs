// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

namespace PCL;

public partial class PageOnlineBlank : MyPageRight
{
    public PageOnlineBlank(FormMain.PageSubType pageType)
    {
        InitializeComponent();
        LabPageTitle.Text = PCL.Core.App.Localization.Lang.Text(pageType switch
        {
            FormMain.PageSubType.OnlineLobby => "Online.Nav.Lobby",
            FormMain.PageSubType.OnlineCreateRoom => "Online.Nav.CreateRoom",
            FormMain.PageSubType.OnlineServerList => "Online.Nav.ServerList",
            FormMain.PageSubType.OnlineFriendList => "Online.Nav.FriendList",
            FormMain.PageSubType.OnlineAddFriend => "Online.Nav.AddFriend",
            _ => "Main.TopTitle.Online"
        });
    }
}
