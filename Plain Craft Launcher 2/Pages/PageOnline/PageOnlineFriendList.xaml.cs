// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageOnlineFriendList : MyPageRight, IRefreshable
{
    private bool _hasLoaded;
    private bool _isRefreshing;

    public PageOnlineFriendList()
    {
        InitializeComponent();
        PageEnter += () =>
        {
            if (!_hasLoaded)
                _ = RefreshAsync();
        };
    }

    public void Refresh()
    {
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        _hasLoaded = true;
        Load.Visibility = Visibility.Visible;
        LabEmpty.Visibility = Visibility.Collapsed;
        PanFriends.Children.Clear();

        try
        {
            var friends = await Task.Run(() =>
                PCL.Online.XboxSocialService.GetFriendsAsync().GetAwaiter().GetResult());
            foreach (var friend in friends)
                PanFriends.Children.Add(CreateFriendItem(friend));
            LabEmpty.Text = friends.Count == 0
                ? PCL.Online.XboxSocialService.LastFailureReason ?? Lang.Text("Online.Friend.List.Empty")
                : "";
            LabEmpty.Visibility = friends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Online.Friend.List.LoadFailed"), ModBase.LogLevel.Hint);
            LabEmpty.Text = Lang.Text("Online.Friend.List.LoadFailed");
            LabEmpty.Visibility = Visibility.Visible;
        }
        finally
        {
            Load.Visibility = Visibility.Collapsed;
            _isRefreshing = false;
        }
    }

    private static UIElement CreateFriendItem(PCL.Online.XboxFriendInfo friend)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = friend.Nickname,
            FontSize = 14,
            FontWeight = FontWeights.Bold
        });
        text.Children.Add(new TextBlock
        {
            Text = Lang.Text("Online.Friend.List.Nickname", friend.Nickname),
            FontSize = 12,
            Opacity = 0.72
        });

        var status = new TextBlock
        {
            Text = Lang.Text("Online.Friend.List.PclStatus", friend.PclOnlineText),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = friend.PclOnline ? 1d : 0.65d
        };
        Grid.SetColumn(status, 1);
        grid.Children.Add(text);
        grid.Children.Add(status);
        return grid;
    }
}
