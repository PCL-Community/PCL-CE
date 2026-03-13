Imports PCL.Core.IO.Net.Http.Client.Request

Public Class PageHomepageMarket
    Implements IRefreshable

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        InitLoading()
    End Sub

    Private Sub InitLoading()
        Load.Text = "正在加载主页市场"
        Load.TextError = "加载失败，点击重试"
        Load.State.LoadingState = MyLoading.MyLoadingState.Run
        AddHandler Load.Click, AddressOf OnRetryClick
        Refresh()
    End Sub

    Private Sub OnRetryClick(sender As Object, e As MouseButtonEventArgs)
        If Load.State.LoadingState = MyLoading.MyLoadingState.Error Then
            InitLoading()
        End If
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        Dispatcher.BeginInvoke(Function() RefreshAsync())
    End Sub
    Private Async Function RefreshAsync() As Task
        Try
            Const HomepageMarketUri = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Homepage.Market/Custom.xaml"
            Dim content As String
            Using resp = Await HttpRequest.Create(HomepageMarketUri).SendAsync()
                resp.EnsureSuccessStatusCode()
                content = resp.AsString()
            End Using

            content = content.Replace("EventType=""刷新主页""", "EventType=""刷新主页市场""")
            PanCustom.Children.Clear()
            PanCustom.Children.Add(GetObjectFromXML($"<StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' xmlns:sys='clr-namespace:System;assembly=System.Runtime'>{content}</StackPanel>"))
            Load.State.LoadingState = MyLoading.MyLoadingState.Stop
            PanMain.Visibility = Visibility.Visible
        Catch
            Load.Text = "加载失败，点击重试"
            Load.State.LoadingState = MyLoading.MyLoadingState.Error
            PanMain.Visibility = Visibility.Visible
        End Try
    End Function
End Class