Public Class PageHomepageMarket
    Implements IRefreshable

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        InitLoading()
        Refresh()
    End Sub

    Private Sub InitLoading()
        Load.Text = "正在加载主页市场"
        Load.TextError = "加载失败，点击重试"
        Load.State.LoadingState = MyLoading.MyLoadingState.Run
        AddHandler Load.Click, AddressOf OnRetryClick
    End Sub

    Private Sub OnRetryClick(sender As Object, e As MouseButtonEventArgs)
        If Load.State.LoadingState = MyLoading.MyLoadingState.Error Then
            InitLoading()
            Refresh()
        End If
    End Sub

    Private Sub Refresh()
        RunInNewThread(Sub()
                           Try
                               Dim content = NetGetCodeByRequestRetry("https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml")
                               RunInUi(Sub()
                                           PanCustom.Children.Clear()
                                           PanCustom.Children.Add(GetObjectFromXML($"<StackPanel xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' xmlns:sys='clr-namespace:System;assembly=System.Runtime'>{content}</StackPanel>"))
                                           Load.State.LoadingState = MyLoading.MyLoadingState.Stop
                                           PanMain.Visibility = Visibility.Visible
                                       End Sub)
            Catch
                               RunInUi(Sub()
                                           Load.Text = "加载失败，点击重试"
                                           Load.State.LoadingState = MyLoading.MyLoadingState.Error
                                           PanMain.Visibility = Visibility.Visible
                                       End Sub)
            End Try
                       End Sub)
    End Sub

    Public Sub ForceRefresh() Implements IRefreshable.Refresh
        Refresh()
    End Sub
End Class