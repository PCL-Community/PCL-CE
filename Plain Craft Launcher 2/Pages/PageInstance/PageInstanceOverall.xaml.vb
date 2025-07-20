Public Class PageInstanceOverall

    Private IsLoad As Boolean = False
    Private Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '更新设置
        ItemDisplayLogoCustom.Tag = "PCL\Logo.png"
        Reload()

        '非重复加载部分
        If IsLoad Then Return
        IsLoad = True
        PanDisplay.TriggerForceResize()

    End Sub

    Public ItemVersion As MyListItem
    ''' <summary>
    ''' 确保当前页面上的信息已正确显示。
    ''' </summary>
    Private Sub Reload()
        AniControlEnabled += 1

        '刷新设置项目
        ComboDisplayType.SelectedIndex = ReadIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
        BtnDisplayStar.Text = If(PageInstanceLeft.Instance.IsStar, "从收藏夹中移除", "加入收藏夹")
        BtnFolderMods.Visibility = If(PageInstanceLeft.Instance.Modable, Visibility.Visible, Visibility.Collapsed)
        '刷新实例显示
        PanDisplayItem.Children.Clear()
        ItemVersion = PageSelectRight.McVersionListItem(PageInstanceLeft.Instance)
        ItemVersion.IsHitTestVisible = False
        PanDisplayItem.Children.Add(ItemVersion)
        FrmMain.PageNameRefresh()
        '刷新实例图标
        ComboDisplayLogo.SelectedIndex = 0
        Dim Logo As String = ReadIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "Logo", "")
        Dim LogoCustom As Boolean = ReadIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "LogoCustom", "False")
        If LogoCustom Then
            For Each Selection As MyComboBoxItem In ComboDisplayLogo.Items
                If Selection.Tag = Logo OrElse (Selection.Tag = "PCL\Logo.png" AndAlso Logo.EndsWith("PCL\Logo.png")) Then
                    ComboDisplayLogo.SelectedItem = Selection
                    Exit For
                End If
            Next
        End If

        AniControlEnabled -= 1
    End Sub

#Region "卡片：个性化"

    '实例分类
    Private Sub ComboDisplayType_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayType.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        If ComboDisplayType.SelectedIndex <> 1 Then
            '改为不隐藏
            Try
                '若设置分类为可安装 Mod，则显示正常的 Mod 管理页面
                WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "DisplayType", ComboDisplayType.SelectedIndex)
                PageInstanceLeft.Instance.DisplayType = ReadIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Auto)
                FrmInstanceLeft.RefreshModDisabled()

                WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "修改实例分类失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
            End Try
            Reload() '更新 “打开 Mod 文件夹” 按钮
        Else
            '改为隐藏
            Try
                If Not Setup.Get("HintHide") Then
                    If MyMsgBox("确认要从实例列表中隐藏该实例吗？隐藏该实例后，它将不再出现于 PCL 显示的实例列表中。" & vbCrLf & "此后，在实例列表页面按下 F11 才可以查看被隐藏的实例。", "隐藏实例提示",, "取消") <> 1 Then
                        ComboDisplayType.SelectedIndex = 0
                        Return
                    End If
                    Setup.Set("HintHide", True)
                End If
                WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "DisplayType", McInstanceCardType.Hidden)
                WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
                LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            Catch ex As Exception
                Log(ex, "隐藏实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Feedback)
            End Try
        End If
    End Sub

    '更改描述
    Private Sub BtnDisplayDesc_Click(sender As Object, e As EventArgs) Handles BtnDisplayDesc.Click
        Try
            Dim OldInfo As String = ReadIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "CustomInfo")
            Dim NewInfo As String = MyMsgBoxInput("更改描述", "修改实例的描述文本，留空则使用 PCL 的默认描述。", OldInfo, New ObjectModel.Collection(Of Validate), "默认描述")
            If NewInfo IsNot Nothing AndAlso OldInfo <> NewInfo Then WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "CustomInfo", NewInfo)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "实例 " & PageInstanceLeft.Instance.Name & " 描述更改失败", LogLevel.Msgbox)
        End Try
    End Sub

    '重命名实例
    Private Sub BtnDisplayRename_Click(sender As Object, e As EventArgs) Handles BtnDisplayRename.Click
        Try
            '确认输入的新名称
            Dim OldName As String = PageInstanceLeft.Instance.Name
            Dim OldPath As String = PageInstanceLeft.Instance.Path
            '修改此部分的同时修改快速安装的实例名检测*
            Dim NewName As String = MyMsgBoxInput("重命名实例", "", OldName, New ObjectModel.Collection(Of Validate) From {New ValidateFolderName(PathMcFolder & "versions", IgnoreCase:=False)})
            If String.IsNullOrWhiteSpace(NewName) Then Return
            Dim NewPath As String = PathMcFolder & "versions\" & NewName & "\"
            '获取临时中间名，以防止仅修改大小写的重命名失败
            Dim TempName As String = NewName & "_temp"
            Dim TempPath As String = PathMcFolder & "versions\" & TempName & "\"
            Dim IsCaseChangedOnly As Boolean = NewName.ToLower = OldName.ToLower
            '重新加载实例 Json 信息，避免 HMCL 项被合并
            Dim JsonObject As JObject
            Try
                JsonObject = GetJson(ReadFile(PageInstanceLeft.Instance.Path & PageInstanceLeft.Instance.Name & ".json"))
            Catch ex As Exception
                Log(ex, "重命名读取 Json 时失败")
                JsonObject = PageInstanceLeft.Instance.JsonObject
            End Try
            '重命名主文件夹
            My.Computer.FileSystem.RenameDirectory(OldPath, TempName)
            My.Computer.FileSystem.RenameDirectory(TempPath, NewName)
            '清理 ini 缓存
            IniClearCache(PageInstanceLeft.Instance.PathIndie & "options.txt")
            IniClearCache(PageInstanceLeft.Instance.Path & "PCL\Setup.ini")
            '遍历重命名所有文件与文件夹
            For Each Entry As DirectoryInfo In New DirectoryInfo(NewPath).EnumerateDirectories
                If Not Entry.Name.Contains(OldName) Then Continue For
                If IsCaseChangedOnly Then
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name & "_temp")
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName & "_temp", Entry.Name.Replace(OldName, NewName))
                Else
                    DeleteDirectory(NewPath & Entry.Name.Replace(OldName, NewName))
                    My.Computer.FileSystem.RenameDirectory(Entry.FullName, Entry.Name.Replace(OldName, NewName))
                End If
            Next
            For Each Entry As FileInfo In New DirectoryInfo(NewPath).EnumerateFiles
                If Not Entry.Name.Contains(OldName) Then Continue For
                If IsCaseChangedOnly Then
                    My.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name & "_temp")
                    My.Computer.FileSystem.RenameFile(Entry.FullName & "_temp", Entry.Name.Replace(OldName, NewName))
                Else
                    If File.Exists(NewPath & Entry.Name.Replace(OldName, NewName)) Then File.Delete(NewPath & Entry.Name.Replace(OldName, NewName))
                    My.Computer.FileSystem.RenameFile(Entry.FullName, Entry.Name.Replace(OldName, NewName))
                End If
            Next
            '替换实例设置文件中的路径
            If File.Exists(NewPath & "PCL\Setup.ini") Then
                WriteFile(NewPath & "PCL\Setup.ini", ReadFile(NewPath & "PCL\Setup.ini").Replace(OldPath, NewPath))
            End If
            '更改已选中的实例
            If ReadIni(PathMcFolder & "PCL.ini", "Version") = OldName Then
                WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            End If
            '更改实例 Json
            If File.Exists(NewPath & NewName & ".json") Then
                Try
                    JsonObject("id") = NewName
                    WriteFile(NewPath & NewName & ".json", JsonObject.ToString)
                Catch ex As Exception
                    Log(ex, "重命名实例 Json 失败")
                End Try
            End If
            '刷新与提示
            Hint("重命名成功！", HintType.Finish)
            PageInstanceLeft.Instance = New McInstance(NewName).Load()
            If Not IsNothing(McInstanceCurrent) AndAlso McInstanceCurrent.Equals(PageInstanceLeft.Instance) Then WriteIni(PathMcFolder & "PCL.ini", "Version", NewName)
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "重命名实例失败", LogLevel.Msgbox)
        End Try
    End Sub

    '实例图标
    Private Sub ComboDisplayLogo_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboDisplayLogo.SelectionChanged
        If Not (IsLoad AndAlso AniControlEnabled = 0) Then Return
        '选择 自定义 时修改图片
        Try
            If ComboDisplayLogo.SelectedItem Is ItemDisplayLogoCustom Then
                Dim FileName As String = SelectFile("常用图片文件(*.png;*.jpg;*.gif)|*.png;*.jpg;*.gif", "选择图片")
                If FileName = "" Then
                    Reload() '还原选项
                    Return
                End If
                CopyFile(FileName, PageInstanceLeft.Instance.Path & "PCL\Logo.png")
            Else
                File.Delete(PageInstanceLeft.Instance.Path & "PCL\Logo.png")
            End If
        Catch ex As Exception
            Log(ex, "更改自定义实例图标失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
        End Try
        '进行更改
        Try
            Dim NewLogo As String = ComboDisplayLogo.SelectedItem.Tag
            WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "Logo", NewLogo)
            WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "LogoCustom", Not NewLogo = "")
            '刷新显示
            WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "") '要求刷新缓存
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "更改实例图标失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Feedback)
        End Try
    End Sub

    '收藏夹
    Private Sub BtnDisplayStar_Click(sender As Object, e As EventArgs) Handles BtnDisplayStar.Click
        Try
            WriteIni(PageInstanceLeft.Instance.Path & "PCL\Setup.ini", "IsStar", Not PageInstanceLeft.Instance.IsStar)
            PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
            Reload()
            McInstanceListForceRefresh = True
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
        Catch ex As Exception
            Log(ex, "实例 " & PageInstanceLeft.Instance.Name & " 收藏状态更改失败", LogLevel.Msgbox)
        End Try
    End Sub

#End Region

#Region "卡片：快捷方式"

    '实例文件夹
    Private Sub BtnFolderVersion_Click() Handles BtnFolderVersion.Click
        OpenVersionFolder(PageInstanceLeft.Instance)
    End Sub
    Public Shared Sub OpenVersionFolder(Version As McInstance)
        OpenExplorer(Version.Path)
    End Sub

    '存档文件夹
    Private Sub BtnFolderSaves_Click() Handles BtnFolderSaves.Click
        Dim FolderPath As String = PageInstanceLeft.Instance.PathIndie & "saves\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

    'Mod 文件夹
    Private Sub BtnFolderMods_Click() Handles BtnFolderMods.Click
        Dim FolderPath As String = PageInstanceLeft.Instance.PathIndie & "mods\"
        Directory.CreateDirectory(FolderPath)
        OpenExplorer(FolderPath)
    End Sub

#End Region

#Region "卡片：管理"

    '导出启动脚本
    Private Sub BtnManageScript_Click() Handles BtnManageScript.Click
        Try
            '弹窗要求指定脚本的保存位置
            Dim SavePath As String = SelectSaveFile("选择脚本保存位置", "启动 " & PageInstanceLeft.Instance.Name & ".bat", "批处理文件(*.bat)|*.bat")
            If SavePath = "" Then Return
            '检查中断（等玩家选完弹窗指不定任务就结束了呢……）
            If McLaunchLoader.State = LoadState.Loading Then
                Hint("请在当前启动任务结束后再试！", HintType.Critical)
                Return
            End If
            '生成脚本
            If McLaunchStart(New McLaunchOptions With {.SaveBatch = SavePath, .Version = PageInstanceLeft.Instance}) Then
                If SelectedProfile.Type = McLoginType.Legacy Then
                    Hint("正在导出启动脚本……")
                Else
                    Hint("正在导出启动脚本……（注意，使用脚本启动可能会导致登录失效！）")
                End If
            End If
        Catch ex As Exception
            Log(ex, "导出启动脚本失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '补全文件
    Private Sub BtnManageCheck_Click(sender As Object, e As EventArgs) Handles BtnManageCheck.Click
        Try
            '忽略文件检查提示
            If ShouldIgnoreFileCheck(PageInstanceLeft.Instance) Then
                Hint("请先关闭 [实例设置 → 设置 → 高级启动选项 → 关闭文件校验]，然后再尝试补全文件！", HintType.Info)
                Return
            End If
            '重复任务检查
            For Each OngoingLoader In LoaderTaskbar
                If OngoingLoader.Name <> PageInstanceLeft.Instance.Name & " 文件补全" Then Continue For
                Hint("正在处理中，请稍候！", HintType.Critical)
                Return
            Next
            '启动
            Dim Loader As New LoaderCombo(Of String)(PageInstanceLeft.Instance.Name & " 文件补全", DlClientFix(PageInstanceLeft.Instance, True, AssetsIndexExistsBehaviour.AlwaysDownload))
            Loader.OnStateChanged =
            Sub()
                Select Case Loader.State
                    Case LoadState.Finished
                        Hint(Loader.Name & "成功！", HintType.Finish)
                    Case LoadState.Failed
                        Hint(Loader.Name & "失败：" & GetExceptionSummary(Loader.Error), HintType.Critical)
                    Case LoadState.Aborted
                        Hint(Loader.Name & "已取消！", HintType.Info)
                End Select
            End Sub
            Loader.Start(PageInstanceLeft.Instance.Name)
            LoaderTaskbarAdd(Loader)
            FrmMain.BtnExtraDownload.ShowRefresh()
            FrmMain.BtnExtraDownload.Ribble()
        Catch ex As Exception
            Log(ex, "尝试补全文件失败（" & PageInstanceLeft.Instance.Name & "）", LogLevel.Msgbox)
        End Try
    End Sub

    '重置
    Private Sub BtnManageRestore_Click(sender As Object, e As EventArgs) Handles BtnManageRestore.Click
        Try
            Dim CurrentVersion = PageInstanceLeft.Instance.Version
            If Not CurrentVersion.McCodeMain = 99 AndAlso VersionSortInteger(CurrentVersion.McName, "1.5.2") = -1 AndAlso CurrentVersion.HasForge Then
                Hint("该实例暂不支持重置！", HintType.Info)
                Exit Sub
            End If
            '确认操作
            If MyMsgBox("你确定要重置实例 " & PageInstanceLeft.Instance.Name & " 吗？" & vbCrLf & "PCL 将会尝试重新从互联网获取此实例的资源文件信息，并重新执行自动安装。", "实例重置确认", "确认", "取消") = 2 Then Exit Sub

            '备份实例核心文件
            CopyFile(PageInstanceLeft.Instance.Path + PageInstanceLeft.Instance.Name + ".json", PageInstanceLeft.Instance.Path + "PCLInstallBackups\" + PageInstanceLeft.Instance.Name + ".json")
            CopyFile(PageInstanceLeft.Instance.Path + PageInstanceLeft.Instance.Name + ".jar", PageInstanceLeft.Instance.Path + "PCLInstallBackups\" + PageInstanceLeft.Instance.Name + ".jar")
            '提交安装申请
            Dim Request As New McInstallRequest With {
                .TargetInstanceName = PageInstanceLeft.Instance.Name,
                .TargetInstanceFolder = $"{PathMcFolder}versions\{PageInstanceLeft.Instance.Name}\",
                .MinecraftName = CurrentVersion.McName,
                .OptiFineEntry = If(CurrentVersion.HasOptiFine, New DlOptiFineListEntry With {.Inherit = CurrentVersion.McName, .NameDisplay = CurrentVersion.McName + " " + CurrentVersion.OptiFineVersion}, Nothing),
                .ForgeEntry = If(CurrentVersion.HasForge, New DlForgeVersionEntry(CurrentVersion.ForgeVersion, Nothing, Inherit:=CurrentVersion.McName) With {.Category = "installer"}, Nothing),
                .ForgeVersion = If(CurrentVersion.HasForge, CurrentVersion.ForgeVersion, Nothing),
                .NeoForgeVersion = If(CurrentVersion.HasNeoForge, CurrentVersion.NeoForgeVersion, Nothing),
                .CleanroomVersion = If(CurrentVersion.HasCleanroom, CurrentVersion.CleanroomVersion, Nothing),
                .FabricVersion = If(CurrentVersion.HasFabric, CurrentVersion.FabricVersion, Nothing),
                .QuiltVersion = If(CurrentVersion.HasQuilt, CurrentVersion.QuiltVersion, Nothing),
                .LiteLoaderEntry = If(CurrentVersion.HasLiteLoader, New DlLiteLoaderListEntry With {.Inherit = CurrentVersion.McName}, Nothing),
                .LegacyFabricVersion = If(CurrentVersion.HasLegacyFabric, CurrentVersion.LegacyFabricVersion, Nothing)
            }
            '.MinecraftJson = CurrentVersion.McName,
            If Not McInstall(Request, "重置") Then Exit Sub
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
        Catch ex As Exception
            Log(ex, "重置实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

    '测试游戏
    Private Sub BtnManageTest_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnManageTest.Click
        Try
            McLaunchStart(New McLaunchOptions With
                 {.Version = PageInstanceLeft.Instance, .Test = True})
            FrmMain.PageChange(FormMain.PageType.Launch)
        Catch ex As Exception
            Log(ex, "测试游戏失败", LogLevel.Feedback)
        End Try
    End Sub

    '删除实例
    '修改此代码时，同时修改 PageSelectRight 中的代码
    Private Sub BtnManageDelete_Click(sender As Object, e As EventArgs) Handles BtnManageDelete.Click
        Try
            Dim IsShiftPressed As Boolean = My.Computer.Keyboard.ShiftKeyDown
            Dim IsHintIndie As Boolean = PageInstanceLeft.Instance.State <> McInstanceState.Error AndAlso PageInstanceLeft.Instance.PathIndie <> PathMcFolder
            Select Case MyMsgBox($"你确定要{If(IsShiftPressed, "永久", "")}删除实例 {PageInstanceLeft.Instance.Name} 吗？" &
                        If(IsHintIndie, vbCrLf & "由于该实例开启了版本隔离，删除时该实例对应的存档、资源包、Mod 等文件也将被一并删除！", ""),
                        "实例删除确认", , "取消",, IsHintIndie OrElse IsShiftPressed)
                Case 1
                    IniClearCache(PageInstanceLeft.Instance.PathIndie & "options.txt")
                    IniClearCache(PageInstanceLeft.Instance.Path & "PCL\Setup.ini")
                    If IsShiftPressed Then
                        DeleteDirectory(PageInstanceLeft.Instance.Path)
                        Hint("实例 " & PageInstanceLeft.Instance.Name & " 已永久删除！", HintType.Finish)
                    Else
                        FileIO.FileSystem.DeleteDirectory(PageInstanceLeft.Instance.Path, FileIO.UIOption.OnlyErrorDialogs, FileIO.RecycleOption.SendToRecycleBin)
                        Hint("实例 " & PageInstanceLeft.Instance.Name & " 已删除到回收站！", HintType.Finish)
                    End If
                Case 2
                    Return
            End Select
            LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
            FrmMain.PageBack()
        Catch ex As OperationCanceledException
            Log(ex, "删除实例 " & PageInstanceLeft.Instance.Name & " 被主动取消")
        Catch ex As Exception
            Log(ex, "删除实例 " & PageInstanceLeft.Instance.Name & " 失败", LogLevel.Msgbox)
        End Try
    End Sub

#End Region

End Class
