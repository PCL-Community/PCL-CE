Imports System.IO
Imports System.IO.Compression
Imports System.Reflection
Imports System.Linq
Imports System.Text
Imports PCL.Core.UI
Imports PCL.Core.UI.SystemDialogs
Imports System.Windows.Input

Public Class PageInstanceSavesDatapacks
    Implements IRefreshable

#Region "变量声明"
    Private IsLoad As Boolean = False
    Private DatapackItems As New Dictionary(Of String, MyLocalCompItem)
    Private SelectedDatapacks As New HashSet(Of String)
    Private SearchResult As List(Of LocalCompFile)
    Private _Filter As FilterType = FilterType.All
    Private _isBottomBarVisible As Boolean = False
    Private _lastSelectedCount As Integer = 0
    Private _bottomBarBasePosition As Double = -10
    Private _isBarAnimationRunning As Boolean = False
    Private Const _bottomBarFinalY As Double = -25
    Private ReadOnly _uiLock As New Object()

    Private CurrentSortMethod As SortMethod = SortMethod.FileName
    Private ReadOnly SortLock As New Object

    Private Enum SortMethod
        FileName
        CreateTime
        FileSize
    End Enum

    Public Enum FilterType As Integer
        All = 0
        Enabled = 1
        Disabled = 2
        CanUpdate = 3
    End Enum
#End Region

#Region "初始化"
    Public Sub New()
        InitializeComponent()
        CardSelect.Visibility = Visibility.Collapsed
        CardSelect.Opacity = 0
        TransSelect.Y = _bottomBarBasePosition
        SetSortMethod(SortMethod.FileName)
    End Sub

    Private Sub Page_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ' 启用拖放
        Me.AllowDrop = True

        PanLoad.Visibility = Visibility.Visible
        PanAllBack.Visibility = Visibility.Collapsed

        SelectedDatapacks.Clear()
        _lastSelectedCount = 0
        _isBottomBarVisible = False

        CardSelect.Visibility = Visibility.Collapsed
        CardSelect.Opacity = 0
        TransSelect.Y = _bottomBarBasePosition

        SetSortMethod(SortMethod.FileName)
        ReloadDatapackList(True)
    End Sub

    Public Sub Refresh() Implements IRefreshable.Refresh
        ReloadDatapackList(True)
    End Sub

    Public Sub ReloadDatapackList(Optional ForceReload As Boolean = False)
        Try
            Dim datapacksPath As String = System.IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks")
            If Not Directory.Exists(datapacksPath) Then
                Directory.CreateDirectory(datapacksPath)
            End If
            LoadDatapacks(datapacksPath)
        Catch ex As Exception
            Log(ex, "加载数据包列表失败", LogLevel.Msgbox)
            RunInUi(Sub()
                        PanLoad.Visibility = Visibility.Collapsed
                        PanAllBack.Visibility = Visibility.Visible
                    End Sub)
        End Try
    End Sub

    Private Sub LoadDatapacks(datapacksPath As String)
        Try
            DatapackItems.Clear()
            SelectedDatapacks.Clear()

            If Not Directory.Exists(datapacksPath) Then
                RunInUi(Sub()
                            PanBack.Visibility = Visibility.Collapsed
                            PanEmpty.Visibility = Visibility.Visible
                            PanLoad.Visibility = Visibility.Collapsed
                            PanAllBack.Visibility = Visibility.Visible
                            CardSelect.Visibility = Visibility.Collapsed
                            CardSelect.Opacity = 0
                            TransSelect.Y = _bottomBarBasePosition
                        End Sub)
                Return
            End If

            Dim datapackFiles As String() = Directory.GetFiles(datapacksPath, "*.zip*", SearchOption.TopDirectoryOnly)
            Dim datapackList As New List(Of LocalCompFile)

            For Each filePath In datapackFiles
                Try
                    datapackList.Add(New LocalCompFile(filePath))
                Catch ex As Exception
                    Log(ex, $"加载数据包失败: {filePath}")
                End Try
            Next

            If datapackList.Count > 0 Then
                For Each datapack In datapackList
                    Try
                        DatapackItems(datapack.Path) = BuildDatapackItem(datapack)
                    Catch ex As Exception
                        Log(ex, $"构建数据包UI项失败: {datapack.Path}")
                    End Try
                Next

                RunInUi(Sub()
                            PanBack.Visibility = Visibility.Visible
                            PanEmpty.Visibility = Visibility.Collapsed
                            Filter = FilterType.All
                            SearchBox.Text = ""
                            RefreshUI()
                            PanLoad.Visibility = Visibility.Collapsed
                            PanAllBack.Visibility = Visibility.Visible
                            If SelectedDatapacks.Count = 0 Then
                                CardSelect.Visibility = Visibility.Collapsed
                                CardSelect.Opacity = 0
                                TransSelect.Y = _bottomBarBasePosition
                            End If
                        End Sub)
            Else
                RunInUi(Sub()
                            PanBack.Visibility = Visibility.Collapsed
                            PanEmpty.Visibility = Visibility.Visible
                            PanLoad.Visibility = Visibility.Collapsed
                            PanAllBack.Visibility = Visibility.Visible
                            CardSelect.Visibility = Visibility.Collapsed
                            CardSelect.Opacity = 0
                            TransSelect.Y = _bottomBarBasePosition
                        End Sub)
            End If
        Catch ex As Exception
            Log(ex, "加载数据包列表时发生错误", LogLevel.Msgbox)
            RunInUi(Sub()
                        PanBack.Visibility = Visibility.Collapsed
                        PanEmpty.Visibility = Visibility.Visible
                        PanLoad.Visibility = Visibility.Collapsed
                        PanAllBack.Visibility = Visibility.Visible
                        CardSelect.Visibility = Visibility.Collapsed
                        CardSelect.Opacity = 0
                        TransSelect.Y = _bottomBarBasePosition
                    End Sub)
        End Try
    End Sub
#End Region

#Region "拖拽安装功能"
    Private Sub Page_DragEnter(sender As Object, e As DragEventArgs) Handles Me.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
            If files.Any(Function(f) Path.GetExtension(f).ToLower() = ".zip") Then
                e.Effects = DragDropEffects.Copy
                Mouse.OverrideCursor = Cursors.Hand
            Else
                e.Effects = DragDropEffects.None
                Mouse.OverrideCursor = Cursors.No
            End If
        Else
            e.Effects = DragDropEffects.None
            Mouse.OverrideCursor = Cursors.No
        End If
        e.Handled = True
    End Sub

    Private Sub Page_DragOver(sender As Object, e As DragEventArgs) Handles Me.DragOver
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim files() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
            If files.Any(Function(f) Path.GetExtension(f).ToLower() = ".zip") Then
                e.Effects = DragDropEffects.Copy
                Mouse.OverrideCursor = Cursors.Hand
            Else
                e.Effects = DragDropEffects.None
                Mouse.OverrideCursor = Cursors.No
            End If
        Else
            e.Effects = DragDropEffects.None
            Mouse.OverrideCursor = Cursors.No
        End If
        e.Handled = True
    End Sub

    Private Sub Page_DragLeave(sender As Object, e As DragEventArgs) Handles Me.DragLeave
        Mouse.OverrideCursor = Nothing
        e.Handled = True
    End Sub

    Private Sub Page_Drop(sender As Object, e As DragEventArgs) Handles Me.Drop
        Try
            Mouse.OverrideCursor = Nothing

            If e.Data.GetDataPresent(DataFormats.FileDrop) Then
                Dim files() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
                Dim zipFiles = files.Where(Function(f) Path.GetExtension(f).ToLower() = ".zip").ToArray()

                If zipFiles.Any() Then
                    InstallDatapacks(zipFiles)
                Else
                    Hint("拖拽的文件不是有效的zip格式数据包", HintType.Critical)
                End If
            End If
        Catch ex As Exception
            Log(ex, "拖拽安装数据包失败", LogLevel.Msgbox)
            Mouse.OverrideCursor = Nothing
        End Try
        e.Handled = True
    End Sub
#End Region

#Region "UI构建"
    Private Function BuildDatapackItem(datapack As LocalCompFile) As MyLocalCompItem
        Try
            AniControlEnabled += 1

            If datapack Is Nothing Then
                Throw New ArgumentNullException("datapack")
            End If

            Dim newItem As New MyLocalCompItem With {
                .SnapsToDevicePixels = True,
                .Entry = datapack,
                .Checked = False
            }

            EnsureCurrentSwipeIsNotNull(newItem)

            AddHandler newItem.Click, Sub(sender As MyLocalCompItem, e As EventArgs)
                                          If sender IsNot Nothing Then
                                              Dim wasChecked = sender.Checked
                                              sender.Checked = Not wasChecked

                                              If sender.Checked Then
                                                  If Not SelectedDatapacks.Contains(sender.Entry.Path) Then
                                                      SelectedDatapacks.Add(sender.Entry.Path)
                                                  End If
                                              Else
                                                  If SelectedDatapacks.Contains(sender.Entry.Path) Then
                                                      SelectedDatapacks.Remove(sender.Entry.Path)
                                                  End If
                                              End If

                                              RefreshBars()
                                          End If
                                      End Sub

            Dim BtnOpen As New MyIconButton With {
                .LogoScale = 1.05,
                .Logo = Logo.IconButtonOpen,
                .Tag = newItem,
                .ToolTip = "打开文件位置"
            }
            AddHandler BtnOpen.Click, AddressOf Open_Click

            Dim BtnInfo As New MyIconButton With {
                .LogoScale = 1,
                .Logo = Logo.IconButtonInfo,
                .Tag = newItem,
                .ToolTip = "详情"
            }
            AddHandler BtnInfo.Click, AddressOf Info_Click

            Dim actionButton As MyIconButton
            If datapack.State = LocalCompFile.LocalFileStatus.Fine Then
                actionButton = New MyIconButton With {
                    .LogoScale = 1,
                    .Logo = Logo.IconButtonStop,
                    .Tag = newItem,
                    .ToolTip = "禁用"
                }
                AddHandler actionButton.Click, AddressOf DisableSingle_Click
            Else
                actionButton = New MyIconButton With {
                    .LogoScale = 1,
                    .Logo = Logo.IconButtonCheck,
                    .Tag = newItem,
                    .ToolTip = "启用"
                }
                AddHandler actionButton.Click, AddressOf EnableSingle_Click
            End If

            Dim BtnDelete As New MyIconButton With {
                .LogoScale = 1,
                .Logo = Logo.IconButtonDelete,
                .Tag = newItem,
                .ToolTip = "删除"
            }
            AddHandler BtnDelete.Click, AddressOf Delete_Click

            Dim buttonsArray As MyIconButton() = {BtnInfo, BtnOpen, actionButton, BtnDelete}
            If newItem IsNot Nothing Then
                newItem.Buttons = buttonsArray
            End If

            AniControlEnabled -= 1
            Return newItem
        Catch ex As Exception
            AniControlEnabled -= 1
            Log(ex, "创建数据包UI项失败", LogLevel.Debug)
            Throw
        End Try
    End Function

    Private Sub EnsureCurrentSwipeIsNotNull(item As MyLocalCompItem)
        Try
            Dim currentSwipeProperty As PropertyInfo = item.GetType().GetProperty("CurrentSwipe")
            If currentSwipeProperty Is Nothing Then
                Return
            End If

            Dim currentValue As Object = currentSwipeProperty.GetValue(item)
            If currentValue Is Nothing Then
                Dim propertyType As Type = currentSwipeProperty.PropertyType
                Dim newInstance As Object = Activator.CreateInstance(propertyType)

                Dim targetFrmProperty As PropertyInfo = propertyType.GetProperty("TargetFrm")
                If targetFrmProperty IsNot Nothing Then
                    Try
                        targetFrmProperty.SetValue(newInstance, Me)
                    Catch
                        targetFrmProperty.SetValue(newInstance, Nothing)
                    End Try
                End If

                Dim swipingProperty As PropertyInfo = propertyType.GetProperty("Swiping")
                If swipingProperty IsNot Nothing AndAlso swipingProperty.PropertyType Is GetType(Boolean) Then
                    swipingProperty.SetValue(newInstance, False)
                End If

                currentSwipeProperty.SetValue(item, newInstance)
            End If
        Catch ex As Exception
            Log(ex, "设置 CurrentSwipe 属性时出错", LogLevel.Debug)
        End Try
    End Sub
#End Region

#Region "UI管理"
    Public Sub RefreshUI()
        If PanList Is Nothing Then
            Return
        End If

        Dim showingDatapacks As List(Of LocalCompFile)
        If IsSearching Then
            showingDatapacks = If(SearchResult, New List(Of LocalCompFile))
        Else
            showingDatapacks = DatapackItems.Values.Where(Function(i) i IsNot Nothing AndAlso i.Entry IsNot Nothing).Select(Function(i) i.Entry).ToList()
        End If

        showingDatapacks = showingDatapacks.Where(Function(d) d IsNot Nothing AndAlso CanPassFilter(d)).ToList()

        If showingDatapacks.Any() Then
            Dim sortMethod = GetSortMethod(CurrentSortMethod)
            showingDatapacks.Sort(Function(a, b) sortMethod(a, b))
        End If

        AniControlEnabled += 1
        If showingDatapacks.Count > 0 Then
            PanList.Visibility = Visibility.Visible
            PanList.Children.Clear()

            For Each datapack In showingDatapacks
                If Not DatapackItems.ContainsKey(datapack.Path) Then
                    Continue For
                End If
                Dim item = DatapackItems(datapack.Path)
                If item IsNot Nothing Then
                    If item.Parent IsNot Nothing Then
                        CType(item.Parent, Panel).Children.Remove(item)
                    End If
                    item.Checked = SelectedDatapacks.Contains(datapack.Path)
                    PanList.Children.Add(item)
                End If
            Next
        Else
            PanList.Visibility = Visibility.Collapsed
        End If
        AniControlEnabled -= 1

        SelectedDatapacks = New HashSet(Of String)(SelectedDatapacks.Where(Function(m) showingDatapacks.Any(Function(s) s.Path = m)))
        RefreshBars()
    End Sub

    Public Sub RefreshBars()
        SyncLock _uiLock
            Dim totalCount As Integer
            Dim enabledCount As Integer
            Dim disabledCount As Integer

            If IsSearching AndAlso SearchResult IsNot Nothing Then
                totalCount = Enumerable.Count(SearchResult)
                enabledCount = Enumerable.Count(SearchResult, Function(d) d.State = LocalCompFile.LocalFileStatus.Fine)
                disabledCount = Enumerable.Count(SearchResult, Function(d) d.State = LocalCompFile.LocalFileStatus.Disabled)
            Else
                totalCount = DatapackItems.Count
                enabledCount = Enumerable.Count(DatapackItems.Values, Function(i) i IsNot Nothing AndAlso i.Entry IsNot Nothing AndAlso i.Entry.State = LocalCompFile.LocalFileStatus.Fine)
                disabledCount = Enumerable.Count(DatapackItems.Values, Function(i) i IsNot Nothing AndAlso i.Entry IsNot Nothing AndAlso i.Entry.State = LocalCompFile.LocalFileStatus.Disabled)
            End If

            BtnFilterAll.Text = If(IsSearching, "搜索结果", "全部") & $" ({totalCount})"
            BtnFilterEnabled.Text = $"启用 ({enabledCount})"
            BtnFilterDisabled.Text = $"禁用 ({disabledCount})"
            BtnFilterCanUpdate.Text = $"可更新 (0)"

            Dim selectedCount As Integer = SelectedDatapacks.Count
            LabSelect.Text = $"已选择 {selectedCount} 个数据包"

            ' 如果选择数量没有变化，不执行动画
            If _lastSelectedCount = selectedCount Then Return

            ' 停止之前的动画
            If _isBarAnimationRunning Then
                AniStop("Mod Sidebar")
                _isBarAnimationRunning = False
            End If

            If selectedCount > 0 Then
                Dim selectedItems = DatapackItems.Values _
            .Where(Function(i) i IsNot Nothing AndAlso i.Entry IsNot Nothing AndAlso SelectedDatapacks.Contains(i.Entry.Path)) _
            .Select(Function(i) i.Entry) _
            .ToList()

                Dim hasEnabled As Boolean = selectedItems.Any(Function(d) d.State = LocalCompFile.LocalFileStatus.Fine)
                Dim hasDisabled As Boolean = selectedItems.Any(Function(d) d.State = LocalCompFile.LocalFileStatus.Disabled)

                BtnSelectEnable.IsEnabled = hasDisabled
                BtnSelectDisable.IsEnabled = hasEnabled
                BtnSelectUpdate.IsEnabled = False

                If Not _isBottomBarVisible Then
                    CardSelect.Visibility = Visibility.Visible
                    _isBottomBarVisible = True
                    _isBarAnimationRunning = True

                    Dim targetY As Double = _bottomBarFinalY
                    Dim currentY As Double = TransSelect.Y

                    AniStart({
                    AaOpacity(CardSelect, 1 - CardSelect.Opacity, 60),
                    AaTranslateY(CardSelect, targetY - currentY, 120, Ease:=New AniEaseOutFluent(AniEasePower.Weak)),
                    AaCode(Sub()
                               TransSelect.Y = targetY
                               _isBarAnimationRunning = False
                           End Sub, After:=True)
                }, "Mod Sidebar")
                End If
            Else
                If _isBottomBarVisible Then
                    _isBarAnimationRunning = True

                    Dim targetY As Double = _bottomBarBasePosition
                    Dim currentY As Double = TransSelect.Y

                    AniStart({
                    AaOpacity(CardSelect, -CardSelect.Opacity, 90),
                    AaTranslateY(CardSelect, targetY - currentY, 90, Ease:=New AniEaseInFluent(AniEasePower.Weak)),
                    AaCode(Sub()
                               CardSelect.Visibility = Visibility.Collapsed
                               TransSelect.Y = targetY
                               _isBottomBarVisible = False
                               _isBarAnimationRunning = False
                           End Sub, After:=True)
                }, "Mod Sidebar")
                End If
            End If

            _lastSelectedCount = selectedCount
        End SyncLock
    End Sub

    Private Sub ChangeAllSelected(value As Boolean)
        SyncLock _uiLock
            AniControlEnabled += 1
            SelectedDatapacks.Clear()
            For Each item In DatapackItems.Values
                If item IsNot Nothing Then
                    item.Checked = value
                    If value Then
                        SelectedDatapacks.Add(item.Entry.Path)
                    End If
                End If
            Next
            AniControlEnabled -= 1
            RefreshBars()
        End SyncLock
    End Sub
#End Region

#Region "排序功能"
    Private Sub SetSortMethod(Target As SortMethod)
        CurrentSortMethod = Target
        BtnSort.Text = $"排序：{GetSortName(Target)}"
        DoSort()
    End Sub

    Private Function GetSortName(Method As SortMethod) As String
        Select Case Method
            Case SortMethod.FileName
                Return "文件名"
            Case SortMethod.CreateTime
                Return "加入时间"
            Case SortMethod.FileSize
                Return "文件大小"
            Case Else
                Return "文件名"
        End Select
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

    Private Sub DoSort()
        SyncLock SortLock
            Try
                If PanList Is Nothing OrElse PanList.Children.Count < 2 Then
                    Exit Sub
                End If

                Dim items = PanList.Children.OfType(Of MyLocalCompItem)().ToList()
                Dim Method = GetSortMethod(CurrentSortMethod)

                Dim invalid = items.Where(Function(i) i.Entry Is Nothing).ToList()
                Dim valid = items.Except(invalid).ToList()

                valid.Sort(Function(x, y) Method(x.Entry, y.Entry))
                items = valid.Concat(invalid).ToList()

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
            Case SortMethod.CreateTime
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Dim aDate = GetFileCreationTime(a.Path)
                           Dim bDate = GetFileCreationTime(b.Path)
                           Return bDate.CompareTo(aDate)
                       End Function
            Case SortMethod.FileSize
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Dim aSize = GetFileSize(a.Path)
                           Dim bSize = GetFileSize(b.Path)
                           Return bSize.CompareTo(aSize)
                       End Function
            Case Else
                Return Function(a As LocalCompFile, b As LocalCompFile) As Integer
                           Return String.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase)
                       End Function
        End Select
    End Function

    Private Function GetFileCreationTime(filePath As String) As DateTime
        Try
            Return File.GetCreationTime(filePath)
        Catch
            Return DateTime.MinValue
        End Try
    End Function

    Private Function GetFileSize(filePath As String) As Long
        Try
            Return New FileInfo(filePath).Length
        Catch
            Return 0
        End Try
    End Function
#End Region

#Region "筛选和搜索"
    Public Property Filter As FilterType
        Get
            Return _Filter
        End Get
        Set(value As FilterType)
            If _Filter = value Then
                Return
            End If
            _Filter = value
            RefreshUI()
        End Set
    End Property

    Private Function CanPassFilter(datapack As LocalCompFile) As Boolean
        If datapack Is Nothing Then
            Return False
        End If

        Select Case Filter
            Case FilterType.All
                Return True
            Case FilterType.Enabled
                Return datapack.State = LocalCompFile.LocalFileStatus.Fine
            Case FilterType.Disabled
                Return datapack.State = LocalCompFile.LocalFileStatus.Disabled
            Case FilterType.CanUpdate
                Return datapack.CanUpdate
            Case Else
                Return False
        End Select
    End Function

    Private Sub ChangeFilter(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnFilterAll.Check, BtnFilterEnabled.Check, BtnFilterDisabled.Check, BtnFilterCanUpdate.Check
        Filter = sender.Tag
        RefreshUI()
        DoSort()
    End Sub

    Public ReadOnly Property IsSearching As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(SearchBox.Text)
        End Get
    End Property

    Public Sub SearchRun() Handles SearchBox.TextChanged
        If IsSearching Then
            SearchResult = DatapackItems.Values.Where(Function(item) item IsNot Nothing AndAlso item.Entry IsNot Nothing AndAlso
                (item.Entry.Name?.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                 item.Entry.FileName?.IndexOf(SearchBox.Text, StringComparison.OrdinalIgnoreCase) >= 0)) _
                .Select(Function(i) i.Entry).ToList()
        End If
        RefreshUI()
        DoSort()
    End Sub
#End Region

#Region "导出功能"
    ''' <summary>
    ''' 导出数据包信息
    ''' </summary>
    Private Sub BtnManageInfoExport_Click(sender As Object, e As RoutedEventArgs) Handles BtnManageInfoExport.Click
        Dim Choice = MyMsgBox("TXT 格式：仅导出数据包文件名称列表" & vbCrLf &
                          "CSV 格式：导出详细的数据包信息，包括文件名，数据包名称，版本信息，Pack Format等详细信息",
                            Title:="选择导出模式",
                            Button1:="TXT 格式",
                            Button2:="CSV 格式",
                            Button3:="取消")

        Select Case Choice
            Case 1 'TXT 格式
                ExportDatapackInfoTxt()
            Case 2 'CSV 格式
                ExportDatapackInfoCsv()
        End Select
    End Sub

    ''' <summary>
    ''' 导出 TXT 格式的数据包信息（仅文件名）
    ''' </summary>
    Private Sub ExportDatapackInfoTxt()
        Try
            ' 检查是否有数据包
            If DatapackItems.Count = 0 Then
                Hint("没有数据包可导出", HintType.Info)
                Return
            End If

            Dim exportContent As New List(Of String)

            ' 只添加数据包文件名
            For Each datapackItem In DatapackItems.Values
                If datapackItem IsNot Nothing AndAlso datapackItem.Entry IsNot Nothing Then
                    exportContent.Add(datapackItem.Entry.FileName)
                End If
            Next

            ' 使用实例名称作为文件名
            Dim fileName = $"{PageInstanceLeft.Instance.Name}已安装的数据包信息.txt"
            Dim savePath = SelectSaveFile("选择保存位置", fileName, "文本文件(*.txt)|*.txt")
            If String.IsNullOrWhiteSpace(savePath) Then Exit Sub

            File.WriteAllText(savePath, String.Join(vbCrLf, exportContent), Encoding.UTF8)
            OpenExplorer(savePath)
            Hint("数据包信息导出成功！", HintType.Finish)

        Catch ex As Exception
            Log(ex, "导出数据包信息失败", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 导出 CSV 格式的数据包信息
    ''' </summary>
    Private Sub ExportDatapackInfoCsv()
        Try
            ' 检查是否有数据包
            If DatapackItems.Count = 0 Then
                Hint("没有数据包可导出", HintType.Info)
                Return
            End If

            Dim exportContent As New List(Of String)
            ' 添加CSV标题行
            exportContent.Add("文件名,数据包名称,版本,描述,文件大小,Pack Format,文件路径")

            For Each datapackItem In DatapackItems.Values
                If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then Continue For

                Dim datapack = datapackItem.Entry
                Dim fileSize = GetFileSize(datapack.Path)
                Dim fileSizeText = If(fileSize > 0, GetFileSizeString(fileSize), "0 B")
                Dim packFormat = GetPackFormatFromZip(datapack.Path)
                Dim packFormatText = If(packFormat.HasValue, packFormat.ToString(), "未知")

                ' 将此处的fileName重命名为fileNameInLoop，避免变量隐藏
                Dim fileNameInLoop = EscapeCsvField(datapack.FileName)
                Dim name = EscapeCsvField(datapack.Name)
                Dim version = EscapeCsvField(If(datapack.Version, ""))
                Dim description = EscapeCsvField(If(datapack.Description, ""))
                Dim path = EscapeCsvField(datapack.Path)

                exportContent.Add($"{fileNameInLoop},{name},{version},{description},{fileSizeText},{packFormatText},{path}")
            Next

            ' 使用实例名称作为文件名
            Dim fileName = $"{PageInstanceLeft.Instance.Name}已安装的数据包信息.csv"
            Dim savePath = SelectSaveFile("选择保存位置", fileName, "CSV 文件(*.csv)|*.csv")
            If String.IsNullOrWhiteSpace(savePath) Then Exit Sub

            File.WriteAllText(savePath, String.Join(vbCrLf, exportContent), Encoding.UTF8)
            OpenExplorer(savePath)
            Hint("数据包详细信息导出成功！", HintType.Finish)

        Catch ex As Exception
            Log(ex, "导出数据包详细信息失败", LogLevel.Msgbox)
        End Try
    End Sub

    ''' <summary>
    ''' 从zip文件中读取pack.mcmeta并提取pack_format值
    ''' </summary>
    Private Function GetPackFormatFromZip(zipFilePath As String) As Integer?
        Try
            Using archive As ZipArchive = ZipFile.OpenRead(zipFilePath)
                Dim packMetaEntry = archive.GetEntry("pack.mcmeta")
                If packMetaEntry IsNot Nothing Then
                    Using streamReader As New StreamReader(packMetaEntry.Open())
                        Dim jsonContent = streamReader.ReadToEnd()
                        Return ParsePackFormatFromJson(jsonContent)
                    End Using
                End If
            End Using
        Catch ex As Exception
            Return Nothing
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' 从JSON内容中解析pack_format值
    ''' </summary>
    Private Function ParsePackFormatFromJson(jsonContent As String) As Integer?
        Try
            ' 简单的JSON解析，查找 "pack_format": 数字
            Dim pattern = """pack_format""\s*:\s*(\d+)"
            Dim match = System.Text.RegularExpressions.Regex.Match(jsonContent, pattern)

            If match.Success AndAlso match.Groups.Count > 1 Then
                Dim formatValue As Integer
                If Integer.TryParse(match.Groups(1).Value, formatValue) Then
                    Return formatValue
                End If
            End If
        Catch ex As Exception
        End Try

        Return Nothing
    End Function

    ''' <summary>
    ''' 转义 CSV 字段中的特殊字符
    ''' </summary>
    Private Function EscapeCsvField(field As String) As String
        If String.IsNullOrEmpty(field) Then Return ""

        If field.Contains(",") OrElse field.Contains("""") OrElse field.Contains(vbCr) OrElse field.Contains(vbLf) Then
            Return """" & field.Replace("""", """""") & """"
        End If

        Return field
    End Function

    ''' <summary>
    ''' 获取文件大小的友好显示格式
    ''' </summary>
    Private Function GetFileSizeString(bytes As Long) As String
        Dim units As String() = {"B", "KB", "MB", "GB", "TB"}
        Dim unitIndex As Integer = 0
        Dim size As Double = bytes

        While size >= 1024 AndAlso unitIndex < units.Length - 1
            size /= 1024
            unitIndex += 1
        End While

        Return $"{size:0.##} {units(unitIndex)}"
    End Function
#End Region

#Region "管理操作"
    Private Sub BtnManageOpen_Click(sender As Object, e As EventArgs) Handles BtnManageOpen.Click, BtnHintOpen.Click
        Try
            Dim datapacksPath As String = System.IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks\")
            Directory.CreateDirectory(datapacksPath)
            OpenExplorer(datapacksPath)
        Catch ex As Exception
            Log(ex, "打开数据包文件夹失败", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub BtnManageInstall_Click(sender As Object, e As EventArgs) Handles BtnManageInstall.Click, BtnHintInstall.Click
        Dim fileList As String() = SelectFiles("数据包文件(*.zip)|*.zip", "选择要安装的数据包")
        If fileList Is Nothing OrElse fileList.Length = 0 Then
            Exit Sub
        End If
        InstallDatapacks(fileList)
    End Sub

    Private Sub InstallDatapacks(filePaths As IEnumerable(Of String))
        Try
            Dim datapacksPath As String = System.IO.Path.Combine(PageInstanceSavesLeft.CurrentSave, "datapacks")
            Directory.CreateDirectory(datapacksPath)

            For Each filePath In filePaths
                Dim fileName As String = System.IO.Path.GetFileName(filePath)
                Dim destPath As String = System.IO.Path.Combine(datapacksPath, fileName)

                If File.Exists(destPath) Then
                    If MyMsgBox($"已存在同名数据包：{fileName}，是否要覆盖？", "文件覆盖确认", "覆盖", "取消") <> 1 Then
                        Continue For
                    End If
                End If

                File.Copy(filePath, destPath, True)
            Next

            Hint($"已安装 {filePaths.Count()} 个数据包！", HintType.Finish)
            ReloadDatapackList(True)
        Catch ex As Exception
            Log(ex, "安装数据包失败", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub BtnManageSelectAll_Click(sender As Object, e As EventArgs) Handles BtnManageSelectAll.Click
        ChangeAllSelected(SelectedDatapacks.Count < DatapackItems.Count)
    End Sub

    Private Sub BtnManageDownload_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageDownload.Click, BtnHintDownload.Click
        FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadDataPack)
        PageComp.TargetVersion = PageInstanceLeft.Instance
    End Sub

    Private Sub BtnSelectUpdate_Click(sender As Object, e As EventArgs) Handles BtnSelectUpdate.Click
        Dim updateList As List(Of LocalCompFile) = DatapackItems.Values _
            .Where(Function(i) i IsNot Nothing AndAlso i.Entry IsNot Nothing AndAlso SelectedDatapacks.Contains(i.Entry.Path) AndAlso i.Entry.CanUpdate) _
            .Select(Function(i) i.Entry) _
            .ToList()

        If updateList.Any() Then
            UpdateDatapacks(updateList)
            ChangeAllSelected(False)
        Else
            Hint("没有可更新的数据包", HintType.Info)
        End If
    End Sub

    Private Sub UpdateDatapacks(datapackList As IEnumerable(Of LocalCompFile))
        Hint("数据包更新功能尚未实现", HintType.Info)
    End Sub
#End Region

#Region "单个数据包操作"
    Public Sub EnableSingle_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim datapackItem = TryCast(sender.Tag, MyLocalCompItem)
            If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then
                Return
            End If
            EnableDatapacks({datapackItem.Entry})
        Catch ex As Exception
            Log(ex, "启用数据包失败", LogLevel.Msgbox)
        End Try
    End Sub

    Public Sub DisableSingle_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim datapackItem = TryCast(sender.Tag, MyLocalCompItem)
            If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then
                Return
            End If
            DisableDatapacks({datapackItem.Entry})
        Catch ex As Exception
            Log(ex, "禁用数据包失败", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub EnableDatapacks(datapackList As IEnumerable(Of LocalCompFile))
        Dim IsSuccessful As Boolean = True

        For Each datapack In datapackList
            If datapack.State <> LocalCompFile.LocalFileStatus.Disabled Then
                Continue For
            End If

            Try
                Dim newPath As String = datapack.Path.Replace(".disabled", "")
                If File.Exists(newPath) Then
                    If MyMsgBox($"已存在同名数据包文件：{System.IO.Path.GetFileName(newPath)}，是否要覆盖？", "文件覆盖确认", "覆盖", "取消") <> 1 Then
                        Continue For
                    End If
                    File.Delete(newPath)
                End If

                File.Move(datapack.Path, newPath)
                Dim newDatapack As New LocalCompFile(newPath)
                If DatapackItems.ContainsKey(datapack.Path) Then
                    DatapackItems.Remove(datapack.Path)
                    DatapackItems(newPath) = BuildDatapackItem(newDatapack)
                End If
            Catch ex As Exception
                Log(ex, $"启用数据包失败: {datapack.Path}")
                IsSuccessful = False
            End Try
        Next

        If IsSuccessful Then
            Hint($"已启用 {datapackList.Count()} 个数据包", HintType.Finish)
            RefreshUI()
        Else
            Hint("部分数据包启用失败，请检查文件权限", HintType.Critical)
            ReloadDatapackList(True)
        End If
    End Sub

    Private Sub DisableDatapacks(datapackList As IEnumerable(Of LocalCompFile))
        Dim IsSuccessful As Boolean = True

        For Each datapack In datapackList
            If datapack.State <> LocalCompFile.LocalFileStatus.Fine Then
                Continue For
            End If

            Try
                Dim newPath As String = datapack.Path & ".disabled"
                If File.Exists(newPath) Then
                    If MyMsgBox($"已存在同名数据包文件：{System.IO.Path.GetFileName(newPath)}，是否要覆盖？", "文件覆盖确认", "覆盖", "取消") <> 1 Then
                        Continue For
                    End If
                    File.Delete(newPath)
                End If

                File.Move(datapack.Path, newPath)
                Dim newDatapack As New LocalCompFile(newPath)
                If DatapackItems.ContainsKey(datapack.Path) Then
                    DatapackItems.Remove(datapack.Path)
                    DatapackItems(newPath) = BuildDatapackItem(newDatapack)
                End If
            Catch ex As Exception
                Log(ex, $"禁用数据包失败: {datapack.Path}")
                IsSuccessful = False
            End Try
        Next

        If IsSuccessful Then
            Hint($"已禁用 {datapackList.Count()} 个数据包", HintType.Finish)
            RefreshUI()
        Else
            Hint("部分数据包禁用失败，请检查文件权限", HintType.Critical)
            ReloadDatapackList(True)
        End If
    End Sub

    Public Sub Info_Click(sender As Object, e As EventArgs)
        Try
            Dim datapackItem As MyLocalCompItem = Nothing
            If TypeOf sender Is MyIconButton Then
                datapackItem = TryCast(CType(sender, MyIconButton).Tag, MyLocalCompItem)
            Else
                datapackItem = TryCast(sender, MyLocalCompItem)
            End If

            If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then
                Return
            End If

            Dim datapack = datapackItem.Entry
            Dim fileSize = GetFileSize(datapack.Path)
            Dim fileSizeText = If(fileSize > 0, GetFileSizeString(fileSize), "0 B")

            Dim infoText As New List(Of String) From {
                $"名称: {datapack.Name}",
                $"文件: {datapack.FileName} ({fileSizeText})"
            }

            MyMsgBox(String.Join(vbCrLf, infoText), "数据包信息", "确定")
        Catch ex As Exception
            Log(ex, "显示数据包信息失败", LogLevel.Feedback)
        End Try
    End Sub

    Public Sub Open_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim datapackItem = TryCast(sender.Tag, MyLocalCompItem)
            If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then
                Return
            End If
            OpenExplorer(datapackItem.Entry.Path)
        Catch ex As Exception
            Log(ex, "打开数据包位置失败", LogLevel.Feedback)
        End Try
    End Sub

    Public Sub Delete_Click(sender As MyIconButton, e As EventArgs)
        Try
            Dim datapackItem = TryCast(sender.Tag, MyLocalCompItem)
            If datapackItem Is Nothing OrElse datapackItem.Entry Is Nothing Then
                Return
            End If

            If MyMsgBox($"确定要删除数据包 '{datapackItem.Entry.Name}' 吗？", "删除确认", "删除", "取消") = 1 Then
                File.Delete(datapackItem.Entry.Path)
                ReloadDatapackList(True)
                Hint("数据包已删除", HintType.Finish)
            End If
        Catch ex As Exception
            Log(ex, "删除数据包失败", LogLevel.Msgbox)
        End Try
    End Sub

    Private Sub BtnSelectCancel_Click(sender As Object, e As EventArgs) Handles BtnSelectCancel.Click
        ChangeAllSelected(False)
    End Sub
#End Region

End Class