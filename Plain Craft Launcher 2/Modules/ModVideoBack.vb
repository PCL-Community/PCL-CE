Imports PCL.Core.Logging

Public Module ModVideoBack
    Public IsGaming As Boolean = False '判断用户是否在游戏中
    Public ForcePlay As Boolean = False '判断是否强行播放
    ''' <summary>
    ''' 尝试开始视频背景播放
    ''' </summary>
    ''' <param name="endGaming">用户是否停止游戏。</param>
    'endGaming只在ModWatcher里（即结束游戏后）能设为True
    Public Sub VideoPlay(endGaming As Boolean)
        RunInUi(
            Sub()
                If endGaming = True Then IsGaming = False
                If FrmMain.VideoBack.Source IsNot Nothing Then
                    If IsGaming = False Or ForcePlay = True Then
                        Try
                            If Not IsNothing(FrmSetupUI) Then FrmSetupUI.BtnBackgroundRefresh.IsEnabled = True
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
    ''' <param name="startGaming">用户是否启动游戏。</param>
    'startGaming只在ModLaunch里（即启动游戏后）能设为True
    Public Sub VideoPause(startGaming As Boolean)
        RunInUi(
            Sub()
                If startGaming = True Then IsGaming = True
                If ForcePlay = True Then
                    Return
                ElseIf FrmMain.VideoBack.Source IsNot Nothing Then
                    Try
                        FrmSetupUI.BtnBackgroundRefresh.IsEnabled = False
                        FrmMain.VideoBack.Pause()
                        Log("[UI] 已暂停视频背景播放")
                    Catch ex As Exception
                        Log(ex, "[UI] 暂停视频背景播放失败")
                    End Try
                End If
            End Sub
            )
    End Sub
End Module
