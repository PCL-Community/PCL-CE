Public Class PageHomepageMarket
    Implements IRefreshable

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Load.TextErrorInherit = False
        Load.TextError = "加载失败，点击重试"
        UpdateLoadingState("正在加载主页市场", MyLoading.MyLoadingState.Run)
        AddHandler Load.Click, AddressOf OnLoadingClick

        InitRefreshButton()
        Refresh()
    End Sub

    Private Sub UpdateLoadingState(text As String, state As MyLoading.MyLoadingState)
        RunInUi(Sub()
                    Load.Text = text
                    Load.State.LoadingState = state
                End Sub)
    End Sub

    Private Sub OnLoadingClick(sender As Object, e As MouseButtonEventArgs)
        If Load.State.LoadingState = MyLoading.MyLoadingState.Error Then
            UpdateLoadingState("正在重新加载...", MyLoading.MyLoadingState.Run)
            Refresh()
        End If
    End Sub

    Private Sub InitRefreshButton()
        ' 检查按钮是否已存在
        For Each child As UIElement In PanCustom.Children
            If TypeOf child Is Button AndAlso DirectCast(child, Button).Name = "BtnManualRefresh" Then
                BtnManualRefresh = DirectCast(child, Button)
                Return
            End If
        Next

        ' 创建新按钮
        BtnManualRefresh = New Button With {
            .Name = "BtnManualRefresh",
            .Content = "刷新",
            .Width = 80,
            .Height = 30,
            .Margin = New Thickness(0, 10, 15, 0),
            .HorizontalAlignment = HorizontalAlignment.Right,
            .VerticalAlignment = VerticalAlignment.Top,
            .Visibility = Visibility.Collapsed
        }
        AddHandler BtnManualRefresh.Click, AddressOf BtnManualRefresh_Click

        ' 确保按钮在最上层
        Panel.SetZIndex(BtnManualRefresh, 999)
        PanCustom.Children.Add(BtnManualRefresh)
    End Sub

    Private WithEvents BtnManualRefresh As Button

    Private Sub BtnManualRefresh_Click(sender As Object, e As RoutedEventArgs)
        ForceRefresh()
    End Sub

    Private Sub Refresh() Handles Me.Loaded
        RunInNewThread(
            Sub()
                Try
                    SyncLock RefreshLock
                        RefreshReal()
                    End SyncLock
                Catch ex As Exception
                    Log(ex, "加载主页市场失败", If(ModeDebug, LogLevel.Msgbox, LogLevel.Hint))
                    UpdateLoadingState("加载失败，点击重试", MyLoading.MyLoadingState.Error)
                End Try
            End Sub, $"刷新主页市场 #{GetUuid()}")
    End Sub


    Private Sub RefreshReal()
        Dim url As String = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml"

        Try
            Dim content As String = NetGetCodeByRequestRetry(url)
            Setup.Set("CacheSavedPageUrl", url)
            WriteFile(PathTemp & "Cache\Custom.xaml", content)

            RunInUi(Sub() LoadContent(content))
        Catch ex As Exception
            Log(ex, $"加载主页市场失败 ({url})", LogLevel.Hint)
            UpdateLoadingState("加载失败，点击重试", MyLoading.MyLoadingState.Error)

            RunInUi(Sub()
                        PanMain.Visibility = Visibility.Visible
                    End Sub)
        End Try
    End Sub

    Private Sub LoadContent(content As String)
        SyncLock LoadContentLock
            Try
                Dim wrappedXaml = "<StackPanel " &
                    "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " &
                    "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " &
                    "xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' " &
                    "xmlns:sys='clr-namespace:System;assembly=mscorlib'>" &
                    content & "</StackPanel>"

                ' 解析并加载
                Dim uiElement = GetObjectFromXML(wrappedXaml)
                PanCustom.Children.Clear()
                PanCustom.Children.Add(uiElement)

                ' 加载成功后停止显示加载状态
                Load.State.LoadingState = MyLoading.MyLoadingState.Stop

                RunInUi(Sub()
                            PanMain.Visibility = Visibility.Visible
                        End Sub)

            Catch ex As Exception
                Log(ex, "解析XAML失败", LogLevel.Msgbox)
                UpdateLoadingState("解析失败，点击重试", MyLoading.MyLoadingState.Error)

                RunInUi(Sub()
                            PanMain.Visibility = Visibility.Visible
                        End Sub)
            End Try
        End SyncLock
    End Sub

    Private RefreshLock As New Object
    Private LoadContentLock As New Object

    Public Sub ForceRefresh() Implements IRefreshable.Refresh
        ClearCache()
        Hint("正在手动刷新...")
        Refresh()
    End Sub

    Private Sub ClearCache()
        Setup.Set("CacheSavedPageUrl", "")
        Setup.Set("CacheSavedPageVersion", "")
    End Sub
End Class