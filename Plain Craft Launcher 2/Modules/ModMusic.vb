Imports PCL.Core.Utils
Imports PCL.Core.App

Public Module ModMusic

#Region "播放列表"

    ''' <summary>
    ''' 接下来要播放的音乐文件路径。未初始化时为 Nothing。
    ''' </summary>
    Public MusicWaitingList As List(Of String) = Nothing

    ''' <summary>
    ''' 全部音乐文件路径。未初始化时为 Nothing。
    ''' </summary>
    Public MusicAllList As List(Of String) = Nothing

    ''' <summary>
    ''' 初始化音乐播放列表。
    ''' </summary>
    ''' <param name="ForceReload">强制全部重新载入列表。</param>
    ''' <param name="PreventFirst">在重载列表时避免让某项成为第一项。</param>
    Private Sub MusicListInit(ForceReload As Boolean, Optional PreventFirst As String = Nothing)
        If ForceReload Then MusicAllList = Nothing

        Try
            If MusicAllList Is Nothing Then
                MusicAllList = New List(Of String)
                Dim musicDir = IO.Path.Combine(ExePath, "PCL", "Musics")
                Directory.CreateDirectory(musicDir)
                For Each file In EnumerateFiles(musicDir)
                    Dim ext = file.Extension.ToLowerInvariant()
                    ' 忽略非音频文件
                    If {".ini", ".jpg", ".txt", ".cfg", ".lrc", ".db", ".png"}.Contains(ext) Then Continue For
                    MusicAllList.Add(file.FullName)
                Next
            End If

            ' 根据设置决定是否随机
            If Config.Preference.Music.ShufflePlayback Then
                MusicWaitingList = RandomUtils.Shuffle(New List(Of String)(MusicAllList))
            Else
                MusicWaitingList = New List(Of String)(MusicAllList)
            End If

            ' 避免 PreventFirst 成为第一项
            If PreventFirst IsNot Nothing AndAlso MusicWaitingList.Count > 0 AndAlso
               String.Equals(MusicWaitingList(0), PreventFirst, StringComparison.OrdinalIgnoreCase) Then
                MusicWaitingList.RemoveAt(0)
                MusicWaitingList.Add(PreventFirst)
            End If

        Catch ex As Exception
            Log(ex, "初始化音乐列表失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 获取下一首播放的音乐路径并将其从列表中移除。
    ''' 如果没有，可能会返回 Nothing。
    ''' </summary>
    Private Function DequeueNextMusicAddress() As String
        If MusicAllList Is Nothing OrElse Not MusicAllList.Any() OrElse Not MusicWaitingList.Any() Then
            MusicListInit(False)
        End If

        If MusicWaitingList.Any() Then
            Dim nextMusic = MusicWaitingList(0)
            MusicWaitingList.RemoveAt(0)
            If Not MusicWaitingList.Any() Then
                MusicListInit(False, nextMusic)
            End If
            Return nextMusic
        Else
            Return Nothing
        End If
    End Function

#End Region

#Region "UI 控制"

    ''' <summary>
    ''' 刷新背景音乐按钮 UI 与设置页 UI。
    ''' </summary>
    Private Sub MusicRefreshUI()
        RunInUi(Sub()
                    Try
                        If Not MusicAllList?.Any() = True Then
                            FrmMain.BtnExtraMusic.Show = False
                        Else
                            FrmMain.BtnExtraMusic.Show = True
                            Dim fileName = GetFileNameWithoutExtentionFromPath(MusicCurrent)
                            Dim isSingle = MusicAllList.Count = 1
                            Dim tipText As String

                            If MusicState = MusicStates.Pause Then
                                FrmMain.BtnExtraMusic.Logo = Logo.IconPlay
                                FrmMain.BtnExtraMusic.LogoScale = 0.8
                                tipText = $"已暂停：{fileName}"
                                tipText &= vbCrLf & If(isSingle,
                                    "左键恢复播放，右键重新从头播放。",
                                    "左键恢复播放，右键播放下一曲。")
                            Else
                                FrmMain.BtnExtraMusic.Logo = Logo.IconMusic
                                FrmMain.BtnExtraMusic.LogoScale = 1
                                tipText = $"正在播放：{fileName}"
                                tipText &= vbCrLf & If(isSingle,
                                    "左键暂停，右键重新从头播放。",
                                    "左键暂停，右键播放下一曲。")
                            End If

                            FrmMain.BtnExtraMusic.ToolTip = tipText
                            ToolTipService.SetVerticalOffset(FrmMain.BtnExtraMusic,
                                If(tipText.Contains(vbLf), 10, 16))
                        End If

                        FrmSetupUI?.MusicRefreshUI()

                    Catch ex As Exception
                        Log(ex, "刷新背景音乐 UI 失败", LogLevel.Feedback)
                    End Try
                End Sub)
    End Sub

    Public Sub MusicControlPause()
        If MusicNAudio Is Nothing Then
            Hint("音乐播放尚未开始！", HintType.Critical)
            Return
        End If

        Select Case MusicState
            Case MusicStates.Pause
                MusicResume()
            Case MusicStates.Play
                MusicPause()
            Case Else ' Stop
                Log("[Music] 音乐目前为停止状态，已强制尝试开始播放", LogLevel.Debug)
                MusicRefreshPlay(False)
        End Select
    End Sub

    Public Sub MusicControlNext()
        If MusicAllList?.Count = 1 Then
            MusicStartPlay(MusicCurrent)
            Hint("重新播放：" & GetFileNameFromPath(MusicCurrent), HintType.Finish)
        Else
            Dim addr = DequeueNextMusicAddress()
            If addr Is Nothing Then
                Hint("没有可以播放的音乐！", HintType.Critical)
            Else
                MusicStartPlay(addr)
                Hint("正在播放：" & GetFileNameFromPath(addr), HintType.Finish)
            End If
        End If
        MusicRefreshUI()
    End Sub

#End Region

#Region "主状态控制"

    Public ReadOnly Property MusicState As MusicStates
        Get
            If MusicNAudio Is Nothing Then Return MusicStates.Stop
            Return If(MusicNAudio.PlaybackState = NAudio.Wave.PlaybackState.Paused,
                      MusicStates.Pause,
                      If(MusicNAudio.PlaybackState = NAudio.Wave.PlaybackState.Stopped,
                         MusicStates.Stop,
                         MusicStates.Play))
        End Get
    End Property

    Public Enum MusicStates
        [Stop]
        Play
        Pause
    End Enum

    Public Sub MusicRefreshPlay(ShowHint As Boolean, Optional IsFirstLoad As Boolean = False)
        Try
            MusicListInit(True)

            If Not MusicAllList?.Any() = True Then
                If MusicNAudio IsNot Nothing Then
                    MusicNAudio = Nothing
                    If ShowHint Then Hint("背景音乐已清除！", HintType.Finish)
                Else
                    If ShowHint Then Hint("未检测到可用的背景音乐！", HintType.Critical)
                End If
            Else
                Dim addr = DequeueNextMusicAddress()
                If addr Is Nothing Then
                    If ShowHint Then Hint("没有可以播放的音乐！", HintType.Critical)
                Else
                    Try
                        MusicStartPlay(addr, IsFirstLoad)
                        If ShowHint Then Hint("背景音乐已刷新：" & GetFileNameFromPath(addr), HintType.Finish, False)
                    Catch
                        ' 容错：播放失败已在 MusicLoop 中处理
                    End Try
                End If
            End If

            MusicRefreshUI()

        Catch ex As Exception
            Log(ex, "刷新背景音乐播放失败", LogLevel.Feedback)
        End Try
    End Sub

    Private Sub MusicStartPlay(Address As String, Optional IsFirstLoad As Boolean = False)
        If String.IsNullOrEmpty(Address) Then Return
        Log("[Music] 播放开始：" & Address)
        MusicCurrent = Address
        RunInNewThread(Sub() MusicLoop(IsFirstLoad), "Music", ThreadPriority.BelowNormal)
    End Sub

    Public Function MusicPause() As Boolean
        If MusicState <> MusicStates.Play Then
            Log($"[Music] 无需暂停播放，当前状态为 {MusicState}")
            Return False
        End If

        RunInThread(Sub()
                        Log("[Music] 已暂停播放")
                        MusicNAudio?.Pause()
                        MusicRefreshUI()
                    End Sub)
        Return True
    End Function

    Public Function MusicResume() As Boolean
        If MusicState = MusicStates.Play OrElse Not MusicAllList?.Any() = True Then
            Log($"[Music] 无需继续播放，当前状态为 {MusicState}")
            Return False
        End If

        RunInThread(Sub()
                        Log("[Music] 已恢复播放")
                        Try
                            MusicNAudio?.Play()
                        Catch
                            ' 参考 PR #5415：设备变更后需 Stop + Play
                            MusicNAudio?.Stop()
                            MusicNAudio?.Play()
                        End Try
                        MusicRefreshUI()
                    End Sub)
        Return True
    End Function

#End Region

    ''' <summary>
    ''' 当前正在播放的 NAudio.Wave.WaveOutEvent。
    ''' </summary>
    Public MusicNAudio As NAudio.Wave.WaveOutEvent = Nothing

    ''' <summary>
    ''' 当前播放的音乐地址。
    ''' </summary>
    Private MusicCurrent As String = ""

    Private Sub MusicLoop(Optional IsFirstLoad As Boolean = False)
        Dim currentWave As NAudio.Wave.WaveOutEvent = Nothing
        Dim reader As NAudio.Wave.AudioFileReader = Nothing

        Try
            currentWave = New NAudio.Wave.WaveOutEvent()
            MusicNAudio = currentWave
            currentWave.DeviceNumber = -1 ' 使用默认设备

            reader = New NAudio.Wave.AudioFileReader(MusicCurrent)
            currentWave.Init(reader)
            currentWave.Play()

            ' 首次加载且用户未启用自动播放，则暂停
            If IsFirstLoad AndAlso Not Config.Preference.Music.StartOnStartup Then
                currentWave.Pause()
            End If

            MusicRefreshUI()

            Dim lastVolume = Config.Preference.Music.Volume
            currentWave.Volume = lastVolume / 1000.0F

            ' 播放主循环
            While currentWave.Equals(MusicNAudio) AndAlso
                  currentWave.PlaybackState <> NAudio.Wave.PlaybackState.Stopped

                ' 音量动态更新
                Dim currentVolume = Config.Preference.Music.Volume
                If currentVolume <> lastVolume Then
                    lastVolume = currentVolume
                    currentWave.Volume = currentVolume / 1000.0F
                End If

                ' 更新进度条
                If reader.TotalTime.TotalMilliseconds > 0 Then
                    Dim progress = reader.CurrentTime.TotalMilliseconds / reader.TotalTime.TotalMilliseconds
                    RunInUi(Sub() FrmMain.BtnExtraMusic.Progress = progress)
                End If

                Thread.Sleep(100)
            End While

            ' 播放结束，继续下一首
            If currentWave.PlaybackState = NAudio.Wave.PlaybackState.Stopped AndAlso
               MusicAllList?.Any() = True Then
                MusicStartPlay(DequeueNextMusicAddress(), IsFirstLoad)
            End If

        Catch ex As Exception
            Log(ex, $"播放音乐出现内部错误（{MusicCurrent}）", LogLevel.Developer)

            ' 错误处理：精准提示用户
            Dim fileName = GetFileNameFromPath(MusicCurrent)
            If TypeOf ex Is NAudio.MmException Then
                Dim msg = ex.Message
                If msg.Contains("AlreadyAllocated") Then
                    Hint("你的音频设备正被其他程序占用。请关闭占用程序后重启 PCL 以恢复音乐功能！", HintType.Critical)
                ElseIf msg.Contains("NoDriver") OrElse msg.Contains("BadDeviceId") Then
                    Hint("音频设备发生变更，音乐播放功能需重启 PCL 后恢复！", HintType.Critical)
                Else
                    Log(ex, $"播放失败（{fileName}）", LogLevel.Hint)
                End If
            ElseIf ex.Message.Contains("Got a frame at sample rate") OrElse
                   ex.Message.Contains("does not support changes to") Then
                Hint($"播放失败（{fileName}）：PCL 不支持中途变更音频属性的音乐文件", HintType.Critical)
            ElseIf Not MusicCurrent.EndsWithF(".wav", True) AndAlso
                   Not MusicCurrent.EndsWithF(".mp3", True) AndAlso
                   Not MusicCurrent.EndsWithF(".flac", True) OrElse
                   ex.Message.Contains("0xC00D36C4") Then
                Hint($"播放失败（{fileName}）：PCL 可能不支持此格式，请转换为 .wav/.mp3/.flac", HintType.Critical)
            Else
                Log(ex, $"播放失败（{fileName}）", LogLevel.Hint)
            End If

            ' 移除无效文件
            MusicAllList?.Remove(MusicCurrent)
            MusicWaitingList?.Remove(MusicCurrent)
            MusicRefreshUI()

            Thread.Sleep(2000)

            ' 尝试播放下一首
            If TypeOf ex Is FileNotFoundException Then
                MusicRefreshPlay(True, IsFirstLoad)
            Else
                MusicStartPlay(DequeueNextMusicAddress(), IsFirstLoad)
            End If

        Finally
            currentWave?.Dispose()
            reader?.Dispose()
            MusicRefreshUI()
        End Try
    End Sub

End Module