Public Module ModVideoBack
    Public Class BooleanEventArgs
        Inherits EventArgs
        Public Property Value As Boolean

        Public Sub New(value As Boolean)
            Me.Value = value
        End Sub
    End Class

    Public Event GamingStateChanged As EventHandler(Of BooleanEventArgs)
    Public Event ForcePlayChanged As EventHandler(Of BooleanEventArgs)

    Private _isGaming As Boolean = False
    Private _forcePlay As Boolean = False

    Public Property IsGaming As Boolean '判断用户是否在游戏中
        Get
            Return _isGaming
        End Get
        Set(value As Boolean)
            If _isGaming <> value Then
                _isGaming = value
                RaiseEvent GamingStateChanged(Nothing, New BooleanEventArgs(value))
            End If
        End Set
    End Property

    Public Property ForcePlay As Boolean '判断是否强行播放
        Get
            Return _forcePlay
        End Get
        Set(value As Boolean)
            If _forcePlay <> value Then
                _forcePlay = value
                RaiseEvent ForcePlayChanged(Nothing, New BooleanEventArgs(value))
            End If
        End Set
    End Property
    Public Sub OnGamingStateChanged(sender As Object, e As ModVideoBack.BooleanEventArgs) '用户是否在游戏中 事件
        RunInUi(
            Sub()
                If IsGaming = True Then
                    If ForcePlay = True Then
                        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = True
                    Else
                        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = False
                    End If
                Else
                    If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = True
                End If
            End Sub
            )
    End Sub
    Public Sub OnForcePlayChanged(sender As Object, e As ModVideoBack.BooleanEventArgs) '是否强行播放 事件
        RunInUi(
            Sub()
                If IsGaming = True Then
                    If ForcePlay = True Then
                        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = True
                    Else
                        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = False
                    End If
                Else
                    If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = True
                End If
            End Sub
            )
    End Sub
    Public IsMinimized As Boolean = False '窗口是否被最小化
    ''' <summary>
    ''' 尝试开始视频背景播放
    ''' </summary>
    Public Sub VideoPlay()
        RunInUi(
            Sub()
                If FrmMain.VideoBack.Source IsNot Nothing And IsMinimized = False Then
                    If IsGaming = False Or ForcePlay = True Then
                        Try
                            FrmMain.VideoBack.Play()
                            Log("[UI] 已开始视频背景播放")
                        Catch ex As Exception
                            Log(ex, "[UI] 开始视频背景播放失败")
                        End Try
                    End If
                End If
            End Sub
            )
    End Sub
    ''' <summary>
    ''' 尝试停止视频背景播放
    ''' </summary>
    Public Sub VideoStop()
        RunInUi(
            Sub()
                Try
                    FrmMain.VideoBack.Source = Nothing
                    FrmMain.VideoBack.Stop()
                    FrmMain.VideoBack.Position = TimeSpan.Zero
                    Log("[UI] 已停止视频背景播放")
                Catch ex As Exception
                    Log(ex, "[UI] 停止视频背景播放失败")
                End Try
            End Sub
            )
    End Sub
    ''' <summary>
    ''' 尝试暂停视频背景播放
    ''' </summary>
    Public Sub VideoPause()
        RunInUi(
            Sub()
                If IsMinimized = True Then
                    If FrmMain.VideoBack.Source IsNot Nothing Then
                        '窗口最小化后暂停
                        Try
                            FrmMain.VideoBack.Pause()
                            Log("[UI] 已暂停视频背景播放")
                        Catch ex As Exception
                            Log(ex, "[UI] 暂停视频背景播放失败")
                        End Try
                    End If
                Else
                    If ForcePlay = True Then
                        Return
                    Else
                        If FrmMain.VideoBack.Source IsNot Nothing Then
                            '游戏启动后暂停
                            Try
                                If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = False
                                FrmMain.VideoBack.Pause()
                                Log("[UI] 已暂停视频背景播放")
                            Catch ex As Exception
                                Log(ex, "[UI] 暂停视频背景播放失败")
                            End Try
                        End If
                    End If
                End If
            End Sub
            )
    End Sub
End Module
