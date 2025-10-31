Imports Microsoft.VisualBasic.FileIO
Imports System.IO

Public Class PageInstanceSaves
    Implements IRefreshable

    Private QuickPlayFeature = False
    Private fileSystemWatcher As FileSystemWatcher
    Private WithEvents fileSystemRefreshTimer As New Threading.DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(100)}
    Private WithEvents searchTimer As New Threading.DispatcherTimer With {.Interval = TimeSpan.FromMilliseconds(100)}

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

        '初始化文件系统监视器和排序按钮
        SetupFileSystemWatcher()
        AddHandler BtnSort.Click, AddressOf BtnSortClick
    End Sub

    Private Function GetFolderNameFromPath(fullPath As String) As String
        Return If(String.IsNullOrEmpty(fullPath), "",
               If(fullPath.EndsWith("\"), New DirectoryInfo(fullPath).Parent?.Name,
               New DirectoryInfo(fullPath).Name))
    End Function

    Private Function GetFileNameFromPath(fullPath As String) As String
        Return Path.GetFileName(fullPath)
    End Function

    Private Sub SetupFileSystemWatcher()
        If fileSystemWatcher IsNot Nothing Then
            fileSystemWatcher.Dispose()
        End If

        '确保目录存在
        If Not Directory.Exists(WorldPath) Then Directory.CreateDirectory(WorldPath)

        fileSystemWatcher = New FileSystemWatcher()
        fileSystemWatcher.Path = WorldPath
        fileSystemWatcher.IncludeSubdirectories = False
        fileSystemWatcher.NotifyFilter = NotifyFilters.DirectoryName Or NotifyFilters.LastWrite

        AddHandler fileSystemWatcher.Created, AddressOf OnFileSystemChanged
        AddHandler fileSystemWatcher.Deleted, AddressOf OnFileSystemChanged
        AddHandler fileSystemWatcher.Renamed, AddressOf OnFileSystemChanged

        fileSystemWatcher.EnableRaisingEvents = True
    End Sub

    Private Sub OnFileSystemChanged(sender As Object, e As FileSystemEventArgs)
        fileSystemRefreshTimer.Stop()
        fileSystemRefreshTimer.Start()
    End Sub

    Private Sub FileSystemRefreshTimer_Tick(sender As Object, e As EventArgs) Handles fileSystemRefreshTimer.Tick
        fileSystemRefreshTimer.Stop()
        RunInUi(Sub() Reload(), True)
    End Sub

    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        If fileSystemWatcher IsNot Nothing Then
            RemoveHandler fileSystemWatcher.Created, AddressOf OnFileSystemChanged
            RemoveHandler fileSystemWatcher.Deleted, AddressOf OnFileSystemChanged
            RemoveHandler fileSystemWatcher.Renamed, AddressOf OnFileSystemChanged
            fileSystemWatcher.Dispose()
            fileSystemWatcher = Nothing
        End If
        fileSystemRefreshTimer.Stop()
        searchTimer.Stop()
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
        Try
            If IsSearching Then
                Dim resultCount As Integer = If(_searchResult Is Nothing, 0, _searchResult.Count)
                PanListBack.Title = $"搜索结果 ({resultCount})"
            Else
                PanListBack.Title = $"存档列表 ({saveFolders.Count})"
            End If

            If saveFolders.Count = 0 Then
                PanNoWorld.Visibility = Visibility.Visible
                PanContent.Visibility = Visibility.Collapsed
                PanNoWorld.UpdateLayout()
            Else
                PanNoWorld.Visibility = Visibility.Collapsed
                PanContent.Visibility = Visibility.Visible
                PanContent.UpdateLayout()

                Dim showingSaves = If(IsSearching, _searchResult, saveFolders).ToList()

                If showingSaves.Any() Then
                    Dim sortMethod = GetSortMethod(_currentSortMethod)
                    showingSaves.Sort(Function(a, b) sortMethod(a, b))
                End If

                AniControlEnabled += 1
                PanList.Children.Clear()

                For Each curFolder In showingSaves
                    '检查文件夹是否仍然存在
                    If Not Directory.Exists(curFolder) Then
                        Continue For
                    End If

                    Dim saveLogo = curFolder + "\icon.png"
                    Dim tmpCurFolder = curFolder
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
                        .Info = $"创建时间：{ Directory.GetCreationTime(curFolder).ToString("yyyy""/""MM""/""dd")}，最后修改时间：{Directory.GetLastWriteTime(curFolder).ToString("yyyy""/""MM""/""dd")}",
                        .Type = MyListItem.CheckType.Clickable
                    }
                    AddHandler worldItem.Click, Sub()
                                                    FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.VersionSaves, .Additional = tmpCurFolder})
                                                End Sub

                    Dim BtnOpen As New MyIconButton With {
                        .Logo = Logo.IconButtonOpen,
                        .ToolTip = "打开"
                    }
                    AddHandler BtnOpen.Click, Sub()
                                                  OpenExplorer(tmpCurFolder)
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
                                                                           FileSystem.DeleteDirectory(tmpCurFolder, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)
                                                                           Hint("已将存档移至回收站！")
                                                                           RunInUiWait(Sub() RemoveItem(worldItem))
                                                                       Catch ex As Exception
                                                                           Log(ex, "删除存档失败！", LogLevel.Hint)
                                                                           RunInUiWait(Sub() Reload())
                                                                       End Try
                                                                   End Sub)
                                                End Sub
                    Dim BtnCopy As New MyIconButton With {
                        .Logo = Logo.IconButtonCopy,
                        .ToolTip = "复制"
                    }
                    AddHandler BtnCopy.Click, Sub()
                                                  Try
                                                      If Directory.Exists(tmpCurFolder) Then
                                                          Clipboard.SetFileDropList(New Specialized.StringCollection() From {tmpCurFolder})
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
                                                  FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.VersionSaves, .Additional = tmpCurFolder})
                                              End Sub

                    Dim BtnLaunch As New MyIconButton With {
                            .Logo = Logo.IconPlayGame,
                            .ToolTip = "快捷启动"
                        }
                    AddHandler BtnLaunch.Click, Sub()
                                                    Dim WorldName = GetFileNameFromPath(tmpCurFolder)
                                                    Dim LaunchOptions As New McLaunchOptions With {.WorldName = WorldName}
                                                    McLaunchStart(LaunchOptions)
                                                    FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
                                                End Sub
                    If QuickPlayFeature Then
                        worldItem.Buttons = {BtnOpen, BtnDelete, BtnCopy, BtnInfo, BtnLaunch}
                    Else
                        worldItem.Buttons = {BtnOpen, BtnDelete, BtnCopy, BtnInfo}
                    End If

                    PanList.Children.Add(worldItem)
                Next
                AniControlEnabled -= 1
            End If
        Catch ex As Exception
            Log(ex, "刷新存档UI失败", LogLevel.Hint)
        End Try
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
            If Directory.Exists(WorldPath) Then
                saveFolders = Directory.EnumerateDirectories(WorldPath).ToList()
            Else
                saveFolders = New List(Of String)()
            End If

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

            RefreshUI() ' 确保UI刷新
        Catch ex As Exception
            Log(ex, "载入存档列表失败", LogLevel.Hint)
        End Try
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
                                                                          RunInUi(Sub() Reload())
                                                                      End Sub))
        Dim loader As New LoaderCombo(Of Integer)($"{PageInstanceLeft.Instance.Name} - 复制存档", loaders) With {
            .OnStateChanged = AddressOf LoaderStateChangedHintOnly
        }
        loader.Start(1)
        LoaderTaskbarAdd(loader)
        FrmMain.BtnExtraDownload.ShowRefresh()
        FrmMain.BtnExtraDownload.Ribble()
    End Sub

#Region "搜索和排序"

    Private _currentSortMethod As SortMethod = SortMethod.FileName
    Private _searchResult As List(Of String)

    Public ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(SearchBox.Text)
        End Get
    End Property

    Private Enum SortMethod
        FileName
        CreateTime
        ModifyTime
    End Enum

    Private Function GetSortName(method As SortMethod) As String
        Select Case method
            Case SortMethod.FileName : Return "文件名"
            Case SortMethod.CreateTime : Return "创建时间"
            Case SortMethod.ModifyTime : Return "修改时间"
            Case Else : Return "文件名"
        End Select
    End Function

    Private Sub SetSortMethod(target As SortMethod)
        _currentSortMethod = target
        BtnSort.Text = $"排序：{GetSortName(target)}"
        RefreshUI()
    End Sub

    Private Sub BtnSortClick(sender As Object, e As EventArgs)
        Dim body As New ContextMenu
        For Each i As SortMethod In [Enum].GetValues(GetType(SortMethod))
            Dim item As New MyMenuItem
            item.Header = GetSortName(i)
            AddHandler item.Click, Sub() SetSortMethod(i)
            body.Items.Add(item)
        Next
        body.PlacementTarget = sender
        body.Placement = Primitives.PlacementMode.Bottom
        body.IsOpen = True
    End Sub

    Private Sub SearchRun() Handles SearchBox.TextChanged
        searchTimer.Stop()
        searchTimer.Start()
    End Sub

    Private Sub SearchTimer_Tick(sender As Object, e As EventArgs) Handles searchTimer.Tick
        searchTimer.Stop()
        PerformSearch()
    End Sub

    Private Sub PerformSearch()
        Try
            If IsSearching Then
                Dim queryList As New List(Of SearchEntry(Of String))
                For Each saveFolder In saveFolders
                    Dim folderName = GetFolderNameFromPath(saveFolder)
                    Dim searchSource As New List(Of KeyValuePair(Of String, Double))
                    searchSource.Add(New KeyValuePair(Of String, Double)(folderName, 1))
                    queryList.Add(New SearchEntry(Of String) With {.Item = saveFolder, .SearchSource = searchSource})
                Next
                _searchResult = Search(queryList, SearchBox.Text, MaxBlurCount:=6, MinBlurSimilarity:=0.35).Select(Function(r) r.Item).ToList()
            Else
                _searchResult = Nothing
            End If
            RefreshUI()
        Catch ex As Exception
            Log(ex, "搜索过程中发生异常", LogLevel.Debug)
        End Try
    End Sub

    Private Function GetSortMethod(method As SortMethod) As Func(Of String, String, Integer)
        Select Case method
            Case SortMethod.FileName
                Return Function(a As String, b As String) String.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b), StringComparison.OrdinalIgnoreCase)
            Case SortMethod.CreateTime
                Return Function(a As String, b As String) Directory.GetCreationTime(b).CompareTo(Directory.GetCreationTime(a))
            Case SortMethod.ModifyTime
                Return Function(a As String, b As String) Directory.GetLastWriteTime(b).CompareTo(Directory.GetLastWriteTime(a))
            Case Else
                Return Function(a As String, b As String) String.Compare(GetFolderNameFromPath(a), GetFolderNameFromPath(b), StringComparison.OrdinalIgnoreCase)
        End Select
    End Function

#End Region
End Class