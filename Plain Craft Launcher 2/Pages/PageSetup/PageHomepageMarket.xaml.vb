Imports System.Net
Imports System.Windows.Markup
Imports System.Xml
Imports System.IO
Imports System.Windows.Controls
Imports System.Linq

Public Class PageHomepageMarket
    Implements IRefreshable

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        InitRefreshButton()
        Refresh()
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
            .Visibility = Visibility.Collapsed '隐藏按钮，默认不显示
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
                    Log(ex, "加载主页市场内容失败", If(ModeDebug, LogLevel.Msgbox, LogLevel.Hint))
                End Try
            End Sub, $"刷新主页市场 #{GetUuid()}")
    End Sub

    Private Sub RefreshReal()
        Dim url As String = "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/JingHai-Lingyun/Custom.xaml"

        ' 显示加载状态
        RunInUi(Sub()
                    Dim keepControls As New List(Of UIElement)
                    For Each child As UIElement In PanCustom.Children
                        If TypeOf child Is Button AndAlso DirectCast(child, Button).Name = "BtnManualRefresh" Then
                            keepControls.Add(child)
                        End If
                    Next

                    PanCustom.Children.Clear()

                    For Each control In keepControls
                        PanCustom.Children.Add(control)
                    Next

                    PanCustom.Children.Add(New TextBlock With {
                        .Text = "正在加载内容...",
                        .HorizontalAlignment = HorizontalAlignment.Center,
                        .VerticalAlignment = VerticalAlignment.Center,
                        .Margin = New Thickness(0, 50, 0, 0)
                    })
                End Sub)

        Try
            Dim content As String = NetGetCodeByRequestRetry(url)
            Setup.Set("CacheSavedPageUrl", url)
            WriteFile(PathTemp & "Cache\Custom.xaml", content)

            RunInUi(Sub() LoadContent(content))
        Catch ex As Exception
            Log(ex, $"下载主页市场失败 ({url})", LogLevel.Hint)
            RunInUi(Sub() LoadFallbackContent())
        End Try
    End Sub

    Private Sub LoadContent(content As String)
        SyncLock LoadContentLock
            Try
                Dim keepControls As New List(Of UIElement)
                For Each child As UIElement In PanCustom.Children
                    If TypeOf child Is Button AndAlso DirectCast(child, Button).Name = "BtnManualRefresh" Then
                        keepControls.Add(child)
                    End If
                Next

                PanCustom.Children.Clear()

                For Each control In keepControls
                    PanCustom.Children.Add(control)
                Next

                If String.IsNullOrEmpty(content) Then
                    LoadFallbackContent()
                    Return
                End If

                Dim wrappedXaml = "<StackPanel " &
                "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " &
                "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' " &
                "xmlns:local='clr-namespace:PCL;assembly=Plain Craft Launcher 2' " &
                "xmlns:sys='clr-namespace:System;assembly=mscorlib'>" &
                content & "</StackPanel>"

                ' 解析并加载
                Dim uiElement = GetObjectFromXML(wrappedXaml)
                PanCustom.Children.Add(uiElement)

            Catch ex As Exception
                Log(ex, "解析XAML失败", LogLevel.Msgbox)
                LoadFallbackContent()
            End Try
        End SyncLock
    End Sub

    Private Sub LoadFallbackContent()
        Dim keepControls As New List(Of UIElement)
        For Each child As UIElement In PanCustom.Children
            If TypeOf child Is Button AndAlso DirectCast(child, Button).Name = "BtnManualRefresh" Then
                keepControls.Add(child)
            End If
        Next

        PanCustom.Children.Clear()

        For Each control In keepControls
            PanCustom.Children.Add(control)
        Next

        PanCustom.Children.Add(New TextBlock With {
            .Text = "内容加载失败，请重试",
            .HorizontalAlignment = HorizontalAlignment.Center,
            .VerticalAlignment = VerticalAlignment.Center,
            .Foreground = Brushes.Red
        })
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