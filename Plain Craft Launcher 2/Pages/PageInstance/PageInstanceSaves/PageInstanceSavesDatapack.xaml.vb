Imports System.Windows.Threading
Imports Microsoft.VisualBasic.FileIO
Imports PCL.Core.UI
Imports PCL.Core.UI.Theme

Public Class PageInstanceSavesDatapack
    Implements IRefreshable

#Region "数据包信息缓存"
    Private ReadOnly DatapackFileInfoCache As New Dictionary(Of String, (CreationTime As DateTime, Length As Long))

    ' 获取数据包信息（带缓存）
    Private Function GetDatapackFileInfo(path As String) As (CreationTime As DateTime, Length As Long)
        Dim cacheItem As (CreationTime As DateTime, Length As Long)
        If DatapackFileInfoCache.TryGetValue(path, cacheItem) Then
            Return cacheItem
        End If

        Try
            Dim fileInfo As New FileInfo(path)
            Dim newItem = (fileInfo.CreationTime, fileInfo.Length)
            If Not DatapackFileInfoCache.ContainsKey(path) Then
                DatapackFileInfoCache.Add(path, newItem)
            End If
            Return newItem
        Catch ex As Exception
            Log(ex, "获取数据包信息失败: " & path)
            Return (DateTime.MinValue, 0)
        End Try
    End Function

    ' 页面关闭时清理缓存
    Private Sub Page_Unloaded(sender As Object, e As RoutedEventArgs) Handles Me.Unloaded
        DatapackFileInfoCache.Clear()
    End Sub
#End Region

#Region "初始化"

    Private CurrentSwipSelect As MyLocalCompItem.SwipeSelect

    Public Sub New()
        CurrentSwipSelect = New MyLocalCompItem.SwipeSelect() With {.TargetFrm = Me}

        InitializeComponent()

    End Sub

    Private Function GetRequireLoaderData() As CompLocalLoaderData
        Dim res As New CompLocalLoaderData
        res.GameVersion = PageInstanceLeft.Instance
        res.Frm = Nothing
        res.Loaders = {CompLoaderType.Minecraft}.ToList()
        res.CompPath = PageInstanceSavesLeft.CurrentSave & "\datapacks\"
        res.CompType = CompType.DataPack
        Return res
    End Function

    Private IsLoad As Boolean = False
    Public Sub PageOther_Loaded() Handles Me.Loaded
        If FrmMain.PageLast.Page <> FormMain.PageType.CompDetail Then PanBack.ScrollToHome()
        AniControlEnabled += 1
        SelectedDatapacks.Clear()
        ReloadDatapackFileList()
        ChangeAllSelected(False)
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True

        AddHandler FrmMain.KeyDown, AddressOf FrmMain_KeyDown
        '调整按钮边距（这玩意儿没法从 XAML 改）
        For Each Btn As MyRadioButton In PanFilter.Children
            Btn.LabText.Margin = New Thickness(-2, 0, 8, 0)
        Next

    End Sub

    ''' <summary>
    ''' 刷新数据包列表。
    ''' </summary>
    Public Sub ReloadDatapackFileList(Optional ForceReload As Boolean = False)
        If LoaderRun(If(ForceReload, LoaderFolderRunType.ForceRun, LoaderFolderRunType.RunOnUpdated)) Then
            Log($"[System] 已刷新数据包列表")
            DatapackFileInfoCache.Clear()

            RunInUi(Sub()
                        Filter = FilterType.All
                        PanBack.ScrollToHome()
                        SearchBox.Text = ""
                    End Sub)
        End If
    End Sub

    '强制刷新
    Private Sub RefreshSelf() Implements IRefreshable.Refresh
        Refresh()
    End Sub
    Public Sub Refresh()
        FrmInstanceSavesDatapack.ReloadDatapackFileList(True)
        Log("[Datapack] 刷新数据包列表")
    End Sub

    Private Sub LoaderInit() Handles Me.Initialized
        PageLoaderInit(Load, PanLoad, PanAllBack, Nothing, CompResourceListLoader, AddressOf LoadUIFromLoaderOutput, Function() CompType.DataPack, AutoRun:=False)
    End Sub
    Private Sub Load_Click(sender As Object, e As MouseButtonEventArgs) Handles Load.Click
        If CompResourceListLoader.State = LoadState.Failed Then
            LoaderRun(LoaderFolderRunType.ForceRun)
        End If
    End Sub
    Public Function LoaderRun(Type As LoaderFolderRunType) As Boolean
        Dim LoadPath As String = PageInstanceSavesLeft.CurrentSave & "\datapacks\"
        Return LoaderFolderRun(CompResourceListLoader, LoadPath, Type, LoaderInput:=GetRequireLoaderData())
    End Function

#End Region

#Region "UI 化"

    ''' <summary>
    ''' 已加载的数据包 UI 缓存。Key 为数据包的 RawPath。
    ''' </summary>
    Public DatapackItems As New Dictionary(Of String, MyLocalCompItem)
    ''' <summary>
    ''' 将加载器结果的数据包列表加载为 UI。
    ''' </summary>
    Private Sub LoadUIFromLoaderOutput()
        Try
            '判断应该显示哪一个页面
            If CompResourceListLoader.Output.Any() Then
                PanBack.Visibility = Visibility.Visible
                PanEmpty.Visibility = Visibility.Collapsed
            Else
                '根据组件类型设置 PanEmpty 的文本内容
                TxtEmptyTitle.Text = "尚未安装数据包"
                TxtEmptyDescription.Text = "你可以从已经下载好的文件安装数据包。" & vbCrLf & "数据包需要放置在存档的 datapacks 文件夹中才能生效。"

                PanEmpty.Visibility = Visibility.Visible
                PanBack.Visibility = Visibility.Collapsed
                Return
            End If

            '修改缓存
            DatapackItems.Clear()
            Dim itemsToShow = CompResourceListLoader.Output.ToList()

            For Each DatapackEntity As LocalCompFile In itemsToShow
                DatapackItems(DatapackEntity.RawPath) = BuildLocalCompItem(DatapackEntity)
            Next

            '显示结果
            RunInUi(Sub()
                        Filter = FilterType.All
                        SearchBox.Text = "" '这会触发结果刷新，所以需要在 DatapackItems 更新之后
                        RefreshUI()
                        SetSortMethod(SortMethod.CompName)
                    End Sub)
        Catch ex As Exception
            Log(ex, $"加载数据包列表 UI 失败", LogLevel.Feedback)
        End Try
    End Sub

    Private Function BuildLocalCompItem(Entry As LocalCompFile) As MyLocalCompItem
        Try
            AniControlEnabled += 1
            Dim NewItem As New MyLocalCompItem With {
                .SnapsToDevicePixels = True,
                .Entry = Entry,
                .ButtonHandler = AddressOf BuildLocalCompItemBtnHandler,
                .Checked = SelectedDatapacks.Contains(Entry.RawPath)
            }
            NewItem.CurrentSwipe = CurrentSwipSelect
            NewItem.Tags = Entry.Tags
            AddHandler Entry.OnCompUpdate, AddressOf NewItem.Refresh
            NewItem.Refresh()
            AniControlEnabled -= 1
            Return NewItem
        Catch ex As Exception
            AniControlEnabled -= 1
            Log(ex, $"创建 UI 项失败：{Entry.RawPath}", LogLevel.Debug)
            Throw
        End Try
    End Function

    Private Sub BuildLocalCompItemBtnHandler(sender As MyLocalCompItem, e As EventArgs)
        '点击事件
        AddHandler sender.Changed, AddressOf CheckChanged

        '文件项的点击事件：切换选中状态
        AddHandler sender.Click, Sub(ss As MyLocalCompItem, ee As EventArgs) ss.Checked = Not ss.Checked

        '图标按钮
        Dim BtnOpen As New MyIconButton With {.LogoScale = 1.05, .Logo = Logo.IconButtonOpen, .Tag = sender}
        BtnOpen.ToolTip = "打开文件位置"
        ToolTipService.SetPlacement(BtnOpen, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnOpen, 30)
        ToolTipService.SetHorizontalOffset(BtnOpen, 2)
        AddHandler BtnOpen.Click, AddressOf Open_Click

        Dim BtnCont As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonInfo, .Tag = sender}
        BtnCont.ToolTip = "详情"
        ToolTipService.SetPlacement(BtnCont, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnCont, 30)
        ToolTipService.SetHorizontalOffset(BtnCont, 2)
        AddHandler BtnCont.Click, AddressOf Info_Click
        AddHandler sender.MouseRightButtonUp, AddressOf Info_Click

        Dim BtnDelete As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonDelete, .Tag = sender}
        BtnDelete.ToolTip = "删除"
        ToolTipService.SetPlacement(BtnDelete, Primitives.PlacementMode.Center)
        ToolTipService.SetVerticalOffset(BtnDelete, 30)
        ToolTipService.SetHorizontalOffset(BtnDelete, 2)
        AddHandler BtnDelete.Click, AddressOf Delete_Click

        If sender.Entry.State = LocalCompFile.LocalFileStatus.Fine Then
            Dim BtnDisable As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonStop, .Tag = sender}
            BtnDisable.ToolTip = "禁用"
            ToolTipService.SetPlacement(BtnDisable, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnDisable, 30)
            ToolTipService.SetHorizontalOffset(BtnDisable, 2)
            AddHandler BtnDisable.Click, AddressOf Disable_Click
            sender.Buttons = {BtnCont, BtnOpen, BtnDisable, BtnDelete}
        ElseIf sender.Entry.State = LocalCompFile.LocalFileStatus.Disabled Then
            Dim BtnEnable As New MyIconButton With {.LogoScale = 1, .Logo = Logo.IconButtonCheck, .Tag = sender}
            BtnEnable.ToolTip = "启用"
            ToolTipService.SetPlacement(BtnEnable, Primitives.PlacementMode.Center)
            ToolTipService.SetVerticalOffset(BtnEnable, 30)
            ToolTipService.SetHorizontalOffset(BtnEnable, 2)
            AddHandler BtnEnable.Click, AddressOf Enable_Click
            sender.Buttons = {BtnCont, BtnOpen, BtnEnable, BtnDelete}
        Else
            sender.Buttons = {BtnCont, BtnOpen, BtnDelete}
        End If
    End Sub

    ''' <summary>
    ''' 刷新整个 UI。
    ''' </summary>
    Public Sub RefreshUI()
        If PanList Is Nothing Then Return
        Dim ShowingDatapacks = If(IsSearching, SearchResult, DatapackItems.Values.Select(Function(i) i.Entry)).Where(Function(m) CanPassFilter(m)).ToList

        ' 对显示的数据包进行排序
        If ShowingDatapacks.Any() Then
            Dim sortMethod = GetSortMethod(CurrentSortMethod)
            ShowingDatapacks.Sort(Function(a, b) sortMethod(a, b))
        End If

        '重新列出列表
        AniControlEnabled += 1
        If ShowingDatapacks.Any() Then
            PanList.Visibility = Visibility.Visible
            PanList.Children.Clear()
            For Each TargetDatapack In ShowingDatapacks
                If Not DatapackItems.ContainsKey(TargetDatapack.RawPath) Then Continue For
                Dim Item As MyLocalCompItem = DatapackItems(TargetDatapack.RawPath)

                ' 确保元素没有父容器，避免重复添加异常
                If Item.Parent IsNot Nothing Then
                    CType(Item.Parent, Panel).Children.Remove(Item)
                End If

                MinecraftFormatter.SetColorfulTextLab(Item.LabTitle.Text, Item.LabTitle, ThemeService.IsDarkMode)
                MinecraftFormatter.SetColorfulTextLab(Item.LabInfo.Text, Item.LabInfo, ThemeService.IsDarkMode)
                Item.Checked = SelectedDatapacks.Contains(TargetDatapack.RawPath) '更新选中状态
                PanList.Children.Add(Item)
            Next
        Else
            PanList.Visibility = Visibility.Collapsed
        End If
        AniControlEnabled -= 1
        SelectedDatapacks = New HashSet(Of String)(SelectedDatapacks.Where(Function(m) ShowingDatapacks.Any(Function(s) s.RawPath = m)))
        RefreshBars()
    End Sub

    ''' <summary>
    ''' 刷新顶栏和底栏显示。
    ''' </summary>
    Public Sub RefreshBars()
        Dispatcher.BeginInvoke(Async Function() As Task
                                   '-----------------
                                   ' 顶部栏
                                   '-----------------

                                   '计数
                                   Dim AnyCount As Integer = 0
                                   Dim EnabledCount As Integer = 0
                                   Dim DisabledCount As Integer = 0
                                   Dim UpdateCount As Integer = 0
                                   Dim UnavalialeCount As Integer = 0
                                   Dim ItemSource = If(IsSearching, SearchResult, DatapackItems.Values.Select(Function(i) i.Entry)).ToArray()
                                   Await Task.Run(Sub()
                                                      For Each item In ItemSource
                                                          AnyCount += 1
                                                          If item.CanUpdate Then UpdateCount += 1
                                                          If item.State = LocalCompFile.LocalFileStatus.Fine Then EnabledCount += 1
                                                          If item.State = LocalCompFile.LocalFileStatus.Disabled Then DisabledCount += 1
                                                          If item.State = LocalCompFile.LocalFileStatus.Unavailable Then UnavalialeCount += 1
                                                      Next
                                                  End Sub)
                                   '显示
                                   BtnFilterAll.Text = If(IsSearching, "搜索结果", "全部") & $" ({AnyCount})"
                                   BtnFilterCanUpdate.Text = $"可更新 ({UpdateCount})"
                                   BtnFilterCanUpdate.Visibility = If(Filter = FilterType.CanUpdate OrElse UpdateCount > 0, Visibility.Visible, Visibility.Collapsed)
                                   BtnFilterEnabled.Text = $"启用 ({EnabledCount})"
                                   BtnFilterEnabled.Visibility = If(Filter = FilterType.Enabled OrElse (EnabledCount > 0 AndAlso EnabledCount < AnyCount), Visibility.Visible, Visibility.Collapsed)
                                   BtnFilterDisabled.Text = $"禁用 ({DisabledCount})"
                                   BtnFilterDisabled.Visibility = If(Filter = FilterType.Disabled OrElse DisabledCount > 0, Visibility.Visible, Visibility.Collapsed)
                                   BtnFilterError.Text = $"错误 ({UnavalialeCount})"
                                   BtnFilterError.Visibility = If(Filter = FilterType.Unavailable OrElse UnavalialeCount > 0, Visibility.Visible, Visibility.Collapsed)

                                   '-----------------
                                   ' 底部栏
                                   '-----------------

                                   '计数
                                   Dim NewCount As Integer = SelectedDatapacks.Count
                                   Dim Selected = NewCount > 0
                                   If Selected Then LabSelect.Text = $"已选择 {NewCount} 个文件"

                                   '按钮可用性
                                   If Selected Then
                                       Dim HasUpdate As Boolean = False
                                       Dim HasEnabled As Boolean = False
                                       Dim HasDisabled As Boolean = False
                                       Dim CanFavoriteAndShare As Boolean = True

                                       Await Task.Run(Sub()
                                                          For Each DatapackEntity In CompResourceListLoader.Output
                                                              If SelectedDatapacks.Contains(DatapackEntity.RawPath) Then
                                                                  If DatapackEntity.CanUpdate Then HasUpdate = True
                                                                  If DatapackEntity.State = LocalCompFile.LocalFileStatus.Fine Then
                                                                      HasEnabled = True
                                                                  ElseIf DatapackEntity.State = LocalCompFile.LocalFileStatus.Disabled Then
                                                                      HasDisabled = True
                                                                  End If

                                                                  ' 检查是否所有选中的数据包都有有效的项目信息
                                                                  If DatapackEntity.Comp Is Nothing OrElse String.IsNullOrEmpty(DatapackEntity.Comp.Id) Then
                                                                      CanFavoriteAndShare = False
                                                                  End If
                                                              End If
                                                          Next
                                                      End Sub)

                                       BtnSelectDisable.IsEnabled = HasEnabled
                                       BtnSelectEnable.IsEnabled = HasDisabled
                                       BtnSelectUpdate.IsEnabled = HasUpdate
                                       BtnSelectFavorites.IsEnabled = CanFavoriteAndShare
                                       BtnSelectShare.IsEnabled = CanFavoriteAndShare
                                   End If

                                   '更新显示状态
                                   If AniControlEnabled = 0 Then
                                       PanListBack.Margin = New Thickness(0, 0, 0, If(Selected, 95, 15))
                                       If Selected Then
                                           '仅在数量增加时播放出现/跳跃动画
                                           If BottomBarShownCount >= NewCount Then
                                               BottomBarShownCount = NewCount
                                               Return
                                           Else
                                               BottomBarShownCount = NewCount
                                           End If
                                           '出现/跳跃动画
                                           CardSelect.Visibility = Visibility.Visible
                                           AniStart({
                                               AaOpacity(CardSelect, 1 - CardSelect.Opacity, 60),
                                               AaTranslateY(CardSelect, -27 - TransSelect.Y, 120, Ease:=New AniEaseOutFluent(AniEasePower.Weak)),
                                               AaTranslateY(CardSelect, 3, 150, 120, Ease:=New AniEaseInoutFluent(AniEasePower.Weak)),
                                               AaTranslateY(CardSelect, -1, 90, 270, Ease:=New AniEaseInoutFluent(AniEasePower.Weak))
                                           }, "Datapack Sidebar")
                                       Else
                                           '不重复播放隐藏动画
                                           If BottomBarShownCount = 0 Then Return
                                           BottomBarShownCount = 0
                                           '隐藏动画
                                           AniStart({
                                               AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                                               AaTranslateY(CardSelect, -10 - TransSelect.Y, 90, Ease:=New AniEaseInFluent(AniEasePower.Weak)),
                                               AaCode(Sub() CardSelect.Visibility = Visibility.Collapsed, After:=True)
                                           }, "Datapack Sidebar")
                                       End If
                                   Else
                                       AniStop("Datapack Sidebar")
                                       BottomBarShownCount = NewCount
                                       If Selected Then
                                           CardSelect.Visibility = Visibility.Visible
                                           CardSelect.Opacity = 1
                                           TransSelect.Y = -25
                                       Else
                                           CardSelect.Visibility = Visibility.Collapsed
                                           CardSelect.Opacity = 0
                                           TransSelect.Y = -10
                                       End If
                                   End If
                               End Function)
    End Sub
    Private BottomBarShownCount As Integer = 0

#End Region

#Region "管理"

    ''' <summary>
    ''' 打开 datapacks 文件夹。
    ''' </summary>
    Private Sub BtnManageOpen_Click(sender As Object, e As EventArgs) Handles BtnManageOpen.Click, BtnHintOpen.Click
        Try
            Dim DatapackPath As String = PageInstanceSavesLeft.CurrentSave & "\datapacks\"
            Directory.CreateDirectory(DatapackPath)
            OpenExplorer(DatapackPath)
        Catch ex As Exception
            Log(ex, "打开 datapacks 文件夹失败", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 全选。
    ''' </summary>
    Private Sub BtnManageSelectAll_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageSelectAll.Click
        ChangeAllSelected(SelectedDatapacks.Count < PanList.Children.Count)
    End Sub

    ''' <summary>
    ''' 安装数据包。
    ''' </summary>
    Private Sub BtnManageInstall_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageInstall.Click, BtnHintInstall.Click
        Dim FileList As String() = SystemDialogs.SelectFiles("数据包文件(*.zip)|*.zip", "选择要安装的数据包")
        If FileList Is Nothing OrElse Not FileList.Any Then Exit Sub
        InstallDatapackFiles(FileList)
        Refresh()
    End Sub

    ''' <summary>
    ''' 安装数据包文件。
    ''' </summary>
    Public Shared Sub InstallDatapackFiles(FilePathList As IEnumerable(Of String))
        If Not FilePathList.Any Then Exit Sub

        Dim Extension As String = FilePathList.First.AfterLast(".").ToLower

        '检查文件扩展名
        If Extension <> "zip" Then
            Hint($"不支持的文件格式：{Extension}，数据包支持的格式：zip", HintType.Critical)
            Exit Sub
        End If

        '检查回收站
        If FilePathList.First.Contains(":\$RECYCLE.BIN\") Then
            Hint("请先将文件从回收站还原，再尝试安装！", HintType.Critical)
            Exit Sub
        End If

        Log($"[System] 文件为 {Extension} 格式，尝试作为数据包安装")

        '确认安装
        If Not (FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.VersionSavesDatapack) Then
            If MyMsgBox($"是否要将这{If(FilePathList.Count = 1, "个", "些")}文件作为数据包安装到当前存档？", "数据包安装确认", "确定", "取消") <> 1 Then Exit Sub
        End If

        '执行安装
        Try
            Dim DatapackFolder As String = PageInstanceSavesLeft.CurrentSave & "\datapacks\"
            Directory.CreateDirectory(DatapackFolder)

            For Each FilePath In FilePathList
                Dim NewFileName = GetFileNameFromPath(FilePath)
                Dim DestFile = DatapackFolder & NewFileName

                If File.Exists(DestFile) Then
                    If MyMsgBox($"已存在同名文件：{NewFileName}，是否要覆盖？", "文件覆盖确认", "覆盖", "取消") <> 1 Then Continue For
                End If

                CopyFile(FilePath, DestFile)
            Next

            If FilePathList.Count = 1 Then
                Hint($"已安装 {GetFileNameFromPath(FilePathList.First)}！", HintType.Finish)
            Else
                Hint($"已安装 {FilePathList.Count} 个数据包！", HintType.Finish)
            End If

            '刷新列表
            If FrmMain.PageCurrent = FormMain.PageType.InstanceSetup AndAlso FrmMain.PageCurrentSub = FormMain.PageSubType.VersionSavesDatapack Then
                If FrmInstanceSavesDatapack IsNot Nothing Then
                    FrmInstanceSavesDatapack.ReloadDatapackFileList(True)
                End If
            End If

        Catch ex As Exception
            Log(ex, "复制数据包文件失败", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 下载数据包。
    ''' </summary>
    Private Sub BtnManageDownload_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageDownload.Click, BtnHintDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadDataPack)
        PageComp.TargetVersion = PageInstanceLeft.Instance '将当前实例设置为筛选器
    End Sub

    ''' <summary>
    ''' 导出信息。
    ''' </summary>
    Private Sub BtnManageInfoExport_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageInfoExport.Click
        Dim Choice = MyMsgBox("TXT 格式：仅导出当前的数据包文件名称信息" & vbCrLf &
                              "CSV 格式：导出详细的数据包信息，包括文件名、工程 ID、版本信息等详细信息",
                                Title:="选择导出模式",
                                Button1:="TXT 格式",
                                Button2:="CSV 格式",
                                Button3:="取消")
        Dim ExportText = Sub(Content As String, FileName As String)
                             Try
                                 Dim savePath = SystemDialogs.SelectSaveFile("选择保存位置", FileName, "文本文件(*.txt)|*.txt|CSV 文件(*.csv)|*.csv")
                                 If String.IsNullOrWhiteSpace(savePath) Then Exit Sub
                                 File.WriteAllText(savePath, Content, Encoding.UTF8)
                                 OpenExplorer(savePath)
                             Catch ex As Exception
                                 Log(ex, "导出数据包信息失败", LogLevel.Msgbox)
                             End Try
                         End Sub
        Select Case Choice
            Case 1 'TXT
                Dim ExportContent As New List(Of String)
                For Each DatapackEntity In CompResourceListLoader.Output
                    ExportContent.Add(DatapackEntity.FileName)
                Next
                ExportText(Join(ExportContent, vbCrLf), GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave) & "的数据包信息.txt")

            Case 2 'CSV
                Dim ExportContent As New List(Of String)
                ExportContent.Add("文件名,数据包名称,数据包版本,此版本更新时间,工程 ID,文件大小（字节）,文件路径")
                For Each DatapackEntity In CompResourceListLoader.Output
                    ExportContent.Add($"{DatapackEntity.FileName},{DatapackEntity.Comp?.TranslatedName},{DatapackEntity.Version},{DatapackEntity.CompFile?.ReleaseDate},{DatapackEntity.Comp?.Id},{GetDatapackFileInfo(DatapackEntity.Path).Length},{DatapackEntity.Path}")
                Next
                ExportText(Join(ExportContent, vbCrLf), GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave) & "的数据包信息.csv")
        End Select
    End Sub

#End Region

#Region "选择"

    ''' <summary>
    ''' 选择的数据包的路径。
    ''' </summary>
    Public SelectedDatapacks As New HashSet(Of String)

    '单项切换选择状态
    Public Sub CheckChanged(sender As MyLocalCompItem, e As RouteEventArgs)
        If AniControlEnabled <> 0 Then Return
        '更新选择了的内容
        Dim SelectedKey As String = sender.Entry.RawPath
        If sender.Checked Then
            SelectedDatapacks.Add(SelectedKey)
        Else
            SelectedDatapacks.Remove(SelectedKey)
        End If
        RefreshBars()
    End Sub

    '切换所有项的选择状态
    Private Sub ChangeAllSelected(Value As Boolean)
        AniControlEnabled += 1
        SelectedDatapacks.Clear()
        For Each Item As MyLocalCompItem In DatapackItems.Values
            Dim ShouldSelected As Boolean = Value AndAlso PanList.Children.Contains(Item)
            Item.Checked = ShouldSelected
            If ShouldSelected Then SelectedDatapacks.Add(Item.Entry.RawPath)
        Next
        AniControlEnabled -= 1
        RefreshBars()
    End Sub
    Private Sub UnselectedAllWithAnimation() Handles Load.StateChanged, Me.PageExit
        Dim CacheAniControlEnabled = AniControlEnabled
        AniControlEnabled = 0
        ChangeAllSelected(False)
        AniControlEnabled += CacheAniControlEnabled
    End Sub
    Private Sub FrmMain_KeyDown(sender As Object, e As KeyEventArgs)
        If FrmMain.PageRight IsNot Me Then Return
        If (Keyboard.IsKeyDown(Key.LeftCtrl) OrElse Keyboard.IsKeyDown(Key.RightCtrl)) AndAlso e.Key = Key.A Then ChangeAllSelected(True)
    End Sub
    Private Sub SearchBox_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles SearchBox.PreviewKeyDown
        'Ctrl + A 会被搜索框捕获，导致无法全选，所以在按下 Ctrl + A 时转移焦点以便捕获
        If SearchBox.Text.Any Then Return
        If (Keyboard.IsKeyDown(Key.LeftCtrl) OrElse Keyboard.IsKeyDown(Key.RightCtrl)) AndAlso e.Key = Key.A Then PanBack.Focus()
    End Sub

#End Region

#Region "筛选"

    Private _Filter As FilterType = FilterType.All
    Public Property Filter As FilterType
        Get
            Return _Filter
        End Get
        Set(value As FilterType)
            If _Filter = value Then Return
            _Filter = value
            Select Case value
                Case FilterType.All
                    BtnFilterAll.Checked = True
                Case FilterType.Enabled
                    BtnFilterEnabled.Checked = True
                Case FilterType.Disabled
                    BtnFilterDisabled.Checked = True
                Case FilterType.CanUpdate
                    BtnFilterCanUpdate.Checked = True
                Case Else
                    BtnFilterError.Checked = True
            End Select
            RefreshUI()
        End Set
    End Property
    Public Enum FilterType As Integer
        All = 0
        Enabled = 1
        Disabled = 2
        CanUpdate = 3
        Unavailable = 4
    End Enum

    ''' <summary>
    ''' 检查该数据包项是否符合当前筛选的类别。
    ''' </summary>
    Private Function CanPassFilter(CheckingDatapack As LocalCompFile) As Boolean
        Select Case Filter
            Case FilterType.All
                Return True
            Case FilterType.Enabled
                Return CheckingDatapack.State = LocalCompFile.LocalFileStatus.Fine
            Case FilterType.Disabled
                Return CheckingDatapack.State = LocalCompFile.LocalFileStatus.Disabled
            Case FilterType.CanUpdate
                Return CheckingDatapack.CanUpdate
            Case FilterType.Unavailable
                Return CheckingDatapack.State = LocalCompFile.LocalFileStatus.Unavailable
            Case Else
                Return False
        End Select
    End Function

    '点击筛选项触发的改变
    Private Sub ChangeFilter(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnFilterAll.Check, BtnFilterCanUpdate.Check, BtnFilterDisabled.Check, BtnFilterEnabled.Check, BtnFilterError.Check
        Filter = sender.Tag
        RefreshUI()
        DoSort()
    End Sub

#End Region

#Region "排序"
    Private CurrentSortMethod As SortMethod = SortMethod.CompName

    Private Sub SetSortMethod(Target As SortMethod)
        CurrentSortMethod = Target
        BtnSort.Text = $"排序：{GetSortName(Target)}"
        DoSort()
    End Sub

    Private Enum SortMethod
        FileName
        CompName
        CreateTime
        DatapackFileSize
    End Enum

    Private Function GetSortName(Method As SortMethod) As String
        Select Case Method
            Case SortMethod.FileName : Return "文件名"
            Case SortMethod.CompName : Return "资源名称"
            Case SortMethod.CreateTime : Return "加入时间"
            Case SortMethod.DatapackFileSize : Return "文件大小"
            Case Else : Return "资源名称"
        End Select
        Return ""
    End Function

    Private Sub BtnSortClick(sender As Object, e As RouteEventArgs) Handles BtnSort.Click
        Dim Body As New ContextMenu
        For Each i As SortMethod In [Enum].GetValues(GetType(SortMethod))
            Dim Item As New MyMenuItem
            Item.Header = GetSortName(i)
            AddHandler Item.Click, Sub()
                                       SetSortMethod(i)
                                   End Sub
            Body.Items.Add(Item)
        Next
        Body.PlacementTarget = sender
        Body.Placement = Primitives.PlacementMode.Bottom
        Body.IsOpen = True
    End Sub

    Private ReadOnly SortLock As New Object
    Private Sub DoSort()
        SyncLock SortLock
            Try
                If PanList Is Nothing OrElse PanList.Children.Count < 2 Then Exit Sub

                ' 将子元素转换为可排序的列表
                Dim items = PanList.Children.OfType(Of MyLocalCompItem)().ToList()
                Dim Method = GetSortMethod(CurrentSortMethod)

                ' 分离有效和无效项（保持原始相对顺序）
                Dim invalid = items.Where(Function(i) i.Entry Is Nothing).ToList()
                Dim valid = items.Except(invalid).ToList()
                ' 仅对有效项进行排序
                valid.Sort(Function(x, y) Method(x.Entry, y.Entry))
                ' 合并保持无效项的原始顺序
                items = valid.Concat(invalid).ToList()

                ' 批量更新UI元素
                PanList.Children.Clear()
                items.ForEach(Sub(i) PanList.Children.Add(i))

            Catch ex As Exception
                Log(ex, "执行排序时出错", LogLevel.Hint)
            End Try
        End SyncLock
    End Sub

    Private Function GetSortMethod(Method As SortMethod) As Func(Of LocalCompFile, LocalCompFile, Integer)
        Select Case Method
            Case SortMethod.FileName
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Return String.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase)
                       End Function
            Case SortMethod.CompName
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                       End Function
            Case SortMethod.CreateTime
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Dim aDate = GetDatapackFileInfo(a.Path).CreationTime
                           Dim bDate = GetDatapackFileInfo(b.Path).CreationTime
                           If aDate = DateTime.MinValue AndAlso bDate = DateTime.MinValue Then
                               Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                           ElseIf aDate = DateTime.MinValue Then
                               Return 1
                           ElseIf bDate = DateTime.MinValue Then
                               Return -1
                           End If
                           Return bDate.CompareTo(aDate)
                       End Function
            Case SortMethod.DatapackFileSize
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Dim aSize As Long = GetDatapackFileInfo(a.Path).Length
                           Dim bSize As Long = GetDatapackFileInfo(b.Path).Length
                           If aSize = 0 AndAlso bSize = 0 Then
                               Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                           ElseIf aSize = 0 Then
                               Return 1
                           ElseIf bSize = 0 Then
                               Return -1
                           End If
                           Return bSize.CompareTo(aSize)
                       End Function
            Case Else
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Return String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                       End Function
        End Select
    End Function
#End Region

#Region "下边栏"

    '启用
    Private Sub BtnSelectEnable_Click(sender As MyIconTextButton, e As RouteEventArgs) Handles BtnSelectEnable.Click
        ToggleDatapacks(CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath)).ToList(), True)
        ChangeAllSelected(False)
    End Sub

    '禁用
    Private Sub BtnSelectDisable_Click(sender As MyIconTextButton, e As RouteEventArgs) Handles BtnSelectDisable.Click
        ToggleDatapacks(CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath)).ToList(), False)
        ChangeAllSelected(False)
    End Sub

    ''' <summary>
    ''' 启用/禁用数据包（通过重命名文件夹为 .disabled）
    ''' </summary>
    Private Sub ToggleDatapacks(DatapackList As IEnumerable(Of LocalCompFile), IsEnable As Boolean)
        Dim IsSuccessful As Boolean = True
        For Each DatapackE In DatapackList
            Dim DatapackEntity = DatapackE
            Dim NewPath As String = Nothing

            If DatapackEntity.State = LocalCompFile.LocalFileStatus.Fine AndAlso Not IsEnable Then
                '禁用 - 添加 .disabled 后缀
                NewPath = DatapackEntity.Path & ".disabled"
            ElseIf DatapackEntity.State = LocalCompFile.LocalFileStatus.Disabled AndAlso IsEnable Then
                '启用 - 移除 .disabled 后缀
                NewPath = DatapackEntity.RawPath
            Else
                Continue For
            End If

            '重命名
            Try
                If File.Exists(NewPath) Then
                    MyMsgBox($"已存在同名文件：{GetFileNameFromPath(NewPath)}，请先处理该文件再重试。")
                    Continue For
                End If

                Rename(DatapackEntity.Path, NewPath)
            Catch ex As FileNotFoundException
                Log(ex, $"未找到需要重命名的数据包（{If(DatapackEntity.Path, "null")}）", LogLevel.Feedback)
                ReloadDatapackFileList(True)
                Return
            Catch ex As Exception
                Log(ex, $"重命名数据包失败（{If(DatapackEntity.Path, "null")}）")
                IsSuccessful = False
            End Try

            '更改 Loader 中的列表
            Dim NewDatapackEntity As New LocalCompFile(NewPath)
            NewDatapackEntity.FromJson(DatapackEntity.ToJson)
            If CompResourceListLoader.Output.Contains(DatapackEntity) Then
                Dim IndexOfLoader As Integer = CompResourceListLoader.Output.IndexOf(DatapackEntity)
                CompResourceListLoader.Output.RemoveAt(IndexOfLoader)
                CompResourceListLoader.Output.Insert(IndexOfLoader, NewDatapackEntity)
            End If
            If SearchResult IsNot Nothing AndAlso SearchResult.Contains(DatapackEntity) Then
                Dim IndexOfResult As Integer = SearchResult.IndexOf(DatapackEntity)
                SearchResult.Remove(DatapackEntity)
                SearchResult.Insert(IndexOfResult, NewDatapackEntity)
            End If

            '更改 UI 中的列表
            Try
                Dim NewItem As MyLocalCompItem = BuildLocalCompItem(NewDatapackEntity)
                DatapackItems(DatapackEntity.RawPath) = NewItem
                Dim IndexOfUi As Integer = PanList.Children.IndexOf(PanList.Children.OfType(Of MyLocalCompItem).FirstOrDefault(Function(i) i.Entry Is DatapackEntity))
                If IndexOfUi = -1 Then Continue For
                PanList.Children.RemoveAt(IndexOfUi)
                PanList.Children.Insert(IndexOfUi, NewItem)
            Catch ex As Exception
                Log(ex, $"更新 UI 列表项失败：{DatapackEntity.FileName}", LogLevel.Hint)
                Continue For
            End Try
        Next

        Dispatcher.Invoke(Sub()
                              PanList.UpdateLayout()
                          End Sub, DispatcherPriority.Background)

        If IsSuccessful Then
            RefreshBars()
        Else
            Hint("由于文件被占用，数据包的状态切换失败，请尝试关闭正在运行的游戏后再试！", HintType.Critical)
            ReloadDatapackFileList(True)
        End If
        LoaderRun(LoaderFolderRunType.UpdateOnly)
    End Sub

    '更新
    Private Sub BtnSelectUpdate_Click() Handles BtnSelectUpdate.Click
        Dim UpdateList As List(Of LocalCompFile) = CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath) AndAlso m.CanUpdate).ToList()
        If Not UpdateList.Any() Then Return
        UpdateResource(UpdateList)
        ChangeAllSelected(False)
    End Sub

    ''' <summary>
    ''' 记录正在进行数据包更新的 datapacks 文件夹路径。
    ''' </summary>
    Public Shared UpdatingVersions As New List(Of String)

    Public Sub UpdateResource(DatapackList As IEnumerable(Of LocalCompFile))
        '更新前警告
        If (Not Setup.Get("HintDatapackUpdate")) OrElse DatapackList.Count >= 15 Then
            If MyMsgBox($"新版本数据包可能不兼容旧存档或者其他数据包，这可能导致游戏崩溃或存档损坏！{vbCrLf}{vbCrLf}在更新前，请先备份存档。{vbCrLf}如果更新后出现问题，你也可以在回收站找回更新前的数据包。", "数据包更新警告", "我已了解风险，继续更新", "取消", IsWarn:=True) = 1 Then
                Setup.Set("HintDatapackUpdate", True)
            Else
                Return
            End If
        End If

        Try
            '构造下载信息
            DatapackList = DatapackList.ToList() '防止刷新影响迭代器
            Dim FileList As New List(Of NetFile)
            Dim FileCopyList As New Dictionary(Of String, String)
            For Each Entry As LocalCompFile In DatapackList
                Dim File As CompFile = Entry.UpdateFile
                If Not File.Available Then Continue For
                '添加到下载列表
                Dim TempAddress As String = PathTemp & "DownloadedComp\" & File.FileName
                Dim RealAddress As String = PageInstanceSavesLeft.CurrentSave & "\datapacks\" & File.FileName
                FileList.Add(File.ToNetFile(TempAddress))
                FileCopyList(TempAddress) = RealAddress
            Next

            '构造加载器
            Dim InstallLoaders As New List(Of LoaderBase)
            Dim FinishedFileNames As New List(Of String)
            InstallLoaders.Add(New LoaderDownload("下载新版数据包文件", FileList) With {.ProgressWeight = DatapackList.Count * 1.5})

            InstallLoaders.Add(New LoaderTask(Of Integer, Integer)("替换旧版数据包文件",
            Sub()
                Try
                    For Each Entry As LocalCompFile In DatapackList
                        If File.Exists(Entry.Path) Then
                            FileSystem.DeleteFile(Entry.Path, FileIO.UIOption.AllDialogs, FileIO.RecycleOption.SendToRecycleBin)
                        Else
                            Log($"[DatapackUpdate] 未找到更新前的数据包文件，跳过对它的删除：{Entry.Path}", LogLevel.Debug)
                        End If
                    Next
                    For Each Entry As KeyValuePair(Of String, String) In FileCopyList
                        If File.Exists(Entry.Value) Then
                            FileSystem.DeleteFile(Entry.Value, FileIO.UIOption.AllDialogs, FileIO.RecycleOption.SendToRecycleBin)
                            Log($"[Datapack] 更新后的数据包文件已存在，将会把它放入回收站：{Entry.Value}", LogLevel.Debug)
                        End If
                        If Directory.Exists(GetPathFromFullPath(Entry.Value)) Then
                            File.Move(Entry.Key, Entry.Value)
                            FinishedFileNames.Add(GetFileNameFromPath(Entry.Value))
                        Else
                            Log($"[Datapack] 更新后的目标文件夹已被删除：{Entry.Value}", LogLevel.Debug)
                        End If
                    Next
                Catch ex As OperationCanceledException
                    Log(ex, "替换旧版数据包文件时被主动取消")
                End Try
            End Sub))

            '结束处理
            Dim Loader As New LoaderCombo(Of IEnumerable(Of LocalCompFile))($"数据包更新：{GetFolderNameFromPath(PageInstanceSavesLeft.CurrentSave)}", InstallLoaders)
            Dim PathDatapacks As String = PageInstanceSavesLeft.CurrentSave & "\datapacks\"

            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Select Case FinishedFileNames.Count
                            Case 0
                                Log($"[DatapackUpdate] 没有数据包被成功更新")
                            Case 1
                                Hint($"已成功更新 {FinishedFileNames.Single}！", HintType.Finish)
                            Case Else
                                Hint($"已成功更新 {FinishedFileNames.Count} 个数据包！", HintType.Finish)
                        End Select
                    Case LoadState.Failed
                        Hint("数据包更新失败：" & Loader.Error.Message, HintType.Critical)
                    Case LoadState.Aborted
                        Hint("数据包更新已中止！", HintType.Info)
                    Case Else
                        Return
                End Select

                Log($"[DatapackUpdate] 已从正在进行数据包更新的文件夹列表移除：{PathDatapacks}")
                UpdatingVersions.Remove(PathDatapacks)

                '清理缓存
                RunInNewThread(
                Sub()
                    Try
                        For Each TempFile In FileCopyList.Keys
                            If File.Exists(TempFile) Then File.Delete(TempFile)
                        Next
                    Catch ex As Exception
                        Log(ex, "清理数据包更新缓存失败")
                    End Try
                End Sub, "Clean Datapack Update Cache", ThreadPriority.BelowNormal)
            End Sub

            '启动加载器
            Log($"[DatapackUpdate] 开始更新 {DatapackList.Count} 个数据包：{PathDatapacks}")
            UpdatingVersions.Add(PathDatapacks)
            Loader.Start()
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
            ReloadDatapackFileList(True)
        Catch ex As Exception
            Log(ex, "初始化数据包更新失败")
        End Try
    End Sub

    '删除
    Private Sub BtnSelectDelete_Click() Handles BtnSelectDelete.Click
        DeleteDatapacks(CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath)))
        ChangeAllSelected(False)
    End Sub

    Private Sub DeleteDatapacks(DatapackList As IEnumerable(Of LocalCompFile))
        Try
            Dim IsSuccessful As Boolean = True
            Dim IsShiftPressed As Boolean = Keyboard.IsKeyDown(Key.LeftShift) OrElse Keyboard.IsKeyDown(Key.RightShift)

            '确认需要删除的文件
            DatapackList = DatapackList.SelectMany(
            Function(Target As LocalCompFile)
                If Target.State = LocalCompFile.LocalFileStatus.Fine Then
                    Return {Target.Path, Target.Path & ".disabled"}
                Else
                    Return {Target.Path, Target.RawPath}
                End If
            End Function).Distinct.Where(Function(m) File.Exists(m)).Select(Function(m) New LocalCompFile(m)).ToList()

            '实际删除文件
            For Each DatapackEntity In DatapackList
                Try
                    If IsShiftPressed Then
                        File.Delete(DatapackEntity.Path)
                    Else
                        FileSystem.DeleteFile(DatapackEntity.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)
                    End If
                Catch ex As OperationCanceledException
                    Log(ex, "删除数据包被主动取消")
                    ReloadDatapackFileList(True)
                    Return
                Catch ex As Exception
                    Log(ex, $"删除数据包失败（{DatapackEntity.Path}）", LogLevel.Msgbox)
                    IsSuccessful = False
                End Try

                '取消选中
                SelectedDatapacks.Remove(DatapackEntity.RawPath)
                '更改 Loader 和 UI 中的列表
                CompResourceListLoader.Output.Remove(DatapackEntity)
                SearchResult?.Remove(DatapackEntity)
                DatapackItems.Remove(DatapackEntity.RawPath)
                Dim IndexOfUi As Integer = PanList.Children.IndexOf(PanList.Children.OfType(Of MyLocalCompItem).FirstOrDefault(Function(i) i.Entry.Equals(DatapackEntity)))
                If IndexOfUi >= 0 Then PanList.Children.RemoveAt(IndexOfUi)
            Next

            RefreshBars()
            If Not IsSuccessful Then
                Hint("由于文件被占用，删除失败，请尝试关闭正在运行的游戏后再试！", HintType.Critical)
                ReloadDatapackFileList(True)
            ElseIf PanList.Children.Count = 0 Then
                ReloadDatapackFileList(True)
            Else
                RefreshBars()
            End If

            If Not IsSuccessful Then Return
            If IsShiftPressed Then
                If DatapackList.Count = 1 Then
                    Hint($"已彻底删除 {DatapackList.Single.FileName}！", HintType.Finish)
                Else
                    Hint($"已彻底删除 {DatapackList.Count} 个项目！", HintType.Finish)
                End If
            Else
                If DatapackList.Count = 1 Then
                    Hint($"已将 {DatapackList.Single.FileName} 删除到回收站！", HintType.Finish)
                Else
                    Hint($"已将 {DatapackList.Count} 个项目删除到回收站！", HintType.Finish)
                End If
            End If
        Catch ex As OperationCanceledException
            Log(ex, "删除数据包被主动取消")
            ReloadDatapackFileList(True)
        Catch ex As Exception
            Log(ex, "删除数据包出现未知错误", LogLevel.Feedback)
            ReloadDatapackFileList(True)
        End Try
        LoaderRun(LoaderFolderRunType.UpdateOnly)
    End Sub

    '取消选择
    Private Sub BtnSelectCancel_Click() Handles BtnSelectCancel.Click
        ChangeAllSelected(False)
    End Sub

    '收藏
    Private Sub BtnSelectFavorites_Click(sender As Object, e As RouteEventArgs) Handles BtnSelectFavorites.Click
        Dim Selected As List(Of CompProject) = CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath) AndAlso m.Comp IsNot Nothing).Select(Function(i) i.Comp).ToList
        CompFavorites.ShowMenu(Selected, sender)
    End Sub

    '分享
    Private Sub BtnSelectShare_Click() Handles BtnSelectShare.Click
        Dim ShareList As HashSet(Of String) = CompResourceListLoader.Output.Where(Function(m) SelectedDatapacks.Contains(m.RawPath) AndAlso m.Comp IsNot Nothing).Select(Function(i) i.Comp.Id).ToHashSet()
        ClipboardSet(CompFavorites.GetShareCode(ShareList))
        ChangeAllSelected(False)
    End Sub

#End Region

#Region "单个资源项"

    '详情
    Public Sub Info_Click(sender As Object, e As EventArgs)
        Try
            Dim DatapackEntry As LocalCompFile = CType(If(TypeOf sender Is MyIconButton, sender.Tag, sender), MyLocalCompItem).Entry

            '加载失败信息
            If DatapackEntry.State = LocalCompFile.LocalFileStatus.Unavailable Then
                MyMsgBox("无法读取此数据包的信息。" & vbCrLf & vbCrLf & "详细的错误信息：" & DatapackEntry.FileUnavailableReason.Message, "数据包读取失败")
                Return
            End If

            If DatapackEntry.Comp IsNot Nothing Then
                '跳转到数据包下载页面
                FrmMain.PageChange(New FormMain.PageStackData With {
                    .Page = FormMain.PageType.CompDetail,
                    .Additional = {DatapackEntry.Comp, New List(Of String), PageInstanceLeft.Instance.Info.VanillaName,
                        CompLoaderType.Minecraft, CompType.DataPack}
                })
            Else
                '获取信息
                Dim ContentLines As New List(Of String)

                If DatapackEntry.Description IsNot Nothing Then ContentLines.Add(DatapackEntry.Description & vbCrLf)
                If DatapackEntry.Authors IsNot Nothing Then ContentLines.Add("作者：" & DatapackEntry.Authors)
                ContentLines.Add("文件：" & DatapackEntry.FileName & "（" & GetString(GetDatapackFileInfo(DatapackEntry.Path).Length) & "）")
                If DatapackEntry.Version IsNot Nothing Then ContentLines.Add("版本：" & DatapackEntry.Version)

                Dim DebugInfo As New List(Of String)
                If DatapackEntry.ModId IsNot Nothing Then
                    DebugInfo.Add("数据包 ID：" & DatapackEntry.ModId)
                End If
                If DebugInfo.Any Then
                    ContentLines.Add("")
                    ContentLines.AddRange(DebugInfo)
                End If

                '显示详情信息
                If DatapackEntry.Url Is Nothing Then
                    MyMsgBox(Join(ContentLines, vbCrLf), DatapackEntry.Name, "返回")
                Else
                    If MyMsgBox(Join(ContentLines, vbCrLf), DatapackEntry.Name, "打开官网", "返回") = 1 Then
                        OpenWebsite(DatapackEntry.Url)
                    End If
                End If
            End If
        Catch ex As Exception
            Log(ex, "获取数据包详情失败", LogLevel.Feedback)
        End Try
    End Sub

    '打开文件所在的位置
    Public Sub Open_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim ListItem As MyLocalCompItem = sender.Tag
            OpenExplorer(ListItem.Entry.Path)
        Catch ex As Exception
            Log(ex, "打开数据包文件位置失败", LogLevel.Feedback)
        End Try
    End Sub

    '删除
    Public Sub Delete_Click(sender As MyIconButton, e As EventArgs)
        Dim ListItem As MyLocalCompItem = sender.Tag
        DeleteDatapacks({ListItem.Entry})
    End Sub

    '启用
    Public Sub Enable_Click(sender As MyIconButton, e As EventArgs)
        Dim ListItem As MyLocalCompItem = sender.Tag
        ToggleDatapacks({ListItem.Entry}, True)
    End Sub

    '禁用
    Public Sub Disable_Click(sender As MyIconButton, e As EventArgs)
        Dim ListItem As MyLocalCompItem = sender.Tag
        ToggleDatapacks({ListItem.Entry}, False)
    End Sub

#End Region

#Region "搜索"

    Public ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(SearchBox.Text)
        End Get
    End Property
    Private SearchResult As List(Of LocalCompFile)
    Public Sub SearchRun() Handles SearchBox.TextChanged
        Try
            If IsSearching Then
                '构造请求
                Dim QueryList As New List(Of SearchEntry(Of LocalCompFile))
                For Each Entry As LocalCompFile In CompResourceListLoader.Output
                    Dim SearchSource As New List(Of KeyValuePair(Of String, Double))
                    SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Name, 1))
                    SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.FileName, 1))
                    If Entry.Version IsNot Nothing Then
                        SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Version, 0.2))
                    End If
                    If Entry.Description IsNot Nothing AndAlso Entry.Description <> "" Then
                        SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Description, 0.4))
                    End If
                    If Entry.Comp IsNot Nothing Then
                        If Entry.Comp.RawName <> Entry.Name Then SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Comp.RawName, 1))
                        If Entry.Comp.TranslatedName <> Entry.Comp.RawName Then SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Comp.TranslatedName, 1))
                        If Entry.Comp.Description <> Entry.Description Then SearchSource.Add(New KeyValuePair(Of String, Double)(Entry.Comp.Description, 0.4))
                        SearchSource.Add(New KeyValuePair(Of String, Double)(String.Join("", Entry.Comp.Tags), 0.2))
                    End If
                    QueryList.Add(New SearchEntry(Of LocalCompFile) With {.Item = Entry, .SearchSource = SearchSource})
                Next
                '进行搜索
                SearchResult = Search(QueryList, SearchBox.Text, MaxBlurCount:=6, MinBlurSimilarity:=0.35).Select(Function(r) r.Item).ToList
            End If
            RefreshUI()
        Catch ex As Exception
            Log(ex, "搜索过程中发生异常", LogLevel.Debug)
        End Try
    End Sub

#End Region

End Class