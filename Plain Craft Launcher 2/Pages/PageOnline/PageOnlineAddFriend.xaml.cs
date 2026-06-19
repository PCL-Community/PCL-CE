// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageOnlineAddFriend : MyPageRight, IRefreshable
{
    private PCL.Online.OnlineFriendProfile? _searchResult;

    public PageOnlineAddFriend()
    {
        InitializeComponent();
        SearchBox.TextChanged += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                CardSearchResult.Visibility = Visibility.Collapsed;
                _ = RefreshPassiveAsync();
            }
        };
        SearchBox.Search += (_, _) => _ = SearchAsync();
        PageEnter += () => _ = RefreshPassiveAsync();
    }

    public void Refresh()
    {
        _ = RefreshPassiveAsync();
    }

    private async Task SearchAsync()
    {
        var query = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            await RefreshPassiveAsync();
            return;
        }

        SetBusy(true);
        try
        {
            _searchResult = await PCL.Online.OnlineFriendService.SearchMinecraftProfileAsync(query);
            if (_searchResult is null)
            {
                CardSearchResult.Visibility = Visibility.Collapsed;
                ModMain.Hint(Lang.Text("Online.Friend.Add.NotFound"), ModMain.HintType.Critical);
                return;
            }

            LabSearchName.Text = _searchResult.Name;
            LabSearchId.Text = Lang.Text("Online.Friend.Add.ProfileId", _searchResult.ProfileId);
            CardSearchResult.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Online.Friend.ServiceUnavailable"), ModBase.LogLevel.Hint);
            ModMain.Hint(Lang.Text("Online.Friend.ServiceUnavailable"), ModMain.HintType.Critical);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BtnApply_Click(object sender, ModBase.RouteEventArgs e)
    {
        if (_searchResult is null)
            return;

        BtnApply.IsEnabled = false;
        try
        {
            var ok = await PCL.Online.OnlineFriendService.SendFriendRequestAsync(_searchResult);
            ModMain.Hint(ok
                    ? Lang.Text("Online.Friend.Add.ApplySent", _searchResult.Name)
                    : Lang.Text("Online.Friend.Add.ApplyFailed"),
                ok ? ModMain.HintType.Finish : ModMain.HintType.Critical);
            await RefreshPassiveAsync();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Online.Friend.Add.ApplyFailed"), ModBase.LogLevel.Hint);
            ModMain.Hint(Lang.Text("Online.Friend.Add.ApplyFailed"), ModMain.HintType.Critical);
        }
        finally
        {
            BtnApply.IsEnabled = true;
        }
    }

    private async Task RefreshPassiveAsync()
    {
        SetBusy(true);
        try
        {
            var requestsTask = Task.Run(() =>
                PCL.Online.OnlineFriendService.GetRequestsAsync().GetAwaiter().GetResult());
            var historyTask = Task.Run(() =>
                PCL.Online.OnlineFriendService.GetHistoryAsync().GetAwaiter().GetResult());
            await Task.WhenAll(requestsTask, historyTask);
            var requests = await requestsTask;
            var history = await historyTask;
            RenderRequests(PanRequests, requests, Lang.Text("Online.Friend.Add.NoRequests"));
            RenderRequests(PanHistory, history, Lang.Text("Online.Friend.Add.NoHistory"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Online.Friend.ServiceUnavailable"), ModBase.LogLevel.Hint);
            RenderRequests(PanRequests, [], Lang.Text("Online.Friend.Add.NoRequests"));
            RenderRequests(PanHistory, [], Lang.Text("Online.Friend.Add.NoHistory"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static void RenderRequests(
        Panel panel,
        IReadOnlyList<PCL.Online.OnlineFriendRequest> requests,
        string emptyText)
    {
        panel.Children.Clear();
        if (requests.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = emptyText,
                FontSize = 12,
                Opacity = 0.68
            });
            return;
        }

        foreach (var request in requests)
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"{request.TargetName}  |  {request.Status}",
                Margin = new Thickness(0, 0, 0, 7),
                FontSize = 13
            });
        }
    }

    private void SetBusy(bool busy)
    {
        Load.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.IsEnabled = !busy;
    }
}
