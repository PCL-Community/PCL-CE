Imports System.Security.Principal

Public Class PageInstanceSaves
    Implements IRefreshable

    Private QuickPlayFeature = False

    Private Sub RefreshSelf() Implements IRefreshable.Refresh
        Refresh()
        CheckQuickPlay()
    End Sub
    Public Shared Sub Refresh()
        If FrmInstanceSaves IsNot Nothing Then FrmInstanceSaves.Reload()
        FrmInstanceLeft.ItemWorld.Checked = True
        Hint("正在刷新……", Log:=False)
    End Sub
    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        WorldPath = PageInstanceLeft.Instance.PathIndie + "saves\"
        If Not Directory.Exists(WorldPath) Then Directory.CreateDirectory(WorldPath)
        Reload()

        '非重复加载部分
        If IsLoad Then Exit Sub
        IsLoad = True
        CheckQuickPlay()
    End Sub

    Dim saveFolders As List(Of String) = New List(Of String)
    Dim WorldPath As String

    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Public Sub Reload()
        AniControlEnabled += 1
        PanBack.ScrollToHome()
        LoadFileList()
        AniControlEnabled -= 1
    End Sub

    Private Sub RefreshUI()
        PanCard.Title = $"存档列表 ({saveFolders.Count})"
        If saveFolders.Count.Equals(0) Then
            PanNoWorld.Visibility = Visibility.Visible
            PanContent.Visibility = Visibility.Collapsed
            PanNoWorld.UpdateLayout()
        Else
            PanNoWorld.Visibility = Visibility.Collapsed
            PanContent.Visibility = Visibility.Visible
            PanContent.UpdateLayout()
        End If
    End Sub

    Private Sub CheckQuickPlay()
        Try
            Dim cur As New LaunchArgument(PageInstanceLeft.Instance)
            QuickPlayFeature = cur.HasArguments("--quickPlaySingleplayer")
        Catch ex As Exception
            Log(ex, "检查存档快捷启动失败", LogLevel.Hint)
        End Try
    End Sub

    Private Sub LoadFileList()
        Try
            Log("[World] 刷新存档文件")
            saveFolders.Clear()
            saveFolders = Directory.EnumerateDirectories(WorldPath).ToList()
            If ModeDebug Then Log("[World] 共发现 " & saveFolders.Count & " 个存档文件夹", LogLevel.Debug)
            PanList.Children.Clear()
            CheckQuickPlay()

            If ModeDebug Then
                If QuickPlayFeature Then
                    Log("[World] 该实例支持存档快捷启动", LogLevel.Debug)
                Else
                    Log("[World] 该实例不支持存档快捷启动", LogLevel.Debug)
                End If
            End If

            For Each curFolder In saveFolders
                Dim saveLogo = curFolder + "\icon.png"
                If File.Exists(saveLogo) Then
                    Dim target = $"{PageInstanceLeft.Instance.Path}PCL\ImgCache\{GetStringMD5(saveLogo)}.png"
                    CopyFile(saveLogo, target)
                    saveLogo = target
                Else
                    saveLogo = PathImage & "Icons/NoIcon.png"
                End If
                Dim worldItem As New MyListItem With {
                    .Logo = saveLogo,
                    .Title = GetFolderNameFromPath(curFolder),
                    .Info = $"创建时间：{ Directory.GetCreationTime(curFolder).ToString("yyyy'/'MM'/'dd")}，最后修改时间：{Directory.GetLastWriteTime(curFolder).ToString("yyyy'/'MM'/'dd")}",
                    .Type = MyListItem.CheckType.Clickable
                }
                AddHandler worldItem.Click, Sub()
                                                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.VersionSaves, .Additional = curFolder})
                                            End Sub

                Dim BtnOpen As New MyIconButton With {
                    .Logo = Logo.IconButtonOpen,
                    .ToolTip = "打开"
                }
                AddHandler BtnOpen.Click, Sub()
                                              OpenExplorer(curFolder)
                                          End Sub
                Dim BtnDelete As New MyIconButton With {
                    .Logo = Logo.IconButtonDelete,
                    .ToolTip = "删除"
                }
                AddHandler BtnDelete.Click, Sub()
                                                worldItem.IsEnabled = False
                                                worldItem.Info = "删除中……"
                                                RunInNewThread(Sub()
                                                                   Try
                                                                       My.Computer.FileSystem.DeleteDirectory(curFolder, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                                                                       Hint("已将存档移至回收站！")
                                                                       RunInUiWait(Sub() RemoveItem(worldItem))
                                                                   Catch ex As Exception
                                                                       Log(ex, "删除存档失败！", LogLevel.Hint)
                                                                   End Try
                                                               End Sub)
                                            End Sub
                Dim BtnCopy As New MyIconButton With {
                    .Logo = Logo.IconButtonCopy,
                    .ToolTip = "复制"
                }
                AddHandler BtnCopy.Click, Sub()
                                              Try
                                                  If Directory.Exists(curFolder) Then
                                                      Clipboard.SetFileDropList(New Specialized.StringCollection() From {curFolder})
                                                      Hint("已复制存档文件夹到剪贴板！")
                                                      Hint("注意！在粘贴之前进行删除操作会导致存档丢失！")
                                                  Else
                                                      Hint("存档文件夹不存在！")
                                                  End If
                                              Catch ex As Exception
                                                  Log(ex, "复制失败……", LogLevel.Hint)
                                              End Try
                                          End Sub
                Dim BtnInfo As New MyIconButton With {
                    .Logo = Logo.IconButtonInfo,
                    .ToolTip = "详情"
                }
                AddHandler BtnInfo.Click, Sub()
                                              FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.VersionSaves, .Additional = curFolder})
                                          End Sub

                Dim BtnLaunch As New MyIconButton With {
                        .Logo = Logo.IconPlayGame,
                        .ToolTip = "快捷启动"
                    }
                AddHandler BtnLaunch.Click, Sub()
                                                Dim WorldName = GetFileNameFromPath(curFolder)
                                                Dim LaunchOptions As New McLaunchOptions With {.WorldName = WorldName}
                                                ModLaunch.McLaunchStart(LaunchOptions)
                                                FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
                                            End Sub
                If QuickPlayFeature Then
                    worldItem.Buttons = {BtnOpen, BtnDelete, BtnCopy, BtnInfo, BtnLaunch}
                Else
                    worldItem.Buttons = {BtnOpen, BtnDelete, BtnCopy, BtnInfo}
                End If

                PanList.Children.Add(worldItem)
            Next
        Catch ex As Exception
            Log(ex, "载入存档列表失败", LogLevel.Hint)
        End Try
        RefreshUI()
    End Sub

    Private Sub RemoveItem(item As MyListItem)
        If PanList.Children.IndexOf(item) = -1 Then Return
        PanList.Children.Remove(item)
        RefreshUI()
    End Sub
    Private Sub BtnOpenFolder_Click(sender As Object, e As MouseButtonEventArgs)
        OpenExplorer(WorldPath)
    End Sub
    Private Sub BtnPaste_Click(sender As Object, e As MouseButtonEventArgs)
        Dim files As Specialized.StringCollection = Clipboard.GetFileDropList()
        Dim loaders As New List(Of LoaderBase)
        loaders.Add(New LoaderTask(Of Integer, Integer)("Copy saves", Sub()
                                                                          Dim Copied = 0
                                                                          For Each i In files
                                                                              Try
                                                                                  If Directory.Exists(i) Then
                                                                                      If (Directory.Exists(WorldPath & GetFolderNameFromPath(i))) Then
                                                                                          Hint("发现同名文件夹，无法粘贴：" & GetFolderNameFromPath(i))
                                                                                      Else
                                                                                          CopyDirectory(i, WorldPath & GetFolderNameFromPath(i))
                                                                                          Copied += 1
                                                                                      End If
                                                                                  Else
                                                                                      Hint("源文件夹不存在或源目标不是文件夹")
                                                                                  End If
                                                                              Catch ex As Exception
                                                                                  Log(ex, "粘贴存档文件夹失败", LogLevel.Hint)
                                                                                  Continue For
                                                                              End Try
                                                                          Next
                                                                          If Copied > 0 Then Hint("已粘贴 " & Copied & " 个文件夹", HintType.Finish)
                                                                          RunInUi(Sub() FrmInstanceSaves?.RefreshUI())
                                                                      End Sub))
        Dim loader As New LoaderCombo(Of Integer)($"{PageInstanceLeft.Instance.Name} - 复制存档", loaders) With {
            .OnStateChanged = AddressOf LoaderStateChangedHintOnly
        }
        loader.Start(1)
        LoaderTaskbarAdd(loader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        FrmMain.BtnExtraDownload.Ribble()
    End Sub
End Class
