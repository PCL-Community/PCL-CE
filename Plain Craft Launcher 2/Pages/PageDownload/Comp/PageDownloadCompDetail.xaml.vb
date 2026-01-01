
Imports PCL.Core.UI

Public Class PageDownloadCompDetail
    Private _compItem As MyCompItem = Nothing

#Region "加载器"

    Private _compFileLoader As New LoaderTask(Of Integer, List(Of CompFile))(
        "Comp File",
        Sub(task)
            LoadTargetFromAdditional()
            Dim result = CompFilesGet(_project.Id, _project.FromCurseForge)
            If task.IsAborted Then Return
            task.Output = result
        End Sub)

    '初始化加载器信息
    Private Sub PageDownloadCompDetail_Inited(sender As Object, e As EventArgs) Handles Me.Initialized
        LoadTargetFromAdditional()
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, _compFileLoader, AddressOf Load_OnFinish)
    End Sub
    Public Sub LoadTargetFromAdditional() Handles Me.Loaded
        _project = FrmMain.PageCurrent.Additional(0)
        _targetInstance = FrmMain.PageCurrent.Additional(2)
        _targetLoader = FrmMain.PageCurrent.Additional(3)
        _pageType = FrmMain.PageCurrent.Additional(4)
    End Sub
    Private _project As CompProject
    Private _targetInstance As String, _targetLoader As CompLoaderType
    ''' <summary>
    ''' 当前页面应展示的内容类别。可能为 Any。
    ''' </summary>
    Private _pageType As CompType
    '自动重试
    Private Sub Load_State(sender As Object, state As MyLoading.MyLoadingState, oldState As MyLoading.MyLoadingState) Handles Load.StateChanged
        Select Case _compFileLoader.State
            Case LoadState.Failed
                Dim errorMessage As String = ""
                If _compFileLoader.Error IsNot Nothing Then errorMessage = _compFileLoader.Error.Message
                If errorMessage.Contains("不是有效的 Json 文件") Then
                    Log("[Comp] 下载的文件 Json 列表损坏，已自动重试", LogLevel.Debug)
                    PageLoaderRestart()
                End If
        End Select
    End Sub
    '结果 UI 化
    Private Class CardSorter
        Implements IComparer(Of String)
        Public Topmost As String = ""
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            '相同
            If x = y Then Return 0
            '置顶
            If x = Topmost Then Return -1
            If y = Topmost Then Return 1
            '特殊版本
            Dim isXSpecial As Boolean = Not x.Contains(".")
            Dim isYSpecial As Boolean = Not y.Contains(".")
            If isXSpecial AndAlso isYSpecial Then Return String.Compare(x, y, StringComparison.Ordinal)
            If isXSpecial Then Return 1
            If isYSpecial Then Return -1
            '比较版本号
            Dim versionCodeSort = -CompareVersion(x.Replace(x.BeforeFirst(" ") & " ", ""), y.Replace(y.BeforeFirst(" ") & " ", ""))
            If versionCodeSort <> 0 Then Return versionCodeSort
            '比较全部
            Return -CompareVersion(x, y)
        End Function
        Public Sub New(Optional topmost As String = "")
            Me.Topmost = If(topmost, "")
        End Sub
    End Class

    Private _versionFilter As String
    Private GroupedDrop As Boolean '是否按 Drop 筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）
    Private GroupedOld As Boolean '是否折叠远古版本为一个选项
    '筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    Private Function GetResults() As List(Of CompFile)
        Dim results As List(Of CompFile) = _compFileLoader.Output
        If _pageType = CompType.Any Then
            results = results.Where(Function(r) r.Type <> CompType.Plugin).ToList
        ElseIf _pageType = CompType.Shader OrElse _pageType = CompType.ResourcePack Then
            '不筛选光影和资源包，否则原版光影会因为是资源包格式而被过滤（Meloong-Git/#6473）
        Else
            results = results.Where(Function(r) r.Type = _pageType).ToList
        End If
        Return results
    End Function
    Private Sub Load_OnFinish()
        Dim results = GetResults()

        '初始化筛选器
        Dim filters As List(Of String) = Nothing
        Dim updateFilters =
        Sub()
            filters = results.SelectMany(Function(v) v.GameVersions).Select(Function(v) GetGroupedVersionName(v, GroupedDrop, GroupedOld)).
                Distinct.OrderByDescending(Function(s) s, New VersionComparer).ToList
        End Sub

        '确定分组方式
        GroupedDrop = False : GroupedOld = False
        updateFilters()
        If filters.Count < 9 Then GoTo GroupDone
        GroupedDrop = True : GroupedOld = False
        updateFilters()
        If filters.Count < 9 Then GoTo GroupDone
        GroupedDrop = False : GroupedOld = True
        updateFilters()
        If filters.Count < 9 Then GoTo GroupDone
        GroupedDrop = True : GroupedOld = True
        updateFilters()
GroupDone:

        'UI 化筛选器
        PanFilter.Children.Clear()
        If filters.Count < 2 Then
            CardFilter.Visibility = Visibility.Collapsed
            _versionFilter = Nothing
        Else
            CardFilter.Visibility = Visibility.Visible
            filters.Insert(0, "全部")
            '转化为按钮
            For Each version As String In filters
                Dim newButton As New MyRadioButton With {
                    .Text = version, .Margin = New Thickness(2, 0, 2, 0), .ColorType = MyRadioButton.ColorState.Highlight}
                newButton.LabText.Margin = New Thickness(-2, 0, 10, 0)
                AddHandler newButton.Check,
                Sub(sender As MyRadioButton, raiseByMouse As Boolean)
                    _versionFilter = If(sender.Text = "全部", Nothing, sender.Text)
                    UpdateFilterResult()
                End Sub
                PanFilter.Children.Add(newButton)
            Next
            '自动选择
            Dim toCheck As MyRadioButton = Nothing
            If _targetInstance <> "" Then
                Dim targetFile = results.FirstOrDefault(Function(v) v.GameVersions.Contains(_targetInstance))
                If targetFile IsNot Nothing Then
                    Dim targetGroup = GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld)
                    For Each button As MyRadioButton In PanFilter.Children
                        If button.Text <> targetGroup Then Continue For
                        toCheck = button
                        Exit For
                    Next
                End If
            End If
            If toCheck Is Nothing Then toCheck = PanFilter.Children(0)
            toCheck.Checked = True
        End If

        '更新筛选结果（文件列表 UI 化）
        UpdateFilterResult()
    End Sub
    Private Sub UpdateFilterResult()
        Dim results = GetResults()

        Dim targetVersionText = If(_targetLoader <> CompLoaderType.Any, _targetLoader.ToString & " ", "")
        Dim targetCardName As String = If(_targetInstance <> "" OrElse _targetLoader <> CompLoaderType.Any,
            $"所选版本：{targetVersionText}{_targetInstance}", "")
        '归类到卡片下
        Dim dict As New SortedDictionary(Of String, List(Of CompFile))(New CardSorter(targetCardName))
        dict.Add("其他", New List(Of CompFile))
        Dim supportedLoaders As New List(Of Integer)([Enum].GetValues(GetType(CompLoaderType)))
        For Each version As CompFile In results
            For Each gameVersion In version.GameVersions
                '检查是否符合版本筛选器
                If _versionFilter IsNot Nothing AndAlso
                   GetGroupedVersionName(gameVersion, GroupedDrop, GroupedOld) <> _versionFilter Then Continue For
                '决定添加到哪个卡片
                Dim verName As String = GetGroupedVersionName(gameVersion, False, False)
                '遍历加入的加载器列表
                Dim loaders As New List(Of String)
                If _project.ModLoaders.Count > 1 AndAlso '工程至少有两个加载器
                    version.Type = CompType.Mod AndAlso '是 Mod
                    McInstanceInfo.IsFormatFit(verName) Then '不是 “快照版本” 之类的
                    For Each loader In version.ModLoaders
                        If loader = CompLoaderType.Quilt AndAlso Setup.Get("ToolDownloadIgnoreQuilt") Then Continue For
                        If supportedLoaders.Contains(loader) Then loaders.Add(Loader.ToString & " ")
                    Next
                End If
                If Not loaders.Any() Then loaders.Add("") '保底加一个空的，确保它在一张卡片里
                '实际添加
                For Each Loader In loaders
                    Dim TargetCard As String = Loader & verName
                    If Not dict.ContainsKey(TargetCard) Then dict.Add(TargetCard, New List(Of CompFile))
                    If Not dict(TargetCard).Contains(version) Then dict(TargetCard).Add(version)
                Next
            Next
        Next
        '添加筛选的版本的卡片
        If targetCardName <> "" AndAlso (_versionFilter Is Nothing OrElse GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld).StartsWithF(_versionFilter)) Then
            dict.Add(targetCardName, New List(Of CompFile))
            For Each version As CompFile In results
                If version.GameVersions.Contains(_targetInstance) AndAlso
                   (_targetLoader = CompLoaderType.Any OrElse version.ModLoaders.Contains(_targetLoader)) Then
                    '检查是否符合版本筛选器
                    If _versionFilter IsNot Nothing AndAlso
                        Not version.GameVersions.Any(Function(v) GetGroupedVersionName(v, GroupedDrop, GroupedOld) = _versionFilter) Then Continue For
                    If Not dict(targetCardName).Contains(version) Then dict(targetCardName).Add(version)
                End If
            Next
        End If
        '转化为 UI
        Try
            PanResults.Children.Clear()
            For Each pair As KeyValuePair(Of String, List(Of CompFile)) In dict
                If Not pair.Value.Any() Then Continue For
                If Pair.Key = TargetCardName.Replace("（所选版本）", "") Then Continue For
                '增加卡片
                Dim newCard As New MyCard With {.Title = pair.Key, .Margin = New Thickness(0, 0, 0, 15)} '9 是安装，8 是另存为
                Dim newStack As New StackPanel With {.Margin = New Thickness(20, MyCard.SwapedHeight, 18, 0), .VerticalAlignment = VerticalAlignment.Top, .RenderTransform = New TranslateTransform(0, 0), .Tag = pair.Value}
                newCard.Children.Add(newStack)
                newCard.InstallMethod = Sub(stack As StackPanel)
                                            stack.Tag = Sort(CType(stack.Tag, List(Of CompFile)), Function(a, b) a.ReleaseDate > b.ReleaseDate)
                                            Dim badDisplayName = CType(stack.Tag, List(Of CompFile)).Distinct(Function(a, b) a.DisplayName = b.DisplayName).Count <> CType(stack.Tag, List(Of CompFile)).Count
                                            If _project.Type = CompType.ModPack Then
                                                For Each item In stack.Tag
                                                    stack.Children.Add(CType(item, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Install_Click, AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                Next
                                            ElseIf _project.Type = CompType.World Then
                                                For Each item In stack.Tag
                                                    stack.Children.Add(CType(item, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.InstallWorld_Click, AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                Next
                                            Else
                                                CompFilesCardPreload(stack, stack.Tag)

                                                For Each item In stack.Tag
                                                    stack.Children.Add(CType(item, CompFile).ToListItem(AddressOf FrmDownloadCompDetail.Save_Click, BadDisplayName:=badDisplayName))
                                                Next
                                            End If
                                        End Sub
                newCard.SwapControl = newStack
                PanResults.Children.Add(newCard)
                '确定卡片是否展开
                If pair.Key = targetCardName OrElse
                   (FrmMain.PageCurrent.Additional IsNot Nothing AndAlso '#2761
                   CType(FrmMain.PageCurrent.Additional(1), List(Of String)).Contains(newCard.Title)) Then
                    newCard.StackInstall() '9 是安装，8 是另存为
                Else
                    newCard.IsSwapped = True
                End If
                '增加提示
                If pair.Key = "其他" Then
                    newStack.Children.Add(New MyHint With {.Text = "由于版本信息更新缓慢，可能无法识别刚更新的 MC 版本。几天后即可正常识别。", .Theme = MyHint.Themes.Yellow, .Margin = New Thickness(5, 0, 0, 8)})
                End If
            Next
            '如果只有一张卡片，展开第一张卡片
            If PanResults.Children.Count = 1 Then
                CType(PanResults.Children(0), MyCard).IsSwapped = False
            End If
        Catch ex As Exception
            Log(ex, "可视化工程下载列表出错", LogLevel.Feedback)
        End Try
    End Sub
    Private Function GetGroupedVersionName(name As String, groupedByDrop As Boolean, foldOld As Boolean) As String
        If name Is Nothing Then
            Return "其他"
        ElseIf name.Contains("w") Then
            Return "快照版"
        ElseIf Not McInstanceInfo.IsFormatFit(name) OrElse (foldOld AndAlso McInstanceInfo.VersionToDrop(name, True) < 120) Then
            Return "远古版"
        ElseIf groupedByDrop Then
            Return McInstanceInfo.DropToVersion(McInstanceInfo.VersionToDrop(name, True))
        Else
            Return name
        End If
    End Function

#End Region
    Private _isFirstInit As Boolean = True
    Private Sub Init() Handles Me.PageEnter
        AniControlEnabled += 1
        _project = FrmMain.PageCurrent.Additional(0)
        PanBack.ScrollToHome()
        '重启加载器
        If _isFirstInit Then
            '在 Me.Initialized 已经初始化了加载器，不再重复初始化
            _isFirstInit = False
        Else
            PageLoaderRestart(IsForceRestart:=True)
        End If
        '放置当前工程
        If _compItem IsNot Nothing Then PanIntro.Children.Remove(_compItem)
        _compItem = _project.ToCompItem(True, True)
        _compItem.CanInteraction = False
        _compItem.ShowFavoriteBtn = false
        _compItem.Margin = New Thickness(-7, -7, 0, 8)
        PanIntro.Children.Insert(0, _compItem)

        '决定按钮显示
        BtnIntroWeb.Text = If(_project.FromCurseForge, "CurseForge", "Modrinth")
        BtnIntroWiki.Visibility = If(_project.WikiId = 0, Visibility.Collapsed, Visibility.Visible)

        AniControlEnabled -= 1
    End Sub

    '整合包安装
    Public Sub Install_Click(sender As MyListItem, e As EventArgs)
        Try

            '获取基本信息
            Dim File As CompFile = sender.Tag
            Dim LoaderName As String = $"{If(_project.FromCurseForge, "CurseForge", "Modrinth")} 整合包下载：{_project.TranslatedName} "

            '获取实例名
            Dim PackName As String = _project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "").Replace("\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("""", "").Replace("： ", "：")
            Dim Validate As New ValidateFolderName(McFolderSelected & "versions")
            If Validate.Validate(PackName) <> "" Then PackName = ""
            Dim InstanceName As String = MyMsgBoxInput("输入实例名称", "", PackName, New ObjectModel.Collection(Of Validate) From {Validate})
            If String.IsNullOrEmpty(InstanceName) Then Return

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            Dim Target As String = $"{McFolderSelected}versions\{InstanceName}\原始整合包.{If(_project.FromCurseForge, "zip", "mrpack")}"
            Dim LogoFileAddress As String = MyImage.GetTempPath(_compItem.Logo)
            Loaders.Add(New LoaderDownload("下载整合包文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 10, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("准备安装整合包",
            Sub() ModpackInstall(Target, InstanceName, If(IO.File.Exists(LogoFileAddress), LogoFileAddress, Nothing), File.ProjectId)) With {.ProgressWeight = 0.1})

            '启动
            Dim Loader As New LoaderCombo(Of String)(LoaderName, Loaders) With {.OnStateChanged =
            Sub(MyLoader)
                Select Case MyLoader.State
                    Case LoadState.Failed
                        Hint(MyLoader.Name & "失败：" & MyLoader.Error.Message, HintType.Critical)
                    Case LoadState.Aborted
                        Hint(MyLoader.Name & "已取消！", HintType.Info)
                    Case LoadState.Loading
                        Return '不重新加载版本列表
                End Select
                McInstallFailedClearFolder(MyLoader)
            End Sub}
            Loader.Start(McFolderSelected & "versions\" & InstanceName & "\")
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "下载资源整合包失败", LogLevel.Feedback)
        End Try
    End Sub
    '世界下载
    Public Sub InstallWorld_Click(sender As MyListItem, e As EventArgs)
        Try

            '获取基本信息
            Dim File As CompFile = sender.Tag
            Dim LoaderName As String = $"{If(_project.FromCurseForge, "CurseForge", "Modrinth")} 世界下载：{_project.TranslatedName} "

            '确认默认保存位置
            Dim DefaultFolder As String = Nothing
            Dim SubFolder As String = "saves\"
            Dim IsVersionSuitable As Func(Of McInstance, Boolean) = Nothing
            '获取资源所需的加载器
            Dim AllowedLoaders As New List(Of CompLoaderType)
            If File.ModLoaders.Any Then
                AllowedLoaders = File.ModLoaders
            ElseIf _project.ModLoaders.Any Then
                AllowedLoaders = _project.ModLoaders
            End If
            Log($"[Comp] 世界要求的加载器种类：" & If(AllowedLoaders.Any(), AllowedLoaders.Join(" / "), "无要求"))
            '判断某个版本是否符合资源要求
            IsVersionSuitable =
                    Function(Version)
                        If Version Is Nothing Then Return False
                        If Not Version.IsLoaded Then Version.Load()
                        If File.GameVersions.Any(Function(v) v.Contains(".")) AndAlso
                               Not File.GameVersions.Any(Function(v) v.Contains(".") AndAlso v = Version.Version.VanillaName) Then Return False
                        '加载器
                        If Not AllowedLoaders.Any() Then Return True '无要求
                        Return False
                    End Function
            '获取常规资源默认下载位置
            If CachedFolder.ContainsKey(File.Type) AndAlso Not String.IsNullOrEmpty(CachedFolder(File.Type)) Then
                DefaultFolder = CachedFolder.GetOrDefault(File.Type, If(McInstanceSelected?.PathIndie, ExePath))
                Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}")
            ElseIf McInstanceSelected IsNot Nothing AndAlso IsVersionSuitable(McInstanceSelected) Then
                DefaultFolder = $"{McInstanceSelected.PathIndie}{SubFolder}"
                Directory.CreateDirectory(DefaultFolder)
                Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}")
            Else
                '查找所有可能的实例
                Dim NeedLoad As Boolean = McInstanceListLoader.State <> LoadState.Finished
                If NeedLoad Then
                    Hint("正在查找适合的游戏实例……")
                    LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                End If
                Dim SuitableVersions = McInstanceList.Values.SelectMany(Function(l) l).Where(Function(v) IsVersionSuitable(v)).
                            Select(Function(v) New DirectoryInfo($"{v.PathIndie}{SubFolder}"))
                If SuitableVersions.Any Then
                    Dim SelectedVersion = SuitableVersions.
                                OrderByDescending(Function(Dir) If(Dir.Exists, Dir.LastWriteTimeUtc, Date.MinValue)). '先按文件夹更改时间降序
                                ThenByDescending(Function(Dir) If(Dir.Exists, Dir.GetFiles().Length, -1)). '再按文件夹中的文件数量降序
                                First()
                    DefaultFolder = SelectedVersion.FullName
                    Directory.CreateDirectory(DefaultFolder)
                    Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}")
                Else
                    DefaultFolder = McFolderSelected
                    If NeedLoad Then
                        Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！")
                    Else
                        Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置")
                    End If
                End If
            End If

            Dim Target As String = SystemDialogs.SelectSaveFile("选择世界安装位置 (saves 文件夹)", File.FileName, "世界文件|" & "*.zip", DefaultFolder)
            If String.IsNullOrEmpty(Target) Then Return

            '构造步骤加载器
            Dim Loaders As New List(Of LoaderBase)
            Dim TargetPath As String = Target.BeforeLast("\")
            Dim LogoFileAddress As String = MyImage.GetTempPath(_compItem.Logo)
            Loaders.Add(New LoaderDownload("下载世界文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 10, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("安装世界", Sub() ExtractFile(Target, TargetPath, Encoding.UTF8)) With {.ProgressWeight = 0.1, .Block = True})
            Loaders.Add(New LoaderTask(Of Integer, Integer)("清理缓存", Sub() IO.File.Delete(Target)))

            '启动
            Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
            Loader.Start()
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()

        Catch ex As Exception
            Log(ex, "下载世界资源失败", LogLevel.Feedback)
        End Try
    End Sub
    '资源下载；整合包另存为
    Public Shared CachedFolder As New Dictionary(Of CompType, String) '仅在本次缓存的下载文件夹
    Public Sub Save_Click(sender As Object, e As EventArgs)
        Dim File As CompFile = If(TypeOf sender Is MyListItem, sender, sender.Parent).Tag
        RunInNewThread(
        Sub()
            Try
                Dim Desc As String = Nothing
                Select Case File.Type
                    Case CompType.ModPack : Desc = "整合包"
                    Case CompType.Mod : Desc = "Mod "
                    Case CompType.ResourcePack : Desc = "资源包"
                    Case CompType.Shader : Desc = "光影包"
                    Case CompType.DataPack : Desc = "数据包"
                    Case CompType.World : Desc = "世界"
                End Select
                '确认默认保存位置
                Dim DefaultFolder As String = Nothing
                If File.Type <> CompType.ModPack Then
                    Dim SubFolder As String = Nothing
                    Select Case File.Type
                        Case CompType.Mod : SubFolder = "mods\"
                        Case CompType.ResourcePack : SubFolder = "resourcepacks\"
                        Case CompType.Shader : SubFolder = "shaderpacks\"
                        Case CompType.World : SubFolder = "saves\"
                        Case CompType.DataPack : SubFolder = "" '导航到版本根目录
                    End Select
                    Dim IsVersionSuitable As Func(Of McInstance, Boolean) = Nothing
                    '获取资源所需的加载器
                    Dim AllowedLoaders As New List(Of CompLoaderType)
                    If File.ModLoaders.Any Then
                        AllowedLoaders = File.ModLoaders
                    ElseIf _project.ModLoaders.Any Then
                        AllowedLoaders = _project.ModLoaders
                    End If
                    Log($"[Comp] {Desc}要求的加载器种类：" & If(AllowedLoaders.Any(), AllowedLoaders.Join(" / "), "无要求"))
                    '判断某个版本是否符合资源要求
                    IsVersionSuitable =
                    Function(Version)
                        If Version Is Nothing Then Return False
                        If Not Version.IsLoaded Then Version.Load()
                        '只对 Mod 和数据包进行版本检测
                        If File.Type = CompType.Mod OrElse File.Type = CompType.DataPack Then
                            If File.GameVersions.Any(Function(v) v.Contains(".")) AndAlso
                               Not File.GameVersions.Any(Function(v) v.Contains(".") AndAlso v = Version.Version.VanillaName) Then Return False
                        End If
                        '加载器
                        If Not AllowedLoaders.Any() Then Return True '无要求
                        If AllowedLoaders.Contains(CompLoaderType.Forge) AndAlso Version.Version.HasForge Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.Fabric) AndAlso Version.Version.HasFabric OrElse Version.Version.HasLegacyFabric Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.NeoForge) AndAlso Version.Version.HasNeoForge Then Return True
                        If AllowedLoaders.Contains(CompLoaderType.LiteLoader) AndAlso Version.Version.HasLiteLoader Then Return True
                        Return False
                    End Function
                    '获取常规资源默认下载位置
                    If CachedFolder.ContainsKey(File.Type) AndAlso Not String.IsNullOrEmpty(CachedFolder(File.Type)) Then
                        DefaultFolder = CachedFolder.GetOrDefault(File.Type, If(McInstanceSelected?.PathIndie, ExePath))
                        Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}")
                    ElseIf McInstanceSelected IsNot Nothing AndAlso IsVersionSuitable(McInstanceSelected) Then
                        DefaultFolder = $"{McInstanceSelected.PathIndie}{SubFolder}"
                        Directory.CreateDirectory(DefaultFolder)
                        Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}")
                    Else
                        '查找所有可能的实例
                        Dim NeedLoad As Boolean = McInstanceListLoader.State <> LoadState.Finished
                        If NeedLoad Then
                            Hint("正在查找适合的游戏实例……")
                            LoaderFolderRun(McInstanceListLoader, McFolderSelected, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\", WaitForExit:=True)
                        End If
                        Dim SuitableVersions = McInstanceList.Values.SelectMany(Function(l) l).Where(Function(v) IsVersionSuitable(v)).
                            Select(Function(v) New DirectoryInfo($"{v.PathIndie}{SubFolder}"))
                        If SuitableVersions.Any Then
                            Dim SelectedVersion = SuitableVersions.
                                OrderByDescending(Function(Dir) If(Dir.Exists, Dir.LastWriteTimeUtc, Date.MinValue)). '先按文件夹更改时间降序
                                ThenByDescending(Function(Dir) If(Dir.Exists, Dir.GetFiles().Length, -1)). '再按文件夹中的文件数量降序
                                First()
                            DefaultFolder = SelectedVersion.FullName
                            Directory.CreateDirectory(DefaultFolder)
                            Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}")
                        Else
                            DefaultFolder = McFolderSelected
                            If NeedLoad Then
                                Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！")
                            Else
                                Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置")
                            End If
                        End If
                    End If
                End If
                '获取基本信息
                Dim FileName As String
                If _project.TranslatedName = _project.RawName Then
                    FileName = File.FileName
                Else
                    Dim ChineseName As String = _project.TranslatedName.BeforeFirst(" (").BeforeFirst(" - ").
                        Replace("\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜").Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("""", "").Replace("： ", "：")
                    Select Case Setup.Get("ToolDownloadTranslateV2")
                        Case 0
                            FileName = $"【{ChineseName}】{File.FileName}"
                        Case 1
                            FileName = $"[{ChineseName}] {File.FileName}"
                        Case 2
                            FileName = $"{ChineseName}-{File.FileName}"
                        Case 3
                            FileName = $"{File.FileName}-{ChineseName}"
                        Case Else
                            FileName = File.FileName
                    End Select
                End If
                If File.Type = CompType.Mod Then FileName = FileName.Replace("~", "-") '~ 会导致 Mixin 加载失败
                RunInUi(
                Sub()
                    '弹窗要求选择保存位置
                    Dim Target As String
                    Target = SystemDialogs.SelectSaveFile("选择保存位置", FileName,
                        Desc & "文件|" &
                        If(File.Type = CompType.Mod,
                            If(File.FileName.EndsWith(".litemod"), "*.litemod", "*.jar"),
                            If(File.FileName.EndsWith(".mrpack"), "*.mrpack", "*.zip")), DefaultFolder)
                    If Not Target.Contains("\") Then Return
                    '构造步骤加载器
                    Dim LoaderName As String = Desc & "下载：" & GetFileNameWithoutExtentionFromPath(Target) & " "
                    If Target <> DefaultFolder Then
                        If CachedFolder.ContainsKey(File.Type) Then
                            CachedFolder(File.Type) = GetPathFromFullPath(Target)
                        Else
                            CachedFolder.Add(File.Type, GetPathFromFullPath(Target))
                        End If
                    End If
                    Dim Loaders As New List(Of LoaderBase)
                    Loaders.Add(New LoaderDownload("下载文件", New List(Of NetFile) From {File.ToNetFile(Target)}) With {.ProgressWeight = 6, .Block = True})
                    '启动
                    Dim Loader As New LoaderCombo(Of Integer)(LoaderName, Loaders) With {.OnStateChanged = AddressOf LoaderStateChangedHintOnly}
                    Loader.Start(1)
                    LoaderTaskbarAdd(Loader)
                    FrmMain.BtnExtraDownload.ShowRefresh()
                    FrmMain.BtnExtraDownload.Ribble()
                End Sub)
            Catch ex As Exception
                Log(ex, "保存资源文件失败", LogLevel.Feedback)
            End Try
        End Sub, "Download CompDetail Save")
    End Sub

    Private Sub BtnIntroWeb_Click(sender As Object, e As EventArgs) Handles BtnIntroWeb.Click
        OpenWebsite(_project.Website)
    End Sub
    Private Sub BtnIntroWiki_Click(sender As Object, e As EventArgs) Handles BtnIntroWiki.Click
        OpenWebsite("https://www.mcmod.cn/class/" & _project.WikiId & ".html")
    End Sub
    Private Sub BtnIntroCopy_Click(sender As Object, e As EventArgs) Handles BtnIntroCopy.Click
        ClipboardSet(_compItem.LabTitle.Text & _compItem.LabTitleRaw.Text)
    End Sub
    Private Sub BtnFavorites_Click(sender As Object, e As EventArgs) Handles BtnFavorites.Click
        CompFavorites.ShowMenu(_project, sender)
    End Sub
    Private Sub BtnIntroLinkCopy_Click(sender As Object, e As EventArgs) Handles BtnIntroLinkCopy.Click
        CompClipboard.CurrentText = _project.Website
        ClipboardSet(_project.Website)
    End Sub
    '翻译简介
    Private Async Sub BtnTranslate_Click(sender As Object, e As EventArgs) Handles BtnTranslate.Click
        Hint($"正在获取 {_project.TranslatedName} 的简介译文……")
        Dim ChineseDescription = Await _project.ChineseDescription
        If ChineseDescription Is Nothing Then Return
        MyMsgBox($"原文：{_project.Description}{Environment.NewLine}译文：{ChineseDescription}")
    End Sub

    ''' <summary>
    ''' 刷新收藏按钮的显示状态
    ''' </summary>
    Public Sub RefreshFavoriteButton()
        Try
            If _project IsNot Nothing Then
                ' 刷新顶部的项目卡片收藏状态
                If _compItem IsNot Nothing Then
                    _compItem.RefreshFavoriteStatus()
                End If
            End If
        Catch ex As Exception
            Log(ex, "刷新收藏按钮状态时出错")
        End Try
    End Sub

End Class
