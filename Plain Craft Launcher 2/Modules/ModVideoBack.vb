Imports PCL.Core.Logging

Public Module ModVideoBack
    Private isGaming As Boolean = False '判断用户是否在游戏中
    ''' <summary>
    ''' 尝试开始视频背景播放
    ''' </summary>
    ''' <param name="EndGaming">用户是否停止游戏。</param>
    'EndGaming只在ModWatcher里（即结束游戏后）能设为True
    Public Sub VideoPlay(EndGaming As Boolean)
        RunInUi(
            Sub()
                If EndGaming = True Then
                    isGaming = False
                End If
                If FrmMain.VideoBack.Source IsNot Nothing And isGaming = False Then
                    Try
                        FrmMain.VideoBack.Play()
                        Log("[UI] 已开始视频背景播放")
                    Catch ex As Exception
                        Log(ex, "[UI] 开始视频背景播放失败")
                        Throw
                    End Try
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
    ''' <param name="StartGaming">用户是否启动游戏。</param>
    'StartGaming只在ModLaunch里（即启动游戏后）能设为True
    Public Sub VideoPause(StartGaming As Boolean)
        RunInUi(
            Sub()
                If StartGaming = True Then
                    isGaming = True
                End If
                If FrmMain.VideoBack.Source IsNot Nothing Then
                    Try
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
