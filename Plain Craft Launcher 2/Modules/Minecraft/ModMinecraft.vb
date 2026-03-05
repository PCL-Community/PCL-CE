Imports System.IO.Compression
Imports System.Text.Json.Nodes
Imports PCL.Core.App
Imports PCL.Core.UI
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts

Public Module ModMinecraft

#Region "文件夹"

    ''' <summary>
    ''' 当前的 Minecraft 文件夹路径，以“\”结尾。
    ''' </summary>
    Public McFolderSelected As String
    ''' <summary>
    ''' 当前的 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderList As New List(Of McFolder)

    Public Class McFolder '必须是 Class，否则不是引用类型，在 ForEach 中不会得到刷新
        Public Name As String
        ''' <summary>
        ''' 文件夹路径。
        ''' 以 \ 结尾，例如 "D:\Game\MC\.minecraft\"。
        ''' </summary>
        Public Location As String
        Public Type As Types
        Public Enum Types
            Original
            RenamedOriginal
            Custom
        End Enum
        Public Overrides Function Equals(obj As Object) As Boolean
            If Not (TypeOf obj Is McFolder) Then Return False
            Dim folder = DirectCast(obj, McFolder)
            Return Name = folder.Name AndAlso Location = folder.Location AndAlso Type = folder.Type
        End Function
        Public Overrides Function ToString() As String
            Return Location
        End Function
    End Class

    ''' <summary>
    ''' 加载 Minecraft 文件夹列表。
    ''' </summary>
    Public McFolderListLoader As New LoaderTask(Of Integer, Integer)("Minecraft Folder List", AddressOf McFolderListLoadSub, Priority:=ThreadPriority.AboveNormal)
    Private Sub McFolderListLoadSub()
        Try
            '初始化
            Dim cacheMcFolderList = New List(Of McFolder)

#Region "读取自定义（Custom）文件夹，可能没有结果"

            '格式：TMZ 12>C://xxx/xx/|Test>D://xxx/xx/|名称>路径
            For Each folder As String In Setup.Get("LaunchFolders").Split("|")
                If folder = "" Then Continue For
                If Not folder.Contains(">") OrElse Not folder.EndsWithF("\") Then
                    Hint("无效的 Minecraft 文件夹：" & folder, HintType.Critical)
                    Continue For
                End If
                Dim name As String = folder.Split(">")(0)
                Dim path As String = folder.Split(">")(1)
                Try
                    CheckPermissionWithException(path)
                    cacheMcFolderList.Add(New McFolder With {.Name = name, .Location = path, .Type = McFolder.Types.Custom})
                Catch ex As Exception
                    MyMsgBox("失效的 Minecraft 文件夹：" & vbCrLf & path & vbCrLf & vbCrLf & ex.Message, "Minecraft 文件夹失效", IsWarn:=True)
                    Log(ex, $"无法访问 Minecraft 文件夹 {path}")
                End Try
            Next

#End Region

#Region "读取默认（Original）文件夹，即当前、官启文件夹，可能没有结果"

            Dim currentMcFolderList = New List(Of McFolder)
            Dim originalMcFolderList = New List(Of McFolder)
            '扫描当前文件夹
            Try
                If Directory.Exists(ExePath & "versions\") Then originalMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = ExePath, .Type = McFolder.Types.Original})
                For Each folder As DirectoryInfo In New DirectoryInfo(ExePath).GetDirectories
                    If Directory.Exists(folder.FullName & "versions\") OrElse folder.Name = ".minecraft" Then
                        Dim newCurrentFolder As New McFolder With {.Name = folder.Name, .Location = folder.FullName & "\", .Type = McFolder.Types.Original}
                        originalMcFolderList.Add(newCurrentFolder)
                        currentMcFolderList.Add(newCurrentFolder)
                    End If
                Next
            Catch ex As Exception
                Log(ex, "扫描 PCL 所在文件夹中是否有 MC 文件夹失败")
            End Try

            '扫描官启文件夹
            Dim MojangPath As String = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\"
            If (Not currentMcFolderList.Any OrElse MojangPath <> currentMcFolderList(0).Location) AndAlso '当前文件夹不是官启文件夹
                Directory.Exists(MojangPath & "versions\") Then '具有权限且存在 versions 文件夹
                originalMcFolderList.Add(New McFolder With {.Name = "官方启动器文件夹", .Location = MojangPath, .Type = McFolder.Types.Original})
            End If

            Log(cacheMcFolderList.Count & " 个自定义文件夹，" & originalMcFolderList.Count & " 个原始文件夹")

            Dim unAdded = False
            For Each newOriginalFolder As McFolder In originalMcFolderList
                For Each cacheFolder As McFolder In cacheMcFolderList
                    If cacheFolder.Location = newOriginalFolder.Location Then
                        If cacheFolder.Name <> newOriginalFolder.Name Then
                            cacheFolder.Type = McFolder.Types.RenamedOriginal
                        Else
                            cacheFolder.Type = McFolder.Types.Original
                        End If
                        unAdded = True
                    End If
                Next
                If Not unAdded Then cacheMcFolderList.Add(newOriginalFolder) '如果没有重命名，则添加当前文件夹
            Next

#End Region

#Region "读取自定义文件夹情况并写入设置"

            '将自定义文件夹情况同步到设置
            Dim Config As New List(Of String)
            For Each Folder As McFolder In cacheMcFolderList
                Config.Add(Folder.Name & ">" & Folder.Location)
            Next
            If Not Config.Any() Then Config.Add("") '防止 0 元素 Join 返回 Nothing
            Setup.Set("LaunchFolders", Join(Config, "|"))

#End Region

            '若没有可用文件夹，则创建 .minecraft
            If Not cacheMcFolderList.Any() Then
                Directory.CreateDirectory(ExePath & ".minecraft\versions\")
                cacheMcFolderList.Add(New McFolder With {.Name = "当前文件夹", .Location = ExePath & ".minecraft\", .Type = McFolder.Types.Original})
            End If

            For Each Folder As McFolder In cacheMcFolderList
#Region "更新 launcher_profiles.json"
                McFolderLauncherProfilesJsonCreate(Folder.Location)
#End Region
            Next
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomUtils.NextInt(200, 2000))

            '回设
            McFolderList = cacheMcFolderList

        Catch ex As Exception
            Log(ex, "加载 Minecraft 文件夹列表失败", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 为 Minecraft 文件夹创建 launcher_profiles.json 文件。
    ''' </summary>
    Public Sub McFolderLauncherProfilesJsonCreate(Folder As String)
        Try
            If File.Exists(Folder & "launcher_profiles.json") Then Return
            Dim ResultJson As String =
"{
    ""profiles"":  {
        ""PCL"": {
            ""icon"": ""Grass"",
            ""name"": ""PCL"",
            ""lastVersionId"": ""latest-release"",
            ""type"": ""latest-release"",
            ""lastUsed"": """ & Date.Now.ToString("yyyy'-'MM'-'dd") & "T" & Date.Now.ToString("HH':'mm':'ss") & ".0000Z""
        }
    },
    ""selectedProfile"": ""PCL"",
    ""clientToken"": ""23323323323323323323323323323333""
}"
            WriteFile(Folder & "launcher_profiles.json", ResultJson, Encoding:=Encoding.GetEncoding("GB18030"))
            Log("[Minecraft] 已创建 launcher_profiles.json：" & Folder)
        Catch ex As Exception
            Log(ex, "创建 launcher_profiles.json 失败（" & Folder & "）", LogLevel.Feedback)
        End Try
    End Sub

#End Region

#Region "实例处理"

    Public Const McInstanceCacheVersion As Integer = 30

    Private _mcInstanceSelected As McInstance
    ''' <summary>
    ''' 当前的 Minecraft 版本。
    ''' </summary>
    Public Property McInstanceSelected As McInstance
        Get
            Return _mcInstanceSelected
        End Get
        Set
            Static mcInstanceSelectedLast As Object = 0 '为 0 以保证与 Nothing 不相同，使得 UI 显示可以正常初始化
            If ReferenceEquals(mcInstanceSelectedLast, value) Then Return
            _mcInstanceSelected = value '由于有可能是 Nothing，导致无法初始化，才得这样弄一圈
            mcInstanceSelectedLast = value
            If value Is Nothing Then Return
            '重置缓存的 Mod 文件夹
            PageDownloadCompDetail.CachedFolder.Clear()
        End Set
    End Property

    Public Class McInstance

        ''' <summary>
        ''' 该实例的实例文件夹，以“\”结尾。
        ''' </summary>
        Public ReadOnly Property PathInstance As String
        ''' <summary>
        ''' 应用版本隔离后，该实例所对应的 Minecraft 根文件夹，以“\”结尾。
        ''' </summary>
        Public ReadOnly Property PathIndie As String
            Get
                If Setup.IsUnset("VersionArgumentIndieV2", instance:=Me) Then
                    If Not IsLoaded Then Load()
                    '决定该实例是否应该被隔离
                    Dim ShouldBeIndie =
                    Function() As Boolean
                        '从老的实例独立设置中迁移：-1 未决定，0 使用全局设置，1 手动开启，2 手动关闭
                        If Not Setup.IsUnset("VersionArgumentIndie", instance:=Me) AndAlso Setup.Get("VersionArgumentIndie", instance:=Me) > 0 Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：从老的实例独立设置中迁移")
                            Return Setup.Get("VersionArgumentIndie", instance:=Me) = 1
                        End If
                        '若实例文件夹下包含 mods 或 saves 文件夹，则自动开启版本隔离
                        Dim ModFolder As New DirectoryInfo(PathInstance & "mods\")
                        Dim SaveFolder As New DirectoryInfo(PathInstance & "saves\")
                        If (ModFolder.Exists AndAlso ModFolder.EnumerateFiles.Any) OrElse (SaveFolder.Exists AndAlso SaveFolder.EnumerateDirectories.Any) Then
                            Log($"[Minecraft] 版本隔离初始化（{Name}）：实例文件夹下存在 mods 或 saves 文件夹，自动开启")
                            Return True
                        End If
                        '根据全局的默认设置决定是否隔离
                        Dim IsRelease As Boolean = State <> McInstanceState.Fool AndAlso State <> McInstanceState.Old AndAlso State <> McInstanceState.Snapshot
                        Log($"[Minecraft] 版本隔离初始化（{Name}）：从全局默认设置中（{Setup.Get("LaunchArgumentIndieV2")}）判断，State {GetStringFromEnum(State)}，IsRelease {IsRelease}，Modable {Modable}")
                        Select Case Setup.Get("LaunchArgumentIndieV2")
                            Case 0 '关闭
                                Return False
                            Case 1 '仅隔离可安装 Mod 的实例
                                Return Info.HasLabyMod OrElse Modable
                            Case 2 '仅隔离非正式版
                                Return Not IsRelease
                            Case 3 '隔离非正式版与可安装 Mod 的实例
                                Return Info.HasLabyMod OrElse Modable OrElse Not IsRelease
                            Case Else '隔离所有实例
                                Return True
                        End Select
                    End Function
                    Setup.Set("VersionArgumentIndieV2", ShouldBeIndie(), instance:=Me)
                End If

                Return If(Setup.Get("VersionArgumentIndieV2", instance:=Me), PathInstance, McFolderSelected)
            End Get
        End Property

        ''' <summary>
        ''' 该实例的实例文件夹名称。
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If _Name Is Nothing AndAlso Not PathInstance = "" Then _Name = GetFolderNameFromPath(PathInstance)
                Return _Name
            End Get
        End Property
        Private _Name As String = Nothing

        ''' <summary>
        ''' 显示的描述文本。
        ''' </summary>
        Public Desc As String = "该实例未被加载，请向作者反馈此问题"
        ''' <summary>
        ''' 该实例的列表检查原始结果，不受自定义影响。
        ''' </summary>
        Public State As McInstanceState = McInstanceState.Error
        ''' <summary>
        ''' 显示的实例图标。
        ''' </summary>
        Public Logo As String
        ''' <summary>
        ''' 是否为收藏的实例。
        ''' </summary>
        Public IsStar As Boolean = False
        ''' <summary>
        ''' 强制实例分类，0 为未启用，1 为隐藏，2 及以上为其他普通分类。
        ''' </summary>
        Public DisplayType As McInstanceCardType = McInstanceCardType.Auto
        ''' <summary>
        ''' 该实例是否可以安装 Mod。
        ''' </summary>
        Public ReadOnly Property Modable As Boolean
            Get
                If Not IsLoaded Then Load()
                Return Info.HasFabric OrElse Info.HasLegacyFabric OrElse Info.HasQuilt OrElse Info.HasForge OrElse Info.HasLiteLoader OrElse Info.HasNeoForge OrElse Info.HasCleanroom OrElse
                    DisplayType = McInstanceCardType.API '#223
            End Get
        End Property
        ''' <summary>
        ''' 实例信息。
        ''' </summary>
        Public Property Info As McInstanceInfo
            Get
                If _info IsNot Nothing Then Return _info
                _info = New McInstanceInfo
#Region "获取游戏版本"
                Try
                    '获取发布时间并判断是否为老版本
                    Try
                        If JsonObject("releaseTime") Is Nothing Then
                            ReleaseTime = New Date(1970, 1, 1, 15, 0, 0) '未知版本也可能显示为 1970 年
                        Else
                            ReleaseTime = JsonObject("releaseTime").ToObject(Of Date)
                        End If
                        If ReleaseTime.Year > 2000 AndAlso ReleaseTime.Year < 2013 Then
                            _info.VanillaName = "Old"
                            GoTo VersionSearchFinish
                        End If
                    Catch
                        ReleaseTime = New Date(1970, 1, 1, 15, 0, 0)
                    End Try
                    '实验性快照
                    If If(JsonObject("type"), "") = "pending" Then
                        _info.VanillaName = "pending"
                        GoTo VersionSearchFinish
                    End If
                    '从 PCL 下载的版本信息中获取版本号
                    If JsonObject("clientVersion") IsNot Nothing Then
                        _info.VanillaName = JsonObject("clientVersion")
                        GoTo VersionSearchFinish
                    End If
                    '从 HMCL 下载的版本信息中获取版本号
                    If JsonObject("patches") IsNot Nothing Then
                        For Each patch As JObject In JsonObject("patches")
                            If If(patch("id"), "").ToString = "game" AndAlso patch("version") IsNot Nothing Then
                                _info.VanillaName = patch("version").ToString
                                GoTo VersionSearchFinish
                            End If
                        Next
                    End If
                    '从 Forge / NeoForge / LabyMod Arguments 中获取版本号
                    If JsonObject("arguments") IsNot Nothing Then
                        If JsonObject("arguments")("game") IsNot Nothing Then
                            Dim Mark As Boolean = False
                            For Each Argument In JsonObject("arguments")("game")
                                If Mark Then
                                    _info.VanillaName = Argument.ToString
                                    GoTo VersionSearchFinish
                                End If
                                If Argument.ToString = "--fml.mcVersion" Then Mark = True
                            Next
                        End If
                        If JsonObject("arguments")("jvm") IsNot Nothing Then
                            For Each Argument In JsonObject("arguments")("game")
                                Dim regexArgument = RegexSeek(Argument.ToString, RegexPatterns.LabyModVersion)
                                If regexArgument IsNot Nothing Then
                                    _info.VanillaName = regexArgument
                                    GoTo VersionSearchFinish
                                End If
                            Next
                        End If
                    End If
                    '从继承实例中获取版本号
                    If Not InheritInstanceName = "" Then
                        _info.VanillaName = If(JsonObject("jar"), "").ToString 'LiteLoader 优先使用 Jar
                        If _info.VanillaName = "" Then _info.VanillaName = InheritInstanceName
                        GoTo VersionSearchFinish
                    End If
                    '从下载地址中获取版本号
                    Dim regex As String = RegexSeek(If(JsonObject("downloads"), "").ToString, RegexPatterns.MinecraftDownloadUrlVersion)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '从 Forge 版本中获取版本号
                    Dim librariesString As String = JsonObject("libraries").ToString
                    regex = RegexSeek(librariesString, RegexPatterns.ForgeLibVersion)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '从 OptiFine 版本中获取版本号
                    regex = RegexSeek(librariesString, RegexPatterns.OptiFineLibVersion)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '从 Fabric / Quilt / Legacy Fabric 版本中获取版本号
                    regex = RegexSeek(librariesString, RegexPatterns.FabricLikeLibVersion)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '从 jar 项中获取版本号
                    If JsonObject("jar") IsNot Nothing Then
                        _info.VanillaName = JsonObject("jar").ToString
                        GoTo VersionSearchFinish
                    End If
                    '从 jar 文件的 version.json 中获取版本号
                    If JsonVersion?("name") IsNot Nothing Then
                        Dim jsonVerName As String = JsonVersion("name").ToString
                        If jsonVerName.Length < 32 Then '因为 wiki 说这玩意儿可能是个 hash，虽然我没发现
                            _info.VanillaName = jsonVerName
                            Log("[Minecraft] 从版本 jar 中的 version.json 获取到版本号：" & jsonVerName)
                            GoTo VersionSearchFinish
                        End If
                    End If
                    '从 JSON 的 ID 中获取
                    regex = RegexSeek(JsonObject("id"), RegexPatterns.MinecraftJsonVersion, RegularExpressions.RegexOptions.IgnoreCase)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '非准确的版本判断警告
                    Log("[Minecraft] 无法完全确认 MC 版本号的版本：" & Name)
                    _info.Reliable = False
                    '从文件夹名中获取
                    regex = RegexSeek(Name, RegexPatterns.MinecraftJsonVersion, RegularExpressions.RegexOptions.IgnoreCase)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '从 JSON 出现的版本号中获取
                    Dim JsonRaw As JObject = JsonObject.DeepClone()
                    JsonRaw.Remove("libraries")
                    Dim JsonRawText As String = JsonRaw.ToString
                    regex = RegexSeek(JsonRawText, RegexPatterns.MinecraftJsonVersion, RegularExpressions.RegexOptions.IgnoreCase)
                    If regex IsNot Nothing Then
                        _info.VanillaName = regex
                        GoTo VersionSearchFinish
                    End If
                    '无法获取
                    _info.VanillaName = "Unknown"
                    Desc = "PCL 无法识别该版本的 MC 版本号"
                Catch ex As Exception
                    Log(ex, "识别 Minecraft 版本时出错")
                    _info.VanillaName = "Unknown"
                    Desc = "无法识别：" & ex.Message
                End Try
#End Region
VersionSearchFinish:
                _info.VanillaName = _info.VanillaName.Replace("_unobfuscated", "").Replace(" Unobfuscated", "")
                '获取版本号
                If _info.VanillaName.StartsWithF("1.") Then
                    Dim segments = _info.VanillaName.Split(" _-.".ToCharArray)
                    _info.Vanilla = New Version(
                        Val(If(segments.Count >= 2, segments(1), "0")),
                        0,
                        Val(If(segments.Count >= 3, segments(2), "0")))
                ElseIf RegexCheck(_info.VanillaName, "^[2-9][0-9]\.") Then
                    Dim segments = _info.VanillaName.Split(" _-.".ToCharArray)
                    _info.Vanilla = New Version(
                        Val(segments(0)),
                        Val(If(segments.Count >= 2, segments(1), "0")),
                        Val(If(segments.Count >= 3, segments(2), "0")))
                Else
                    _info.Vanilla = New Version(9999, 0, 0)
                End If
                Return _info
            End Get
            Set(value As McInstanceInfo)
                _info = value
            End Set
        End Property
        Private _info As McInstanceInfo = Nothing

        ''' <summary>
        ''' 实例的发布时间。
        ''' </summary>
        Public ReleaseTime As New Date(1970, 1, 1, 15, 0, 0)

        ''' <summary>
        ''' 该实例的 JSON 文本。
        ''' </summary>
        Public Property JsonText As String
            Get
                '快速检查 JSON 是否以 { 开头、} 结尾；忽略空白字符
                Dim FastJsonCheck =
                Function(Json As String) As Boolean
                    Dim TrimedJson As String = Json.Trim()
                    Return TrimedJson.StartsWithF("{") AndAlso TrimedJson.EndsWithF("}")
                End Function
                If _JsonText Is Nothing Then
                    Dim JsonPath As String = PathInstance & Name & ".json"
                    If Not File.Exists(JsonPath) Then
                        '如果文件夹下只有一个 JSON 文件，则将其作为实例 JSON
                        Dim JsonFiles As String() = Directory.GetFiles(PathInstance, "*.json")
                        If JsonFiles.Count = 1 Then
                            JsonPath = JsonFiles(0)
                            Log("[Minecraft] 未找到同名实例 JSON，自动换用 " & JsonPath, LogLevel.Debug)
                        Else
                            Throw New Exception($"未找到实例 JSON 文件：{PathInstance}{Name}.json")
                        End If
                    End If
                    _JsonText = ReadFile(JsonPath)
                    '如果 ReadFile 失败会返回空字符串；这可能是由于文件被临时占用，故延时后重试
                    If Not FastJsonCheck(_JsonText) Then
                        If RunInUi() Then
                            Log("[Minecraft] 实例 JSON 文件为空或有误，由于代码在主线程运行，将不再进行重试", LogLevel.Debug)
                            GetJson(_JsonText) '触发异常
                        Else
                            Log($"[Minecraft] 实例 JSON 文件为空或有误，将在 2s 后重试读取（{JsonPath}）", LogLevel.Debug)
                            Thread.Sleep(2000)
                            _JsonText = ReadFile(JsonPath)
                            If Not FastJsonCheck(_JsonText) Then GetJson(_JsonText) '触发异常
                        End If
                    End If
                End If
                Return _JsonText
            End Get
            Set(value As String)
                _JsonText = value
            End Set
        End Property
        Private _JsonText As String = Nothing
        ''' <summary>
        ''' 该实例的 JSON 对象。
        ''' 若 JSON 存在问题，在获取该属性时即会抛出异常。
        ''' </summary>
        Public Property JsonObject As JObject
            Get
                If _JsonObject Is Nothing Then
                    Dim Text As String = JsonText '触发 JsonText 的 Get 事件
                    Try
                        _JsonObject = GetJson(Text)
                        '转换 HMCL 关键项
                        If _JsonObject.ContainsKey("patches") AndAlso Not _JsonObject.ContainsKey("time") Then
                            IsHmclFormatJson = True
                            '合并 JSON
                            'Dim HasOptiFine As Boolean = False, HasForge As Boolean = False
                            Dim CurrentObject As JObject = Nothing
                            Dim SubjsonList As New List(Of JObject)
                            For Each Subjson As JObject In _JsonObject("patches")
                                SubjsonList.Add(Subjson)
                            Next
                            SubjsonList.Sort(Function(left, right) Val(If(left("priority"), "0").ToString) < Val(If(right("priority"), "0").ToString))
                            For Each Subjson As JObject In SubjsonList
                                Dim Id As String = Subjson("id")
                                If Id IsNot Nothing Then
                                    '合并 JSON
                                    Log("[Minecraft] 合并 HMCL 分支项：" & Id)
                                    If CurrentObject IsNot Nothing Then
                                        CurrentObject.Merge(Subjson)
                                    Else
                                        CurrentObject = Subjson
                                    End If
                                Else
                                    Log("[Minecraft] 存在为空的 HMCL 分支项")
                                End If
                            Next
                            _JsonObject = CurrentObject
                            '修改附加项
                            _JsonObject("id") = Name
                            If _JsonObject.ContainsKey("inheritsFrom") Then _JsonObject.Remove("inheritsFrom")
                        End If
                        '与继承实例合并
                        Dim inheritInstanceName = Nothing
                        Try
                            inheritInstanceName = If(_JsonObject("inheritsFrom") Is Nothing, "", _JsonObject("inheritsFrom").ToString)
                            If inheritInstanceName = Name Then
                                Log("[Minecraft] 自引用的继承实例：" & Name, LogLevel.Debug)
                                inheritInstanceName = ""
                                Exit Try
                            End If
Recheck:
                            If inheritInstanceName <> "" Then
                                Dim inheritInstance As New McInstance(inheritInstanceName)
                                '继续循环
                                If inheritInstance.InheritInstanceName = inheritInstanceName Then Throw New Exception("版本依赖项出现嵌套：" & inheritInstanceName)
                                inheritInstanceName = inheritInstance.InheritInstanceName
                                '合并
                                inheritInstance.JsonObject.Merge(_JsonObject)
                                _JsonObject = inheritInstance.JsonObject
                                GoTo Recheck
                            End If
                        Catch ex As Exception
                            Log(ex, "合并实例依赖项 JSON 失败（" & If(inheritInstanceName, "null").ToString & "）")
                        End Try
                    Catch ex As Exception
                        Throw New Exception("初始化实例 JSON 时失败（" & If(Name, "null") & "）", ex)
                    End Try
                End If
                Return _JsonObject
            End Get
            Set(value As JObject)
                _JsonObject = value
            End Set
        End Property
        Private _JsonObject As JObject = Nothing
        ''' <summary>
        ''' 是否为旧版 JSON 格式。
        ''' </summary>
        Public ReadOnly Property IsOldJson As Boolean
            Get
                Return JsonObject("minecraftArguments") IsNot Nothing AndAlso JsonObject("minecraftArguments") <> ""
            End Get
        End Property
        ''' <summary>
        ''' JSON 是否为 HMCL 格式。
        ''' </summary>
        Public Property IsHmclFormatJson As Boolean = False

        ''' <summary>
        ''' 实例 JAR 中的 version.json 文件对象。
        ''' 若没有则返回 Nothing。
        ''' </summary>
        Public ReadOnly Property JsonVersion As JObject
            Get
                Static jsonVersionInited As Boolean = False
                If Not jsonVersionInited Then
                    jsonVersionInited = True
                    Try
                        If Not File.Exists(PathInstance & Name & ".jar") Then Exit Try
                        Using jarArchive As New ZipArchive(New FileStream(PathInstance & Name & ".jar", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            Dim versionJson As ZipArchiveEntry = jarArchive.GetEntry("version.json")
                            If versionJson IsNot Nothing Then
                                Using versionJsonStream As New StreamReader(versionJson.Open)
                                    _jsonVersion = GetJson(versionJsonStream.ReadToEnd)
                                End Using
                            End If
                        End Using
                    Catch ex As Exception
                        Log(ex, $"从实例 JAR 中读取 version.json 失败 ({PathInstance}{Name}.jar)")
                    End Try
                End If
                Return _jsonVersion
            End Get
        End Property
        Private _jsonVersion As JObject = Nothing

        ''' <summary>
        ''' 该实例的依赖实例。若无依赖实例则为空字符串。
        ''' </summary>
        Public ReadOnly Property InheritInstanceName As String
            Get
                If _inheritInstanceName Is Nothing Then
                    _inheritInstanceName = If(JsonObject("inheritsFrom"), "").ToString
                    '由于过老的 LiteLoader 中没有 Inherits（例如 1.5.2），需要手动判断以获取真实继承实例
                    '此外，由于这里的加载早于实例种类判断，所以需要手动判断是否为 LiteLoader
                    '如果实例提供了不同的 JAR，代表所需的 JAR 可能已被更改，则跳过 Inherit 替换
                    If JsonText.Contains("liteloader") AndAlso Info.VanillaName <> Name AndAlso Not JsonText.Contains("logging") Then
                        If If(JsonObject("jar"), Info.VanillaName).ToString = Info.VanillaName Then _inheritInstanceName = Info.VanillaName
                    End If
                    'HMCL 实例无 JSON
                    If IsHmclFormatJson Then _inheritInstanceName = ""
                End If
                Return _inheritInstanceName
            End Get
        End Property
        Private _inheritInstanceName As String = Nothing

        ''' <summary></summary>
        ''' <param name="name">实例名，或实例文件夹的完整路径（不规定是否以 \ 结尾）。</param>
        Public Sub New(name As String)
            Me.PathInstance = If(name.Contains(":"), "", McFolderSelected & "versions\") & '补全完整路径
                      name &
                      If(name.EndsWithF("\"), "", "\") '补全右划线
        End Sub

        ''' <summary>
        ''' 检查 Minecraft 版本，若检查通过 State 则为 Original 且返回 True。
        ''' </summary>
        Public Function Check() As Boolean
            
            '检查文件夹
            If Not Directory.Exists(PathInstance) Then
                State = McInstanceState.Error
                Desc = "未找到实例 " & Name
                Return False
            End If
            '检查权限
            Try
                Directory.CreateDirectory(PathInstance & "PCL\")
                CheckPermissionWithException(PathInstance & "PCL\")
            Catch ex As Exception
                State = McInstanceState.Error
                Desc = "PCL 没有对该文件夹的访问权限，请右键以管理员身份运行 PCL"
                Log(ex, "没有访问实例文件夹的权限")
                Return False
            End Try
            '确认 JSON 可用性
            Try
                Dim jsonObjCheck = JsonObject
            Catch ex As Exception
                Log(ex, "实例 JSON 可用性检查失败（" & PathInstance & "）")
                JsonText = ""
                JsonObject = Nothing
                Desc = ex.Message
                State = McInstanceState.Error
                Return False
            End Try
            '检查版本号获取
            Try
                If String.IsNullOrEmpty(Info.VanillaName) Then Throw New Exception("无法获取版本号，结果为空")
            Catch ex As Exception
                Log(ex, "版本号获取失败（" & Name & "）")
                State = McInstanceState.Error
                Desc = "版本号获取失败：" & ex.ToString()
                Return False
            End Try
            '检查依赖实例
            Try
                If Not InheritInstanceName = "" Then
                    If Not File.Exists(GetPathFromFullPath(PathInstance) & InheritInstanceName & "\" & InheritInstanceName & ".json") Then
                        State = McInstanceState.Error
                        Desc = "需要安装 " & InheritInstanceName & " 作为前置实例"
                        Return False
                    End If
                End If
            Catch ex As Exception
                Log(ex, "依赖实例检查出错（" & Name & "）")
                State = McInstanceState.Error
                Desc = "未知错误：" & ex.ToString()
                Return False
            End Try

            State = McInstanceState.Original
            Return True
        End Function
        ''' <summary>
        ''' 加载 Minecraft 实例的详细信息。不使用其缓存，且会更新缓存。
        ''' </summary>
        Public Function Load() As McInstance
            Try
                '检查实例，若出错则跳过数据确定阶段
                If Not Check() Then GoTo ExitDataLoad
#Region "确定实例分类"
                Select Case Info.VanillaName '在获取 Version.Original 对象时会完成它的加载
                    Case "Unknown"
                        State = McInstanceState.Error
                    Case "Old"
                        State = McInstanceState.Old
                    Case Else '根据 API 进行筛选
                        Dim realJson As String = If(JsonObject, JsonText).ToString
                        '愚人节与快照版本
                        If If(JsonObject("type"), "").ToString = "fool" OrElse GetMcFoolName(Info.VanillaName) <> "" Then
                            State = McInstanceState.Fool
                        ElseIf IsSnapshot() Then
                            State = McInstanceState.Snapshot
                        End If
                        'OptiFine
                        If realJson.Contains("optifine") Then
                            State = McInstanceState.OptiFine
                            Info.HasOptiFine = True
                            Info.OptiFine = If(RegexSeek(realJson, RegexPatterns.OptiFineVersion), "未知版本")
                        End If
                        'LiteLoader
                        If realJson.Contains("liteloader") Then
                            State = McInstanceState.LiteLoader
                            Info.HasLiteLoader = True
                        End If
                        'Fabric、Forge、Quilt、LabyMod、Legacy Fabric
                        If realJson.Contains("labymod_data") Then
                            State = McInstanceState.LabyMod
                            Info.HasLabyMod = True
                            Info.LabyMod = JsonObject("labymod_data")("version")
                        ElseIf realJson.Contains("net.legacyfabric:intermediary") Then
                            State = McInstanceState.LegacyFabric
                            Info.HasLegacyFabric = True
                            Info.LegacyFabric = If(RegexSeek(realJson, RegexPatterns.LegacyFabricVersion), "未知版本").Replace("+build", "")
                        ElseIf realJson.Contains("net.fabricmc:fabric-loader") Then
                            State = McInstanceState.Fabric
                            Info.HasFabric = True
                            Info.Fabric = If(RegexSeek(realJson, RegexPatterns.FabricVersion), "未知版本").Replace("+build", "")
                        ElseIf realJson.Contains("org.quiltmc:quilt-loader") Then
                            State = McInstanceState.Quilt
                            Info.HasQuilt = True
                            Info.Quilt = If(RegexSeek(realJson, RegexPatterns.QuiltVersion), "未知版本").Replace("+build", "")
                        ElseIf realJson.Contains("com.cleanroommc:cleanroom:") Then
                            State = McInstanceState.Cleanroom
                            Info.HasCleanroom = True
                            Info.Cleanroom = If(RegexSeek(realJson, RegexPatterns.CleanroomVersion), "未知版本").Replace("+build", "")
                        ElseIf realJson.Contains("minecraftforge") AndAlso Not realJson.Contains("net.neoforge") Then
                            State = McInstanceState.Forge
                            Info.HasForge = True
                            Info.Forge = RegexSeek(realJson, RegexPatterns.ForgeMainVersion)
                            If Info.Forge Is Nothing Then Info.Forge = If(RegexSeek(realJson, RegexPatterns.ForgeLibVersion), "未知版本")
                        ElseIf realJson.Contains("net.neoforge") Then
                            '1.20.1 JSON 范例："--fml.forgeVersion", "47.1.99"
                            '1.20.2+ JSON 范例："--fml.neoForgeVersion", "20.6.119-beta"
                            State = McInstanceState.NeoForge
                            Info.HasNeoForge = True
                            Info.NeoForge = If(RegexSeek(realJson, RegexPatterns.NeoForgeVersion), "未知版本")
                        End If
                End Select
#End Region
ExitDataLoad:
                '确定实例图标
                Logo = States.Instance.LogoPath(PathInstance)
                If Logo = "" OrElse Not States.Instance.IsLogoCustom(PathInstance) Then
                    Select Case State
                        Case McInstanceState.Original
                            Logo = PathImage & "Blocks/Grass.png"
                        Case McInstanceState.Snapshot
                            Logo = PathImage & "Blocks/CommandBlock.png"
                        Case McInstanceState.Old
                            Logo = PathImage & "Blocks/CobbleStone.png"
                        Case McInstanceState.Forge
                            Logo = PathImage & "Blocks/Anvil.png"
                        Case McInstanceState.NeoForge
                            Logo = PathImage & "Blocks/NeoForge.png"
                        Case McInstanceState.Cleanroom
                            Logo = PathImage & "Blocks/Cleanroom.png"
                        Case McInstanceState.Fabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.LegacyFabric
                            Logo = PathImage & "Blocks/Fabric.png"
                        Case McInstanceState.Quilt
                            Logo = PathImage & "Blocks/Quilt.png"
                        Case McInstanceState.OptiFine
                            Logo = PathImage & "Blocks/GrassPath.png"
                        Case McInstanceState.LiteLoader
                            Logo = PathImage & "Blocks/Egg.png"
                        Case McInstanceState.Fool
                            Logo = PathImage & "Blocks/GoldBlock.png"
                        Case McInstanceState.LabyMod
                            Logo = PathImage & "Blocks/LabyMod.png"
                        Case Else
                            Logo = PathImage & "Blocks/RedstoneBlock.png"
                    End Select
                End If
                '确定实例描述
                If State = McInstanceState.Error Then
                    Desc = Me.Desc
                Else
                    Desc = States.Instance.CustomInfo(PathInstance)
                    If Desc = GetDefaultDescription() Then Desc = ""
                End If
                '确定实例收藏状态
                IsStar = States.Instance.Starred(PathInstance)
                '确定实例显示种类
                DisplayType = States.Instance.CardType(PathInstance)
                '写入缓存
                If Directory.Exists(PathInstance) Then
                    States.Instance.State(PathInstance) = State
                    States.Instance.Info(PathInstance) = Desc
                    States.Instance.LogoPath(PathInstance) = Logo
                End If
                If State <> McInstanceState.Error Then
                    States.Instance.ReleaseTime(PathInstance) = ReleaseTime.ToString("yyyy'-'MM'-'dd HH':'mm")
                    States.Instance.FabricVersion(PathInstance) = Info.Fabric
                    States.Instance.LegacyFabricVersion(PathInstance) = Info.LegacyFabric
                    States.Instance.QuiltVersion(PathInstance) = Info.Quilt
                    States.Instance.LabyModVersion(PathInstance) = Info.LabyMod
                    States.Instance.OptiFineVersion(PathInstance) = Info.OptiFine
                    States.Instance.HasLiteLoader(PathInstance) = Info.HasLiteLoader
                    States.Instance.ForgeVersion(PathInstance) = Info.Forge
                    States.Instance.NeoForgeVersion(PathInstance) = Info.NeoForge
                    States.Instance.CleanroomVersion(PathInstance) = Info.Cleanroom
                    States.Instance.VanillaVersionName(PathInstance) = Info.VanillaName
                    States.Instance.VanillaVersion(PathInstance) = Info.Vanilla.ToString()
                End If
            Catch ex As Exception
                Desc = "未知错误：" & ex.ToString()
                Logo = PathImage & "Blocks/RedstoneBlock.png"
                State = McInstanceState.Error
                Log(ex, "加载实例失败（" & Name & "）", LogLevel.Feedback)
            Finally
                IsLoaded = True
            End Try
            Return Me
        End Function
        Private Function IsSnapshot() As Boolean
            Return {"w", "snapshot", "rc", "pre", "experimental", "-"}.Any(Function(s) Info.VanillaName.ContainsF(s, True)) OrElse
                   Name.ContainsF("combat", True) OrElse
                   If(JsonObject("type"), "").ToString = "snapshot" OrElse If(JsonObject("type"), "").ToString = "pending"
        End Function
        ''' <summary>
        ''' 获取实例的默认描述。
        ''' </summary>
        Public Function GetDefaultDescription() As String
            'Mod Loader 信息
            Dim ModLoaderInfo As String = ""
            If Me.Info.HasForge Then ModLoaderInfo += ", Forge" & If(Me.Info.Forge = "未知版本", "", " " & Me.Info.Forge)
            If Me.Info.HasNeoForge Then ModLoaderInfo += ", NeoForge" & If(Me.Info.NeoForge = "未知版本", "", " " & Me.Info.NeoForge)
            If Me.Info.HasCleanroom Then ModLoaderInfo += ", Cleanroom" & If(Me.Info.Cleanroom = "未知版本", "", " " & Me.Info.Cleanroom)
            If Me.Info.HasLabyMod Then ModLoaderInfo += ", LabyMod" & If(Me.Info.LabyMod = "未知版本", "", " " & Me.Info.LabyMod)
            If Me.Info.HasFabric Then ModLoaderInfo += ", Fabric" & If(Me.Info.Fabric = "未知版本", "", " " & Me.Info.Fabric)
            If Me.Info.HasQuilt Then ModLoaderInfo += ", Quilt" & If(Me.Info.Quilt = "未知版本", "", " " & Me.Info.Quilt)
            If Me.Info.HasLegacyFabric Then ModLoaderInfo += ", Legacy Fabric" & If(Me.Info.LegacyFabric = "未知版本", "", " " & Me.Info.LegacyFabric)
            If Me.Info.HasOptiFine Then ModLoaderInfo += ", OptiFine" & If(Me.Info.OptiFine = "未知版本", "", " " & Me.Info.OptiFine.Replace("-", " ").Replace("_", " "))
            If Me.Info.HasLiteLoader Then ModLoaderInfo += ", LiteLoader"
'基础信息
            Dim Info As String
            Select Case State
                Case McInstanceState.Snapshot, McInstanceState.Original, McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.Fabric, McInstanceState.OptiFine, McInstanceState.LiteLoader
                    If Me.Info.VanillaName.ContainsF("pre", True) Then
                        Info = "预发布版 " & Me.Info.VanillaName
                    ElseIf Me.Info.VanillaName.ContainsF("rc", True) Then
                        Info = "发布候选 " & Me.Info.VanillaName
                    ElseIf Me.Info.VanillaName.Contains("experimental") Then
                        Info = "实验性快照" & Me.Info.VanillaName
                    ElseIf Me.Info.VanillaName = "pending" Then
                        Info = "实验性快照"
                    ElseIf IsSnapshot Then
                        Info = If(Me.Info.Reliable, "快照版 " & Me.Info.VanillaName.Replace("-snapshot", ""), "快照版")
                    Else
                        Info = If(Me.Info.Reliable, "正式版 " & Me.Info.VanillaName, "正式版")
                    End If
                Case McInstanceState.Old
                    Info = "远古版本"
                Case McInstanceState.Fool
                    Info = "愚人节版本 " & Me.Info.VanillaName
                Case McInstanceState.Error
                    Return Me.Desc '已有错误信息
                Case Else
                    Return "发生了未知错误，请向作者反馈此问题"
            End Select
            Return (Info & ModLoaderInfo).Replace("_", "-")
        End Function

        Public IsLoaded As Boolean = False

        '运算符支持
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim instance = TryCast(obj, McInstance)
            Return instance IsNot Nothing AndAlso PathInstance = instance.PathInstance
        End Function
        Public Shared Operator =(a As McInstance, b As McInstance) As Boolean
            If a Is Nothing AndAlso b Is Nothing Then Return True
            If a Is Nothing OrElse b Is Nothing Then Return False
            Return a.PathInstance = b.PathInstance
        End Operator
        Public Shared Operator <>(a As McInstance, b As McInstance) As Boolean
            Return Not (a = b)
        End Operator

    End Class
    Public Enum McInstanceState
        [Error]
        Original
        Snapshot
        Fool
        OptiFine
        Old
        Forge
        NeoForge
        LiteLoader
        Fabric
        LegacyFabric
        Quilt
        Cleanroom
        LabyMod
    End Enum

    ''' <summary>
    ''' 某个 Minecraft 实例的版本名、附加组件信息。
    ''' </summary>
    Public Class McInstanceInfo

        '原版

        ''' <summary>
        ''' 原版版本名。
        ''' 如 26.1，26.1-snapshot-1，1.12.2，16w01a。
        ''' </summary>
        Public VanillaName As String
        ''' <summary>
        ''' 可比较的三段式原版版本号。
        ''' 对老版本格式，例如 1.20.3，会被转换为 20.0.3。
        ''' 若没有版本号，例如旧快照，则为 9999.0.0。
        ''' </summary>
        Public Vanilla As Version
        ''' <summary>
        ''' 指示原版版本号是否可靠（不是通过猜测获取）。
        ''' </summary>
        Public Reliable As Boolean = True
        ''' <summary>
        ''' 原版版本号是否有效。
        ''' </summary>
        Public ReadOnly Property Valid As Boolean
            Get
                Return Vanilla.Major < 1000
            End Get
        End Property
        ''' <summary>
        ''' 可供比较的原版 Drop 序数。
        ''' 例如 26.3.2 为 263，1.21.5 为 210。
        ''' 若没有版本号，例如旧快照，则直接指定为 209。
        ''' </summary>
        Public ReadOnly Property Drop As Integer
            Get
                Return If(Valid, Vanilla.Major * 10 + Vanilla.Minor, 209)
            End Get
        End Property

        'OptiFine

        ''' <summary>
        ''' 该实例是否通过 JSON 安装了 OptiFine。
        ''' </summary>
        Public HasOptiFine As Boolean = False
        ''' <summary>
        ''' OptiFine 版本号，如 C8、C9_pre10。
        ''' </summary>
        Public OptiFine As String = ""
        ''' <summary>
        ''' 可供比较的 OptiFine 版本序数。
        ''' </summary>
        Public ReadOnly Property OptiFineCode As Integer
            Get
                If String.IsNullOrEmpty(OptiFine) OrElse OptiFine = "未知版本" Then Return 0
                '字母编号，如 G2 中的 G（7）
                Dim result As Integer = Asc(OptiFine.ToUpper.First) - Asc("A"c) + 1
                '末尾数字，如 C5 beta4 中的 5
                result *= 100
                result += Val(RegexSeek(Right(OptiFine, OptiFine.Length - 1), "[0-9]+"))
                '测试标记（正式版为 99，Pre[x] 为 50+x，Beta[x] 为 x）
                result *= 100
                If OptiFine.ContainsF("pre", True) Then result += 50
                If OptiFine.ContainsF("pre", True) OrElse OptiFine.ContainsF("beta", True) Then
                    If Val(Right(OptiFine, 1)) = 0 AndAlso Right(OptiFine, 1) <> "0" Then
                        result += 1 '为 pre 或 beta 结尾，视作 1
                    Else
                        result += Val(RegexSeek(OptiFine.ToLower, "(?<=((pre)|(beta)))[0-9]+"))
                    End If
                Else
                    result += 99
                End If
                Return result
            End Get
        End Property

        'Forgelike
        
        ''' <summary>
        ''' 该版本是否安装了 Forgelike 加载器。
        ''' </summary>
        Public ReadOnly Property HasForgelike As Boolean
            Get
                Return HasForge OrElse HasNeoForge OrElse HasCleanroom
            End Get
        End Property
        
        ''' <summary>
        ''' 可供比较的类 Forge 版本序数。
        ''' </summary>
        Public ReadOnly Property ForgelikeCode As Integer
            Get
                If Not HasForgelike Then Return 0
                If (String.IsNullOrEmpty(Forge) OrElse Forge = "未知版本") AndAlso
                   (String.IsNullOrEmpty(NeoForge) OrElse NeoForge = "未知版本") Then Return 0
                Dim segments = RegexSearch(If(HasForge, Forge, NeoForge), "\d+")
                Select Case segments.Count
                    Case Is > 4
                        Return Val(segments(0)) * 1000000 + Val(segments(1)) * 10000 + Val(segments(3))
                    Case 3
                        Return Val(segments(0)) * 1000000 + Val(segments(1)) * 10000 + Val(segments(2))
                    Case 2
                        Return Val(segments(0)) * 1000000 + Val(segments(1)) * 10000
                    Case Else
                        Return Val(segments(0)) * 1000000
                End Select
            End Get
        End Property
        
        'Forge

        ''' <summary>
        ''' 该实例是否安装了 Forge。
        ''' </summary>
        Public HasForge As Boolean = False
        ''' <summary>
        ''' Forge 版本号，如 31.1.2、14.23.5.2847。
        ''' </summary>
        Public Forge As String = ""

        'NeoForge

        ''' <summary>
        ''' 该实例是否安装了 NeoForge。
        ''' </summary>
        Public HasNeoForge As Boolean = False
        ''' <summary>
        ''' NeoForge 版本号，如 21.0.2-beta、47.1.79。
        ''' </summary>
        Public NeoForge As String = ""

        'Cleanroom

        ''' <summary>
        ''' 该实例是否安装了 Cleanroom。
        ''' </summary>
        Public HasCleanroom As Boolean = False
        ''' <summary>
        ''' Cleanroom 版本号，如 0.2.4-alpha。
        ''' </summary>
        Public Cleanroom As String = ""

        'Fabriclike
        
        ''' <summary>
        ''' 该版本是否安装了 Fabriclike 加载器。
        ''' </summary>
        Public ReadOnly Property HasFabriclike As Boolean
            Get
                Return HasFabric OrElse HasQuilt OrElse HasLegacyFabric
            End Get
        End Property
        
        'Fabric

        ''' <summary>
        ''' 该实例是否安装了 Fabric。
        ''' </summary>
        Public HasFabric As Boolean = False
        ''' <summary>
        ''' Fabric 版本号，如 0.7.2.175。
        ''' </summary>
        Public Fabric As String = ""

        'LegacyFabric

        ''' <summary>
        ''' 该实例是否安装了 Fabric。
        ''' </summary>
        Public HasLegacyFabric As Boolean = False
        ''' <summary>
        ''' Fabric 版本号，如 0.7.2.175。
        ''' </summary>
        Public LegacyFabric As String = ""


        'Quilt

        ''' <summary>
        ''' 该实例是否安装了 Quilt。
        ''' </summary>
        Public HasQuilt As Boolean = False
        ''' <summary>
        ''' Quilt 版本号，如 0.26.1-beta.1、0.26.0。
        ''' </summary>
        Public Quilt As String = ""

        'LabyMod

        ''' <summary>
        ''' 该实例是否安装了 LabyMod。
        ''' </summary>
        Public HasLabyMod As Boolean = False
        ''' <summary>
        ''' LabyMod 版本号，如 4.2.59。
        ''' </summary>
        Public LabyMod As String = ""

        'LiteLoader

        ''' <summary>
        ''' 该实例是否安装了 LiteLoader。
        ''' </summary>
        Public HasLiteLoader As Boolean = False

        'API

        ''' <summary>
        ''' 生成对此实例信息的用户友好的描述性字符串。
        ''' </summary>
        Public Overrides Function ToString() As String
            ToString = ""
            If HasForge Then ToString += ", Forge" & If(Forge = "未知版本", "", " " & Forge)
            If HasNeoForge Then ToString += ", NeoForge" & If(NeoForge = "未知版本", "", " " & NeoForge)
            If HasCleanroom Then ToString += ", Cleanroom" & If(Cleanroom = "未知版本", "", " " & Cleanroom)
            If HasFabric Then ToString += ", Fabric" & If(Fabric = "未知版本", "", " " & Fabric)
            If HasLegacyFabric Then ToString += ", LegacyFabric" & If(LegacyFabric = "未知版本", "", " " & LegacyFabric)
            If HasQuilt Then ToString += ", Quilt" & If(Quilt = "未知版本", "", " " & Quilt)
            If HasLabyMod Then ToString += ", LabyMod" & If(LabyMod = "未知版本", "", " " & LabyMod)
            If HasOptiFine Then ToString += ", OptiFine" & If(OptiFine = "未知版本", "", " " & OptiFine)
            If HasLiteLoader Then ToString += ", LiteLoader"
            If ToString = "" Then
                Return "原版 " & VanillaName
            Else
                Return VanillaName & ToString
            End If
        End Function

        'Helpers

        ''' <summary>
        ''' 版本字符串是否符合 Minecraft 原版格式，例如 1.x、26.x。
        ''' </summary>
        Public Shared Function IsFormatFit(version As String) As Boolean
            If version Is Nothing Then Return False
            If RegexCheck(version, "^1\.\d") Then Return True
            If Val(RegexSeek(version, "^[2-9]\d\.\d+")) > 25 Then Return True
            Return False
        End Function
        ''' <summary>
        ''' 尝试将版本字符串转换为 Drop 序数。
        ''' 若无法转换则返回 0。
        ''' </summary>
        Public Shared Function VersionToDrop(version As String, Optional allowSnapshot As Boolean = False) As Integer
            If Not allowSnapshot AndAlso version.Contains("-") Then Return 0
            If version Is Nothing Then Return 0
            Dim segments = version.BeforeFirst("-").Split(".")
            If segments.Length < 2 Then Return 0
            Dim major As Integer = Val(segments(0))
            Dim minor As Integer = Val(segments(1))
            If major = 1 Then
                Return minor * 10
            ElseIf major < 25 Then
                Return 0
            Else
                Return major * 10 + minor
            End If
        End Function
        ''' <summary>
        ''' 将 Drop 序数转换为版本字符串。
        ''' </summary>
        Public Shared Function DropToVersion(drop As Integer) As String
            If drop >= 250 Then
                Return $"{drop \ 10}.{drop Mod 10}"
            Else
                Return $"1.{drop \ 10}"
            End If
        End Function
        
    End Class
    
    ''' <summary>
    ''' 根据版本名获取对应的愚人节版本描述。非愚人节版本会返回空字符串。
    ''' </summary>
    Public Function GetMcFoolName(name As String) As String
        name = name.ToLower
        If name.StartsWithF("2.0") OrElse name.StartsWithF("2point0") Then
            Dim tag = ""
            If name.EndsWith("red") Then
                tag = "（红色版本）"
            ElseIf name.EndsWith("blue") Then
                tag = "（蓝色版本）"
            ElseIf name.EndsWith("purple") Then
                tag = "（紫色版本）"
            End If
            Return "2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！" & tag
        ElseIf name = "15w14a" Then
            Return "2015 | 作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。"
        ElseIf name = "1.rv-pre1" Then
            Return "2016 | 是时候将现代科技带入 Minecraft 了！"
        ElseIf name = "3d shareware v1.34" Then
            Return "2019 | 我们从地下室的废墟里找到了这个开发于 1994 年的杰作！"
        ElseIf name.StartsWithF("20w14inf") OrElse name = "20w14∞" Then
            Return "2020 | 我们加入了 20 亿个新的维度，让无限的想象变成了现实！"
        ElseIf name = "22w13oneblockatatime" Then
            Return "2022 | 一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！"
        ElseIf name = "23w13a_or_b" Then
            Return "2023 | 研究表明：玩家喜欢作出选择——越多越好！"
        ElseIf name = "24w14potato" Then
            Return "2024 | 毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！"
        ElseIf name = "25w14craftmine" Then
            Return "2025 | 你可以合成任何东西——包括合成你的世界！"
        Else
            Return ""
        End If
    End Function

    ''' <summary>
    ''' 当前按卡片分类的所有版本列表。
    ''' </summary>
    Public McInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#End Region

#Region "实例列表加载"

    ''' <summary>
    ''' 是否要求本次加载强制刷新实例列表。
    ''' </summary>
    Public McInstanceListForceRefresh As Boolean = False

    ''' <summary>
    ''' 是否为本次打开 PCL 后第一次加载实例列表。
    ''' 这会清理所有 .pclignore 文件，而非跳过这些对应实例。
    ''' </summary>
    Private _isFirstMcInstanceListLoad As Boolean = True

    ''' <summary>
    ''' 加载 Minecraft 文件夹的实例列表。
    ''' </summary>
    Public McInstanceListLoader As New LoaderTask(Of String, Integer)("Minecraft Instance List", AddressOf InitMcInstanceList) With {.ReloadTimeout = 1}
    Private Sub InitMcInstanceList(loader As LoaderTask(Of String, Integer))
        '开始加载
        Dim path As String = loader.Input
        Try
            '初始化
            McInstanceList = New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '检测缓存是否需要更新
            Dim folderList As New List(Of String)
            If Directory.Exists(path & "versions") Then '不要使用 CheckPermission，会导致写入时间改变，从而使得文件夹被强制刷新
                Try
                    For Each folder As DirectoryInfo In New DirectoryInfo(path & "versions").GetDirectories
                        folderList.Add(folder.Name)
                    Next
                Catch ex As Exception
                    Throw New Exception("无法读取实例文件夹，可能是由于没有权限（" & path & "versions）", ex)
                End Try
            End If
            '不可用
            If Not folderList.Any() Then
                WriteIni(path & "PCL.ini", "InstanceCache", "") '清空缓存
                GoTo OnLoaded
            End If
            '有可用实例
            Dim folderListCheck As Integer = GetHash(McInstanceCacheVersion & "#" & Join(folderList.ToArray, "#")) Mod (Integer.MaxValue - 1) '根据文件夹名列表生成辨识码
            If Not McInstanceListForceRefresh AndAlso Val(ReadIni(path & "PCL.ini", "InstanceCache")) = folderListCheck Then
                '可以使用缓存
                Dim result = InitMcInstanceListWithCache(path)
                If result Is Nothing Then
                    GoTo Reload
                Else
                    McInstanceList = result
                End If
            Else
                '文件夹列表不符
Reload:
                McInstanceListForceRefresh = False
                Log("[Minecraft] 文件夹列表变更，重载所有实例")
                WriteIni(path & "PCL.ini", "InstanceCache", folderListCheck)
                McInstanceList = InitMcInstanceListWithoutCache(path)
            End If
            _isFirstMcInstanceListLoad = False

            '改变当前选择的实例
OnLoaded:
            If loader.IsAborted Then Return
            If McInstanceList.Any(Function(v) v.Key <> McInstanceCardType.Error) Then
                '尝试读取已储存的选择
                Dim savedSelection As String = ReadIni(path & "PCL.ini", "Version")
                If savedSelection <> "" Then
                    For Each card As KeyValuePair(Of McInstanceCardType, List(Of McInstance)) In McInstanceList
                        For Each instance As McInstance In card.Value
                            If instance.Name = savedSelection AndAlso Not instance.State = McInstanceState.Error Then
                                '使用已储存的选择
                                McInstanceSelected = instance
                                Setup.Set("LaunchInstanceSelect", McInstanceSelected.Name)
                                Log("[Minecraft] 选择该文件夹储存的 Minecraft 实例：" & McInstanceSelected.PathInstance)
                                Return
                            End If
                        Next
                    Next
                End If
                If Not McInstanceList.First.Value(0).State = McInstanceState.Error Then
                    '自动选择第一项
                    McInstanceSelected = McInstanceList.First.Value(0)
                    Setup.Set("LaunchInstanceSelect", McInstanceSelected.Name)
                    Log("[Launch] 自动选择 Minecraft 实例：" & McInstanceSelected.PathInstance)
                End If
            Else
                McInstanceSelected = Nothing
                Setup.Set("LaunchInstanceSelect", "")
                Log("[Minecraft] 未找到可用 Minecraft 实例")
            End If
            If Setup.Get("SystemDebugDelay") Then Thread.Sleep(RandomUtils.NextInt(200, 3000))
        Catch ex As ThreadInterruptedException
        Catch ex As Exception
            WriteIni(path & "PCL.ini", "InstanceCache", "") '要求下次重新加载
            Log(ex, "加载 .minecraft 实例列表失败", LogLevel.Feedback)
        End Try
    End Sub

    '获取实例列表
    Private Function InitMcInstanceListWithCache(path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim results As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        Try
            Dim cardCount As Integer = ReadIni(path & "PCL.ini", "CardCount", -1)
            If cardCount = -1 Then Return Nothing
            For i = 0 To cardCount - 1
                Dim cardType As McInstanceCardType = ReadIni(path & "PCL.ini", "CardKey" & (i + 1), ":")
                Dim instanceList As New List(Of McInstance)

                '循环读取实例
                For Each folder As String In ReadIni(path & "PCL.ini", "CardValue" & (i + 1), ":").Split(":")
                    If folder = "" Then Continue For
                    Dim versionFolder As String = $"{path}versions\{folder}\"
                    If File.Exists(versionFolder & ".pclignore") Then
                        If _isFirstMcInstanceListLoad Then
                            Log("[Minecraft] 清理残留的忽略项目：" & versionFolder) '#2781
                            File.Delete(versionFolder & ".pclignore")
                        Else
                            Log("[Minecraft] 跳过要求忽略的项目：" & versionFolder)
                            Continue For
                        End If
                    End If
                    Try

                        '读取单个实例
                        Dim instance As New McInstance(versionFolder)
                        instanceList.Add(instance)
                        Dim instanceCfg = States.Instance
                        instance.Desc = instanceCfg.CustomInfo(instance.PathInstance)
                        If instance.Desc = "" Then instance.Desc = instanceCfg.Info(instance.PathInstance)
                        If Not instanceCfg.LogoPathConfig.IsDefault(instance.PathInstance) Then _
                            instance.Logo = instanceCfg.LogoPath(instance.PathInstance)
                        If Not instanceCfg.ReleaseTimeConfig.IsDefault(instance.PathInstance) Then _
                            instance.ReleaseTime = instanceCfg.ReleaseTime(instance.PathInstance)
                        If Not instanceCfg.StateConfig.IsDefault(instance.PathInstance) Then _
                            instance.State = instanceCfg.State(instance.PathInstance)
                        instance.IsStar = instanceCfg.Starred(instance.PathInstance)
                        instance.DisplayType = instanceCfg.CardType(instance.PathInstance)
                        If instance.State <> McInstanceState.Error AndAlso
                           Not instanceCfg.VanillaVersionNameConfig.IsDefault(instance.PathInstance) AndAlso '旧版本可能没有这一项，导致 Instance 不加载（#643）
                           Not instanceCfg.VanillaVersionConfig.IsDefault(instance.PathInstance) Then
                            Dim instanceInfo As New McInstanceInfo With {
                                    .Fabric = instanceCfg.FabricVersion(instance.PathInstance),
                                    .LegacyFabric = instanceCfg.LegacyFabricVersion(instance.PathInstance),
                                    .Quilt = instanceCfg.QuiltVersion(instance.PathInstance),
                                    .Forge = instanceCfg.ForgeVersion(instance.PathInstance),
                                    .LabyMod = instanceCfg.LabyModVersion(instance.PathInstance),
                                    .NeoForge = instanceCfg.NeoForgeVersion(instance.PathInstance),
                                    .Cleanroom = instanceCfg.CleanroomVersion(instance.PathInstance),
                                    .OptiFine = instanceCfg.OptiFineVersion(instance.PathInstance),
                                    .HasLiteLoader = instanceCfg.HasLiteLoader(instance.PathInstance),
                                    .VanillaName = instanceCfg.VanillaVersionName(instance.PathInstance),
                                    .Vanilla = New Version(instanceCfg.VanillaVersion(instance.PathInstance))
                            }
                            instanceInfo.HasFabric = instanceInfo.Fabric.Any()
                            instanceInfo.HasLegacyFabric = instanceInfo.LegacyFabric.Any()
                            instanceInfo.HasQuilt = instanceInfo.Quilt.Any()
                            instanceInfo.HasForge = instanceInfo.Forge.Any()
                            instanceInfo.HasNeoForge = instanceInfo.NeoForge.Any()
                            instanceInfo.HasCleanroom = instanceInfo.Cleanroom.Any()
                            instanceInfo.HasOptiFine = instanceInfo.OptiFine.Any()
                            instance.Info = instanceInfo
                        End If

                        '重新检查错误实例
                        If instance.State = McInstanceState.Error Then
                            '重新获取实例错误信息
                            Dim OldDesc As String = instance.Desc
                            instance.State = McInstanceState.Original
                            instance.Check()
                            '校验错误原因是否改变
                            Dim CustomInfo As String = instanceCfg.CustomInfo(instance.PathInstance)
                            If instance.State = McInstanceState.Original OrElse (CustomInfo = "" AndAlso Not OldDesc = instance.Desc) Then
                                Log("[Minecraft] 实例 " & instance.Name & " 的错误状态已变更，新的状态为：" & instance.Desc)
                                Return Nothing
                            End If
                        End If

                        '校验未加载的实例
                        If instance.Logo = "" Then
                            Log("[Minecraft] 实例 " & instance.Name & " 未被加载")
                            Return Nothing
                        End If

                    Catch ex As Exception
                        Log(ex, "读取实例加载缓存失败（" & folder & "）", LogLevel.Debug)
                        Return Nothing
                    End Try
                Next

                If instanceList.Any Then results.Add(cardType, instanceList)
            Next
            Return results
        Catch ex As Exception
            Log(ex, "读取实例缓存失败")
            Return Nothing
        End Try
    End Function
    Private Function InitMcInstanceListWithoutCache(path As String) As Dictionary(Of McInstanceCardType, List(Of McInstance))
        Dim instanceList As New List(Of McInstance)

#Region "循环加载每个实例的信息"
        For Each folder As DirectoryInfo In New DirectoryInfo(path & "versions").GetDirectories
            If Not folder.Exists OrElse Not folder.EnumerateFiles.Any Then
                Log("[Minecraft] 跳过空文件夹：" & folder.FullName)
                Continue For
            End If
            If (folder.Name = "cache" OrElse folder.Name = "BLClient" OrElse folder.Name = "PCL") AndAlso Not File.Exists(folder.FullName & "\" & folder.Name & ".json") Then
                Log("[Minecraft] 跳过可能不是实例文件夹的项目：" & folder.FullName)
                Continue For
            End If
            Dim instanceFolder As String = folder.FullName & "\"
            If File.Exists(instanceFolder & ".pclignore") Then
                If _isFirstMcInstanceListLoad Then
                    Log("[Minecraft] 清理残留的忽略项目：" & instanceFolder) '#2781
                    Try
                        File.Delete(instanceFolder & ".pclignore")
                    Catch ex As Exception
                        Log(ex, "清理残留的忽略项目失败（" & instanceFolder & "）", LogLevel.Hint)
                    End Try
                Else
                    Log("[Minecraft] 跳过要求忽略的项目：" & instanceFolder)
                    Continue For
                End If
            End If
            Dim instance As New McInstance(instanceFolder)
            instanceList.Add(instance)
            instance.Load()
        Next
#End Region

        Dim results As New Dictionary(Of McInstanceCardType, List(Of McInstance))

#Region "将实例分类到各个卡片"
        Try

            '未经过自定义的实例列表
            Dim instanceListOriginal As New Dictionary(Of McInstanceCardType, List(Of McInstance))

            '单独列出收藏的实例
            Dim staredInstances As New List(Of McInstance)
            For Each instance As McInstance In instanceList.ToList
                If Not instance.IsStar Then Continue For
                If instance.DisplayType = McInstanceCardType.Hidden Then Continue For
                staredInstances.Add(instance)
                instanceList.Remove(instance)
            Next
            If staredInstances.Any Then instanceListOriginal.Add(McInstanceCardType.Star, staredInstances)

            '预先筛选出愚人节和错误的实例
            McInstanceFilter(instanceList, instanceListOriginal, {McInstanceState.Error}, McInstanceCardType.Error)
            McInstanceFilter(instanceList, instanceListOriginal, {McInstanceState.Fool}, McInstanceCardType.Fool)

            '筛选 API 实例
            McInstanceFilter(instanceList, instanceListOriginal, {McInstanceState.Forge, McInstanceState.NeoForge, McInstanceState.LiteLoader, McInstanceState.Fabric, McInstanceState.LegacyFabric, McInstanceState.Quilt, McInstanceState.Cleanroom, McInstanceState.LabyMod}, McInstanceCardType.API)

            '将老实例预先分类入不常用，只剩余原版、快照、OptiFine
            Dim instanceUseful As New List(Of McInstance)
            Dim instanceRubbish As New List(Of McInstance)
            McInstanceFilter(instanceList, {McInstanceState.Old}, instanceRubbish)

            '确认最新实例，若为快照则加入常用列表
            Dim latestInstance As McInstance = instanceList.
                    Where(Function(v) v.State = McInstanceState.Original OrElse v.State = McInstanceState.Snapshot).
                    MaxOrDefault(Function(v) v.ReleaseTime)
            If latestInstance IsNot Nothing AndAlso latestInstance.State = McInstanceState.Snapshot Then
                instanceUseful.Add(latestInstance)
                instanceList.Remove(latestInstance)
            End If

            '将剩余的快照全部拖进不常用列表
            McInstanceFilter(instanceList, {McInstanceState.Snapshot}, instanceRubbish)

            '获取每个 Drop 下最新的原版与 OptiFine
            Dim newerInstance As New Dictionary(Of String, McInstance)
            Dim existDrops As New List(Of Integer)
            For Each instance As McInstance In InstanceList
                If Not instance.Info.Valid Then Continue For
                If Not existDrops.Contains(instance.Info.Drop) Then existDrops.Add(instance.Info.Drop)
                Dim key As String = instance.Info.Drop & "-" & instance.State
                If Not newerInstance.ContainsKey(key) Then
                    newerInstance.Add(key, instance)
                    Continue For
                End If
                If instance.Info.HasOptiFine Then
                    If instance.Info.OptiFineCode > newerInstance(key).Info.OptiFineCode Then newerInstance(key) = instance 'OptiFine 根据版本号判断
                Else
                    If instance.ReleaseTime > newerInstance(key).ReleaseTime Then newerInstance(key) = instance '原版根据发布时间判断
                End If
            Next

            '将每个 Drop 下的最常规版本加入
            For Each drop As Integer In existDrops
                If newerInstance.ContainsKey(drop & "-" & McInstanceState.OptiFine) AndAlso newerInstance.ContainsKey(drop & "-" & McInstanceState.Original) Then
                    '同时存在 OptiFine 与原版
                    Dim vanillaInstance As McInstance = newerInstance(drop & "-" & McInstanceState.Original)
                    Dim optiFineInstance As McInstance = newerInstance(drop & "-" & McInstanceState.OptiFine)
                    If vanillaInstance.Info.Drop > optiFineInstance.Info.Drop Then
                        '仅在原版比 OptiFine 更新时才加入原版
                        instanceUseful.Add(vanillaInstance)
                        instanceList.Remove(vanillaInstance)
                    End If
                    instanceUseful.Add(optiFineInstance)
                    instanceList.Remove(optiFineInstance)
                ElseIf newerInstance.ContainsKey(drop & "-" & McInstanceState.OptiFine) Then
                    '没有原版，直接加入 OptiFine
                    instanceUseful.Add(NewerInstance(drop & "-" & McInstanceState.OptiFine))
                    instanceList.Remove(NewerInstance(drop & "-" & McInstanceState.OptiFine))
                ElseIf newerInstance.ContainsKey(drop & "-" & McInstanceState.Original) Then
                    '没有 OptiFine，直接加入原版
                    instanceUseful.Add(NewerInstance(drop & "-" & McInstanceState.Original))
                    instanceList.Remove(NewerInstance(drop & "-" & McInstanceState.Original))
                End If
            Next

            '将剩余的东西添加进去
            instanceRubbish.AddRange(instanceList)
            If instanceUseful.Any Then instanceListOriginal.Add(McInstanceCardType.OriginalLike, instanceUseful)
            If instanceRubbish.Any Then instanceListOriginal.Add(McInstanceCardType.Rubbish, instanceRubbish)

            '按照自定义实例分类重新添加
            For Each instancePair In instanceListOriginal
                For Each instance As McInstance In instancePair.Value
                    Dim realType = If(instance.DisplayType = 0 OrElse instancePair.Key = McInstanceCardType.Star, instancePair.Key, instance.DisplayType)
                    If Not results.ContainsKey(realType) Then results.Add(realType, New List(Of McInstance))
                    results(realType).Add(instance)
                Next
            Next

        Catch ex As Exception
            results.Clear()
            Log(ex, "分类实例列表失败", LogLevel.Feedback)
        End Try
#End Region

#Region "对卡片与实例进行排序"

        '卡片排序
        Dim sortedInstanceList As New Dictionary(Of McInstanceCardType, List(Of McInstance))
        For Each sortRule As String In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool, McInstanceCardType.Error, McInstanceCardType.Hidden}
            If results.ContainsKey(sortRule) Then sortedInstanceList.Add(sortRule, results(sortRule))
        Next
        results = sortedInstanceList

        '版本排序
        For Each cardType In {McInstanceCardType.Star, McInstanceCardType.API, McInstanceCardType.OriginalLike, McInstanceCardType.Rubbish, McInstanceCardType.Fool}
            If Not results.ContainsKey(CardType) Then Continue For
            Dim getComponentCode =
                    Function(instance As McInstance) As Integer
                        If instance.Info.ForgelikeCode > 0 Then Return instance.Info.ForgelikeCode
                        If instance.Info.HasOptiFine Then Return instance.Info.OptiFineCode
                        Return 0
                    End Function
            results(CardType) = SortUtils.Sort(results(cardType), Function(left As McInstance, right As McInstance)
                '发布时间
                If (left.ReleaseTime.Year >= 2000 OrElse right.ReleaseTime.Year >= 2000) AndAlso
                   left.ReleaseTime <> right.ReleaseTime Then Return left.ReleaseTime > right.ReleaseTime
                '附加组件种类
                If left.Info.HasFabric <> right.Info.HasFabric Then Return left.Info.HasFabric
                If left.Info.HasQuilt <> right.Info.HasQuilt Then Return left.Info.HasQuilt
                If left.Info.HasLegacyFabric <> right.Info.HasLegacyFabric Then Return left.Info.HasLegacyFabric
                If left.Info.HasNeoForge <> right.Info.HasNeoForge Then Return left.Info.HasNeoForge
                If left.Info.HasForge <> right.Info.HasForge Then Return left.Info.HasForge
                If left.Info.HasCleanroom <> right.Info.HasCleanroom Then Return left.Info.HasCleanroom
                If left.Info.HasLabyMod <> right.Info.HasLabyMod Then Return left.Info.HasLabyMod
                If left.Info.HasOptiFine <> right.Info.HasOptiFine Then Return left.Info.HasOptiFine
                If left.Info.HasLiteLoader <> right.Info.HasLiteLoader Then Return left.Info.HasLiteLoader
                '附加组件版本
                If getComponentCode(left) <> getComponentCode(right) Then Return getComponentCode(left) > getComponentCode(right)
                '名称
                Return left.Name > right.Name
            End Function)
        Next

#End Region

#Region "保存卡片缓存"
        WriteIni(path & "PCL.ini", "CardCount", results.Count)
        For i = 0 To results.Count - 1
            WriteIni(path & "PCL.ini", "CardKey" & (i + 1), results.Keys(i))
            Dim Value As String = ""
            For Each Instance As McInstance In results.Values(i)
                Value += Instance.Name & ":"
            Next
            WriteIni(path & "PCL.ini", "CardValue" & (i + 1), Value)
        Next
#End Region

        Return results
    End Function
    ''' <summary>
    ''' 筛选特定种类的实例，并直接添加为卡片。
    ''' </summary>
    ''' <param name="instanceList">用于筛选的列表。</param>
    ''' <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    ''' <param name="cardType">卡片的名称。</param>
    Private Sub McInstanceFilter(ByRef instanceList As List(Of McInstance), ByRef target As Dictionary(Of McInstanceCardType, List(Of McInstance)), formula As McInstanceState(), cardType As McInstanceCardType)
        Dim keepList = instanceList.Where(Function(v) formula.Contains(v.State)).ToList
        '加入实例列表，并从剩余中删除
        If keepList.Any Then
            target.Add(cardType, keepList)
            instanceList = instanceList.Except(keepList).ToList()
        End If
    End Sub
    ''' <summary>
    ''' 筛选特定种类的实例，并增加入一个已有列表中。
    ''' </summary>
    ''' <param name="instanceList">用于筛选的列表。</param>
    ''' <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    ''' <param name="keepList">传入需要增加入的列表。</param>
    Private Sub McInstanceFilter(ByRef instanceList As List(Of McInstance), formula As McInstanceState(), ByRef keepList As List(Of McInstance))
        keepList.AddRange(instanceList.Where(Function(v) formula.Contains(v.State)))
        '加入实例列表，并从剩余中删除
        If keepList.Any Then
            instanceList = instanceList.Except(keepList).ToList()
        End If
    End Sub
    Public Enum McInstanceCardType
        Star = -1
        Auto = 0 '仅用于强制实例分类的自动
        Hidden = 1
        API = 2
        OriginalLike = 3
        Rubbish = 4
        Fool = 5
        [Error] = 6
    End Enum

#End Region

#Region "皮肤"

    Public Structure McSkinInfo
        Public IsSlim As Boolean
        Public LocalFile As String
        Public IsVaild As Boolean
    End Structure
    ''' <summary>
    ''' 要求玩家选择一个皮肤文件，并进行相关校验。
    ''' </summary>
    Public Function McSkinSelect() As McSkinInfo
        Dim FileName As String = SystemDialogs.SelectFile("皮肤文件(*.png;*.jpg;*.webp)|*.png;*.jpg;*.webp", "选择皮肤文件")

        '验证有效性
        If FileName = "" Then Return New McSkinInfo With {.IsVaild = False}
        Try
            Dim Image As New MyBitmap(FileName)
            If Image.Pic.Width <> 64 OrElse Not (Image.Pic.Height = 32 OrElse Image.Pic.Height = 64) Then
                Hint("皮肤图片大小应为 64x32 像素或 64x64 像素！", HintType.Critical)
                Return New McSkinInfo With {.IsVaild = False}
            End If
            Dim FileInfo As New FileInfo(FileName)
            If FileInfo.Length > 24 * 1024 Then
                Hint("皮肤文件大小需小于 24 KB，而所选文件大小为 " & Math.Round(FileInfo.Length / 1024, 2) & " KB", HintType.Critical)
                Return New McSkinInfo With {.IsVaild = False}
            End If
        Catch ex As Exception
            Log(ex, "皮肤文件存在错误", LogLevel.Hint)
            Return New McSkinInfo With {.IsVaild = False}
        End Try

        '获取皮肤种类
        Dim IsSlim As Integer = MyMsgBox("此皮肤为 Steve 模型（粗手臂）还是 Alex 模型（细手臂）？", "选择皮肤种类", "Steve 模型", "Alex 模型", "我不知道", HighLight:=False)
        If IsSlim = 3 Then
            Hint("请在皮肤下载页面确认皮肤种类后再使用此皮肤！")
            Return New McSkinInfo With {.IsVaild = False}
        End If

        Return New McSkinInfo With {.IsVaild = True, .IsSlim = IsSlim = 2, .LocalFile = FileName}
    End Function

    ''' <summary>
    ''' 获取 Uuid 对应的皮肤文件地址，失败将抛出异常。
    ''' </summary>
    Public Function McSkinGetAddress(Uuid As String, Type As String) As String
        If Uuid = "" Then Throw New Exception("Uuid 为空。")
        If Uuid.StartsWithF("00000") Then Throw New Exception("离线 Uuid 无正版皮肤文件。")
        '尝试读取缓存
        Dim CacheSkinAddress As String = ReadIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", Uuid)
        If Not CacheSkinAddress = "" Then Return CacheSkinAddress
        '获取皮肤地址
        Dim Url As String
        Select Case Type
            Case "Mojang", "Ms"
                Url = "https://sessionserver.mojang.com/session/minecraft/profile/"
            Case "Auth"
                Dim AuthUrl = SelectedProfile.Server
                Url = AuthUrl.Replace("/authserver", "") & "/sessionserver/session/minecraft/profile/"
            Case Else
                Throw New ArgumentException("皮肤地址种类无效：" & If(Type, "null"))
        End Select
        Dim SkinString = NetGetCodeByRequestRetry(Url & Uuid)
        If SkinString = "" Then Throw New Exception("皮肤返回值为空，可能是未设置自定义皮肤的用户")
        '处理皮肤地址
        Dim SkinValue As String
        Try
            For Each SkinProperty In GetJson(SkinString)("properties")
                If SkinProperty("name") = "textures" Then
                    SkinValue = SkinProperty("value").ToString()
                    Exit Try
                End If
            Next
            Throw New Exception("未从皮肤返回值中找到符合条件的 Property")
        Catch ex As Exception
            Log(ex, "无法完成解析的皮肤返回值，可能是未设置自定义皮肤的用户：" & SkinString, LogLevel.Developer)
            Throw New Exception("皮肤返回值中不包含皮肤数据项，可能是未设置自定义皮肤的用户", ex)
        End Try
        SkinString = Encoding.GetEncoding("utf-8").GetString(Convert.FromBase64String(SkinValue))
        Dim SkinJson As JObject = GetJson(SkinString.ToLower)
        If SkinJson("textures") Is Nothing OrElse SkinJson("textures")("skin") Is Nothing OrElse SkinJson("textures")("skin")("url") Is Nothing Then
            Throw New Exception("用户未设置自定义皮肤")
        Else
            Dim SkinUrl As String = SkinJson("textures")("skin")("url").ToString
            SkinValue = If(SkinUrl.Contains("minecraft.net/"), SkinUrl.Replace("http://", "https://"), SkinUrl)
        End If
        '保存缓存
        WriteIni(PathTemp & "Cache\Skin\Index" & Type & ".ini", Uuid, SkinValue)
        Log("[Skin] UUID " & Uuid & " 对应的皮肤文件为 " & SkinValue)
        Return SkinValue
    End Function

    Private ReadOnly McSkinDownloadLock As New Object
    ''' <summary>
    ''' 从 Url 下载皮肤。返回本地文件路径，失败将抛出异常。
    ''' </summary>
    Public Function McSkinDownload(Address As String) As String
        Dim SkinName As String = GetFileNameFromPath(Address)
        Dim FileAddress As String = PathTemp & "Cache\Skin\" & GetHash(Address) & ".png"
        SyncLock McSkinDownloadLock
            If Not File.Exists(FileAddress) Then
                NetDownloadByClient(Address, FileAddress & NetDownloadEnd).GetAwaiter().GetResult()
                File.Delete(FileAddress)
                FileSystem.Rename(FileAddress & NetDownloadEnd, FileAddress)
                Log("[Minecraft] 皮肤下载成功：" & FileAddress)
            End If
            Return FileAddress
        End SyncLock
    End Function

    ''' <summary>
    ''' 获取 Uuid 对应的皮肤，返回“Steve”或“Alex”。
    ''' </summary>
    Public Function McSkinSex(Uuid As String) As String
        If Not Uuid.Length = 32 Then Return "Steve"
        Dim a = Integer.Parse(Uuid(7), Globalization.NumberStyles.AllowHexSpecifier)
        Dim b = Integer.Parse(Uuid(15), Globalization.NumberStyles.AllowHexSpecifier)
        Dim c = Integer.Parse(Uuid(23), Globalization.NumberStyles.AllowHexSpecifier)
        Dim d = Integer.Parse(Uuid(31), Globalization.NumberStyles.AllowHexSpecifier)
        Return If((a Xor b Xor c Xor d) Mod 2, "Alex", "Steve")
        'Math.floorMod(uuid.hashCode(), 18)

        'Public Function hashCode(ByVal str As String) As Integer
        'Dim hash As Integer = 0
        'Dim n As Integer = str.Length
        'If n = 0 Then
        '    Return hash
        'End If
        'For i As Integer = 0 To n - 1
        '    hash = hash + Asc(str(i)) * (1 << (n - i - 1))
        'Next
        'Return hash
        'End Function
    End Function

#End Region

#Region "支持库文件（Libraries）"

    Public Class McLibToken
        ''' <summary>
        ''' 文件的完整本地路径。
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' 文件大小。若无有效数据即为 0。
        ''' </summary>
        Public Size As Long = 0
        ''' <summary>
        ''' 是否为 Natives 文件。
        ''' </summary>
        Public IsNatives As Boolean = False
        ''' <summary>
        ''' 文件的 SHA1。
        ''' </summary>
        Public SHA1 As String = Nothing
        ''' <summary>
        ''' 是否为纯本地文件，若是则不尝试联网下载。
        ''' </summary>
        Public IsLocal As Boolean = False
        ''' <summary>
        ''' 由 JSON 提供的 URL，若没有则为 Nothing。
        ''' </summary>
        Public Property Url As String
            Get
                Return _Url
            End Get
            Set(value As String)
                '孤儿 Forge 作者喜欢把没有 URL 的写个空字符串
                _Url = If(String.IsNullOrWhiteSpace(value), Nothing, value)
            End Set
        End Property
        Private _Url As String
        ''' <summary>
        ''' 原 JSON 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                If OriginalName Is Nothing Then Return Nothing
                Dim Splited As New List(Of String)(OriginalName.Split(":"))
                Splited.RemoveAt(2) 'Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                Return Join(Splited, ":")
            End Get
        End Property
        ''' <summary>
        ''' 原 JSON 中的 Name 项。
        ''' </summary>
        Public OriginalName As String

        Public Overrides Function ToString() As String
            Return If(IsNatives, "[Native] ", "") & GetString(Size) & " | " & LocalPath
        End Function
    End Class

    ''' <summary>
    ''' 检查是否符合 JSON 中的 Rules。
    ''' </summary>
    ''' <param name="RuleToken">JSON 中的 "rules" 项目。</param>
    Public Function McJsonRuleCheck(RuleToken As JToken) As Boolean
        If RuleToken Is Nothing Then Return True

        '初始化
        Dim Required As Boolean = False
        For Each Rule As JToken In RuleToken

            '单条条件验证
            Dim IsRightRule As Boolean = True '是否为正确的规则
            If Rule("os") IsNot Nothing Then '操作系统
                If Rule("os")("name") IsNot Nothing Then '操作系统名称
                    Dim OsName As String = Rule("os")("name").ToString
                    If OsName = "unknown" Then
                    ElseIf OsName = "windows" Then
                        If Rule("os")("version") IsNot Nothing Then '操作系统版本
                            Dim Cr As String = Rule("os")("version").ToString
                            IsRightRule = IsRightRule AndAlso RegexCheck(OSVersion, Cr)
                        End If
                    Else
                        IsRightRule = False
                    End If
                End If
                If Rule("os")("arch") IsNot Nothing Then '操作系统架构
                    IsRightRule = IsRightRule AndAlso ((Rule("os")("arch").ToString = "x86") = Is32BitSystem)
                End If
            End If
            If Not IsNothing(Rule("features")) Then '标签
                IsRightRule = IsRightRule AndAlso IsNothing(Rule("features")("is_demo_user")) '反选是否为 Demo 用户
                If CType(Rule("features"), JObject).Children.Any(Function(j As JProperty) j.Name.Contains("quick_play")) Then
                    IsRightRule = False '不开 Quick Play，让玩家自己加去
                End If
            End If

            '反选确认
            If Rule("action").ToString = "allow" Then
                If IsRightRule Then Required = True 'allow
            Else
                If IsRightRule Then Required = False 'disallow
            End If

        Next
        Return Required
    End Function
    Private OSVersion As String = Environment.OSVersion.Version.ToString()

    ''' <summary>
    ''' 递归获取 Minecraft 某一实例的完整支持库列表。
    ''' </summary>
    Public Function McLibListGet(Instance As McInstance, IncludeInstanceJar As Boolean) As List(Of McLibToken)

        '获取当前支持库列表
        Log("[Minecraft] 获取支持库列表：" & Instance.Name)
        Dim result = McLibListGetWithJson(Instance.JsonObject, TargetInstance:=Instance)

        '需要添加原版 Jar
        If IncludeInstanceJar Then
            Dim RealInstance As McInstance
            Dim RequiredJar As String = Instance.JsonObject("jar")?.ToString
            If Instance.IsHmclFormatJson OrElse RequiredJar Is Nothing Then
                'HMCL 项直接使用自身的 Jar
                '根据 Inherit 获取最深层实例
                Dim OriginalInstance As McInstance = Instance
                '1.17+ 的 Forge 不寻找 Inherit
                If Not ((Instance.Info.HasForge OrElse Instance.Info.HasNeoForge) AndAlso Instance.Info.Drop >= 170) Then
                    Do Until OriginalInstance.InheritInstanceName = ""
                        If OriginalInstance.InheritInstanceName = OriginalInstance.Name Then Exit Do
                        OriginalInstance = New McInstance(McFolderSelected & "versions\" & OriginalInstance.InheritInstanceName & "\")
                    Loop
                End If
                '需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                '复现：启动一个 Snapshot 实例
                RealInstance = New McInstance(OriginalInstance.PathInstance)
            Else
                'Json 已提供 Jar 字段，使用该字段的信息
                RealInstance = New McInstance(RequiredJar)
            End If
            Dim ClientUrl As String, ClientSHA1 As String
            '判断需求的实例是否存在
            '不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            If Not File.Exists(RealInstance.PathInstance & RealInstance.Name & ".json") Then
                RealInstance = Instance
                Log("[Minecraft] 可能缺少前置实例 " & RealInstance.Name & "，找不到对应的 JSON 文件", LogLevel.Debug)
            End If
            '获取详细下载信息
            If RealInstance.JsonObject("downloads") IsNot Nothing AndAlso RealInstance.JsonObject("downloads")("client") IsNot Nothing Then
                ClientUrl = RealInstance.JsonObject("downloads")("client")("url")
                ClientSHA1 = RealInstance.JsonObject("downloads")("client")("sha1")
            Else
                ClientUrl = Nothing
                ClientSHA1 = Nothing
            End If
            '把所需的原版 Jar 添加进去
            result.Add(New McLibToken With {.LocalPath = RealInstance.PathInstance & RealInstance.Name & ".jar", .Size = 0, .IsNatives = False, .Url = ClientUrl, .SHA1 = ClientSHA1})
        End If
        Return result
    End Function
    ''' <summary>
    ''' 获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    ''' </summary>
    Public Function McLibListGetWithJson(JsonObject As JObject, Optional KeepSameNameDifferentVersionResult As Boolean = False, Optional CustomMcFolder As String = Nothing, Optional TargetInstance As McInstance = Nothing) As List(Of McLibToken)
        CustomMcFolder = If(CustomMcFolder, McFolderSelected)
        Dim BasicArray As New List(Of McLibToken)

        '添加基础 Json 项
        Dim AllLibs As JArray = JsonObject("libraries")

        '转换为 LibToken
        For Each Library As JObject In AllLibs.Children

            '清理 null 项（BakaXL 会把没有的项序列化为 null，但会被 Newtonsoft 转换为 JValue，导致 Is Nothing = false；这导致了 #409）
            For i = Library.Properties.Count - 1 To 0 Step -1
                If Library.Properties(i).Value.Type = JTokenType.Null Then Library.Remove(Library.Properties(i).Name)
            Next

            '检查是否需要（Rules）
            If Not McJsonRuleCheck(Library("rules")) Then Continue For

            '获取根节点下的 url
            Dim RootUrl As String = Library("url")
            If RootUrl IsNot Nothing Then
                RootUrl += McLibGet(Library("name"), False, True, CustomMcFolder).Replace("\", "/")
            End If

            '是否为纯本地项
            Dim Hint As String = Library("hint")
            Dim IsLocal As Boolean = If(Hint IsNot Nothing, Hint = "local", False)

            '根据是否本地化处理（Natives）
            If Library("natives") Is Nothing Then '没有 Natives
                Dim LocalPath As String
                If IsLocal AndAlso TargetInstance IsNot Nothing Then '纯本地项
                    LocalPath = TargetInstance.PathInstance & "libraries\" & Library("name").ToString.AfterFirst(":").Replace(":", "-") & ".jar"
                Else
                    LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder)
                End If
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("artifact") IsNot Nothing Then
                        BasicArray.Add(New McLibToken With {
                            .OriginalName = Library("name"),
                            .Url = If(RootUrl, Library("downloads")("artifact")("url")),
                            .LocalPath = If(Library("downloads")("artifact")("path") Is Nothing, McLibGet(Library("name"),
                                CustomMcFolder:=CustomMcFolder), CustomMcFolder & "libraries\" & Library("downloads")("artifact")("path").ToString.Replace("/", "\")),
                            .Size = Val(Library("downloads")("artifact")("size").ToString),
                            .IsNatives = False,
                            .SHA1 = Library("downloads")("artifact")("sha1")?.ToString,
                            .IsLocal = IsLocal})
                    Else
                        BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing, .IsLocal = IsLocal})
                    End If
                Catch ex As Exception
                    Log(ex, "处理实际支持库列表失败（无 Natives，" & If(Library("name"), "Nothing").ToString & "）")
                    BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = LocalPath, .Size = 0, .IsNatives = False, .SHA1 = Nothing})
                End Try
            ElseIf Library("natives")("windows") IsNot Nothing Then '有 Windows Natives
                Try
                    If Library("downloads") IsNot Nothing AndAlso Library("downloads")("classifiers") IsNot Nothing AndAlso Library("downloads")("classifiers")("natives-windows") IsNot Nothing Then
                        BasicArray.Add(New McLibToken With {
                             .OriginalName = Library("name"),
                             .Url = If(RootUrl, Library("downloads")("classifiers")("natives-windows")("url")),
                             .LocalPath = If(Library("downloads")("classifiers")("natives-windows")("path") Is Nothing,
                                 McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")),
                                 CustomMcFolder & "libraries\" & Library("downloads")("classifiers")("natives-windows")("path").ToString.Replace("/", "\")),
                             .Size = Val(Library("downloads")("classifiers")("natives-windows")("size").ToString),
                             .IsNatives = True,
                             .SHA1 = Library("downloads")("classifiers")("natives-windows")("sha1").ToString,
                             .IsLocal = IsLocal})
                    Else
                        BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")), .Size = 0, .IsNatives = True, .SHA1 = Nothing, .IsLocal = IsLocal})
                    End If
                Catch ex As Exception
                    Log(ex, "处理实际支持库列表失败（有 Natives，" & If(Library("name"), "Nothing").ToString & "）")
                    BasicArray.Add(New McLibToken With {.OriginalName = Library("name"), .Url = RootUrl, .LocalPath = McLibGet(Library("name"), CustomMcFolder:=CustomMcFolder).Replace(".jar", "-" & Library("natives")("windows").ToString & ".jar").Replace("${arch}", If(Environment.Is64BitOperatingSystem, "64", "32")), .Size = 0, .IsNatives = True, .SHA1 = Nothing, .IsLocal = False})
                End Try
            End If

        Next

        '去重
        Dim ResultArray As New Dictionary(Of String, McLibToken)
        Dim GetVersion =
        Function(Token As McLibToken) As String
            '测试例：
            'D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
            'D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
            'D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
            Return GetFolderNameFromPath(GetPathFromFullPath(Token.LocalPath))
        End Function
        For i = 0 To BasicArray.Count - 1
            Dim Key As String = BasicArray(i).Name & BasicArray(i).IsNatives.ToString
            If ResultArray.ContainsKey(Key) Then
                Dim BasicArrayVersion As String = GetVersion(BasicArray(i))
                Dim ResultArrayVersion As String = GetVersion(ResultArray(Key))
                If BasicArrayVersion <> ResultArrayVersion AndAlso KeepSameNameDifferentVersionResult Then
                    Log($"[Minecraft] 发现疑似重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})")
                    ResultArray.Add(Key & GetUuid(), BasicArray(i))
                Else
                    Log($"[Minecraft] 发现重复的支持库：{BasicArray(i)} ({BasicArrayVersion}) 与 {ResultArray(Key)} ({ResultArrayVersion})，已忽略其中之一")
                    If CompareVersionGe(BasicArrayVersion, ResultArrayVersion) Then
                        ResultArray(Key) = BasicArray(i)
                    End If
                End If
            Else
                ResultArray.Add(Key, BasicArray(i))
            End If
        Next
        Return ResultArray.Values.ToList
    End Function

    ''' <summary>
    ''' 获取实例所需支持库文件的 NetFile。
    ''' </summary>
    Public Function McLibNetFilesFromInstance(instance As McInstance) As List(Of NetFile)
        If Not instance.IsLoaded Then instance.Load()
        Dim result As New List(Of NetFile)

        '更新此方法时需要同步更新 Forge 新版自动安装方法！

        '主 Jar 文件
        Try
            Dim mainJar As NetFile = DlClientJarGet(instance, True)
            If mainJar IsNot Nothing Then result.Add(mainJar)
        Catch ex As Exception
            Log(ex, "实例缺失主 Jar 文件所必须的信息", LogLevel.Developer)
        End Try

        'Library 文件
        result.AddRange(McLibNetFilesFromTokens(McLibListGet(instance, False)))

        'Authlib-Injector 文件
        Dim authlibTargetFile = PathPure & "\authlib-injector.jar"
        Dim authlibDownloadInfo As JObject = Nothing
        Try
            Log("[Minecraft] 开始获取 Authlib-Injector 下载信息")
            authlibDownloadInfo = GetJson(NetGetCodeByLoader({
                        "https://authlib-injector.yushi.moe/artifact/latest.json",
                        "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                    }, IsJson:=True))
        Catch ex As Exception
            Log(ex, "获取 Authlib-Injector 下载信息失败")
        End Try
        '校验文件
        If authlibDownloadInfo IsNot Nothing Then
            Dim checker As New FileChecker(Hash:=authlibDownloadInfo("checksums")("sha256").ToString)
            If checker.Check(authlibTargetFile) IsNot Nothing Then
                '开始下载
                Dim downloadAddress As String = authlibDownloadInfo("download_url").ToString.
                            Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe")
                Log("[Minecraft] Authlib-Injector 需要更新：" & downloadAddress, LogLevel.Developer)
                result.Add(New NetFile({
                        downloadAddress,
                        downloadAddress.Replace("authlib-injector.yushi.moe", "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                    }, authlibTargetFile, New FileChecker(Hash:=authlibDownloadInfo("checksums")("sha256").ToString)))
            End If
        End If

        '修改渲染器
        Dim mesaLoaderWindowsVersion = "25.3.5"
        Dim mesaLoaderWindowsTargetFile = PathPure & "\mesa-loader-windows\" & mesaLoaderWindowsVersion & "\Loader.jar"
        Dim renderer = -1
        If McInstanceSelected IsNot Nothing Then
            renderer = Setup.Get("VersionAdvanceRenderer", instance:=McInstanceSelected) - 1
        End If
        If renderer = -1 Then
            renderer = Setup.Get("LaunchAdvanceRenderer")
        End If

        If renderer <> 0 AndAlso Not File.Exists(mesaLoaderWindowsTargetFile) Then
            Dim downloadAddress As String = "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/org/glavo/mesa-loader-windows/" & mesaLoaderWindowsVersion & "/mesa-loader-windows-" & mesaLoaderWindowsVersion & "-" & If(ModBase.Is32BitSystem, "x86", If(ModBase.IsArm64System, "arm64", "x64")) & ".jar"
            result.Add(New NetFile({downloadAddress}, mesaLoaderWindowsTargetFile))
        End If

        'LabyMod Assets 文件
        If instance.Info.HasLabyMod Then
            If instance.PathIndie = instance.PathInstance Then
                If Directory.Exists(instance.PathInstance & "labymod-neo") Then Directory.Delete(instance.PathInstance & "labymod-neo", True)
                CreateSymbolicLink(instance.PathInstance & "labymod-neo", McFolderSelected & "labymod-neo", &H2)
            End If
            Try
                Dim channelType = instance.JsonObject("labymod_data")("channelType").ToString()
                Directory.CreateDirectory($"{McFolderSelected}labymod-neo\libraries")
                Log("[Minecraft] 开始获取 LabyMod 信息")
                Dim labyManifest As JObject = NetGetCodeByRequestRetry($"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json", IsJson:=True)
                Dim labyAssets As JObject = labyManifest("assets")
                Dim labyModCommitRef As String = labyManifest("commitReference").ToString()
                For Each Asset In labyAssets
                    Dim assetName As String = Asset.Key
                    Dim assetSHA1 As String = Asset.Value.ToString()
                    Dim assetPath As String = $"{McFolderSelected}labymod-neo\assets\{assetName}.jar"
                    Dim assetUrl As String = $"https://releases.r2.labymod.net/api/v1/download/assets/labymod4/{channelType}/{labyModCommitRef}/{assetName}/{assetSHA1}.jar"
                    Dim checker = New FileChecker(Hash:=assetSHA1)
                    If checker.Check(assetPath) Is Nothing Then Continue For
                    result.Add(New NetFile(
                           {assetUrl},
                           assetPath,
                           checker))
                Next
            Catch ex As Exception
                Log(ex, "获取 LabyMod 信息失败，跳过检查")
            End Try
        End If

        '跳过校验
        If ShouldIgnoreFileCheck(instance) Then
            Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件")
            result = result.Where(
            Function(f)
                If File.Exists(f.LocalPath) Then
                    Log("[Minecraft] 跳过下载的支持库文件：" & f.LocalPath, LogLevel.Debug)
                    Return False
                Else
                    Return True
                End If
            End Function).ToList
        End If

        Return result
    End Function
    ''' <summary>
    ''' 将 McLibToken 列表转换为 NetFile。
    ''' </summary>
    Public Function McLibNetFilesFromTokens(libs As List(Of McLibToken), Optional customMcFolder As String = Nothing) As List(Of NetFile)
        customMcFolder = If(customMcFolder, McFolderSelected)
        Dim result As New List(Of NetFile)
        '获取
        For Each token As McLibToken In libs
            '检查文件
            Dim checker As New FileChecker(ActualSize:=If(token.Size = 0, -1, token.Size), Hash:=token.SHA1)
            If checker.Check(token.LocalPath) Is Nothing Then Continue For
            If token.IsLocal Then
                Log("[Download] 已跳过被标记为本地文件的支持库: " & token.OriginalName)
                Continue For
            End If
            'URL
            Dim urls As New List(Of String)
            If token.Url Is Nothing AndAlso token.Name = "net.minecraftforge:forge:universal" Then
                '特判修复 Forge 部分 universal 文件缺失 URL（#5455）
                token.Url = "https://maven.minecraftforge.net" & token.LocalPath.Replace(customMcFolder & "libraries", "").Replace("\", "/")
            End If
            If token.Url IsNot Nothing Then
                '获取 URL 的真实地址
                urls.Add(token.Url)
                If token.Url.Contains("launcher.mojang.com/v1/objects") OrElse token.Url.Contains("client.txt") OrElse
                   token.Url.Contains(".tsrg") Then
                    urls.AddRange(DlSourceLauncherOrMetaGet(token.Url)) 'Mappings（#4425）
                End If
                If token.Url.Contains("maven") Then
                    Dim bmclapiUrl As String =
                        token.Url.Replace(Mid(token.Url, 1, token.Url.IndexOfF("maven")), "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven").Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven")
                    If DlSourcePreferMojang Then
                        urls.Add(bmclapiUrl) '官方源优先
                    Else
                        urls.Insert(0, bmclapiUrl) '镜像源优先
                    End If
                End If
            End If
            If token.LocalPath.Contains("transformer-discovery-service") Then
                'Transformer 文件释放
                If Not File.Exists(token.LocalPath) Then WriteFile(token.LocalPath, GetResourceStream("Resources/transformer.jar"))
                Log("[Download] 已自动释放 Transformer Discovery Service", LogLevel.Developer)
                Continue For
            ElseIf token.LocalPath.Contains("optifine\OptiFine") Then
                'OptiFine 主 Jar
                Dim optiFineBase As String = token.LocalPath.Replace(customMcFolder & "libraries\optifine\OptiFine\", "").Split("_")(0) & "/" & GetFileNameFromPath(token.LocalPath).Replace("-", "_")
                optiFineBase = "/maven/com/optifine/" & optiFineBase
                If optiFineBase.Contains("_pre") Then optiFineBase = optiFineBase.Replace("com/optifine/", "com/optifine/preview_")
                urls.Add("https://bmclapi2.bangbang93.com" & optiFineBase)
            ElseIf token.Name.Contains("LabyMod") Then
                'LabyMod 只有一个下载源
                urls.Add(token.Url)
                Log($"[Download] 获取到 LabyMod 主要库文件的 Size = {token.Size},SHA1 = {token.SHA1}，由于 LabyMod 乱写 Size，已忽略 Size")
                checker = New FileChecker(Hash:=token.SHA1) '只校验 SHA1
            ElseIf urls.Count <= 2 Then
                '普通文件
                urls.AddRange(DlSourceLibraryGet("https://libraries.minecraft.net" & token.LocalPath.Replace(customMcFolder & "libraries", "").Replace("\", "/")))
            End If
            result.Add(New NetFile(urls.Distinct, token.LocalPath, checker))
        Next
        '去重并返回
        Return result.Distinct(Function(a, b) a.LocalPath = b.LocalPath)
    End Function
    ''' <summary>
    ''' 获取对应的支持库文件地址。
    ''' </summary>
    ''' <param name="original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    ''' <param name="withHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
    Public Function McLibGet(original As String, Optional withHead As Boolean = True, Optional ignoreLiteLoader As Boolean = False, Optional customMcFolder As String = Nothing) As String
        customMcFolder = If(customMcFolder, McFolderSelected)
        Dim splited = original.Split(":")
        McLibGet = If(withHead, customMcFolder & "libraries\", "") &
                   splited(0).Replace(".", "\") & "\" & splited(1) & "\" & splited(2) & "\" & splited(1) & "-" & splited(2) & ".jar"
        '判断 OptiFine 是否应该使用 installer
        If McLibGet.Contains("optifine\OptiFine\1.") AndAlso splited(2).Split(".").Count > 1 Then
            Dim majorVersion As Integer = Val(splited(2).Split(".")(1).BeforeFirst("_"))
            Dim minorVersion As Integer = If(splited(2).Split(".").Count > 2, Val(splited(2).Split(".")(2).BeforeFirst("_")), 0)
            If (majorVersion = 12 OrElse (majorVersion = 20 AndAlso minorVersion >= 4) OrElse majorVersion >= 21) AndAlso '仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
                File.Exists($"{customMcFolder}libraries\{splited(0).Replace(".", "\")}\{splited(1)}\{splited(2)}\{splited(1)}-{splited(2)}-installer.jar") Then
                McLaunchLog("已将 " & original & " 替换为对应的 Installer 文件")
                McLibGet = McLibGet.Replace(".jar", "-installer.jar")
            End If
        End If
    End Function

    ''' <summary>
    ''' 检查设置，是否应当忽略文件检查？
    ''' </summary>
    Public Function ShouldIgnoreFileCheck(Version As McInstance)
        Return Setup.Get("VersionAdvanceAssetsV2", instance:=Version) OrElse (Setup.Get("VersionAdvanceAssets", instance:=Version) = 2)
    End Function

#End Region

#Region "资源文件（Assets）"

    '获取索引
    ''' <summary>
    ''' 获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
    ''' </summary>
    Public Function McAssetsGetIndex(instance As McInstance, Optional returnLegacyOnError As Boolean = False, Optional checkURLEmpty As Boolean = False) As JToken
        Dim assetsName As String
        Try
            Do While True
                Dim index As JToken = instance.JsonObject("assetIndex")
                If index IsNot Nothing AndAlso index("id") IsNot Nothing Then Return index
                If instance.JsonObject("assets") IsNot Nothing Then assetsName = instance.JsonObject("assets").ToString
                If checkURLEmpty AndAlso index("url") IsNot Nothing Then Return index
                '下一个实例
                If instance.InheritInstanceName = "" Then Exit Do
                instance = New McInstance(McFolderSelected & "versions\" & instance.InheritInstanceName)
            Loop
        Catch
        End Try
        '无法获取到下载地址
        If returnLegacyOnError Then
            '返回 assets 文件名会由于没有下载地址导致全局失败
            'If AssetsName IsNot Nothing AndAlso AssetsName <> "legacy" Then
            '    Log("[Minecraft] 无法获取资源文件索引下载地址，使用 assets 项提供的资源文件名：" & AssetsName)
            '    Return GetJson("{""id"": """ & AssetsName & """}")
            'Else
            Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址")
            Return GetJson("{
                ""id"": ""legacy"",
                ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                ""size"": 134284,
                ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                ""totalSize"": 111220701
            }")
            'End If
        Else
            Throw New Exception("该实例不存在资源文件索引信息")
        End If
    End Function
    ''' <summary>
    ''' 获取某实例资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    ''' </summary>
    Public Function McAssetsGetIndexName(instance As McInstance) As String
        Try
            Do While True
                If instance.JsonObject("assetIndex") IsNot Nothing AndAlso instance.JsonObject("assetIndex")("id") IsNot Nothing Then
                    Return instance.JsonObject("assetIndex")("id").ToString
                End If
                If instance.JsonObject("assets") IsNot Nothing Then
                    Return instance.JsonObject("assets").ToString
                End If
                If instance.InheritInstanceName = "" Then Exit Do
                instance = New McInstance(McFolderSelected & "versions\" & instance.InheritInstanceName)
            Loop
        Catch ex As Exception
            Log(ex, "获取资源文件索引名失败")
        End Try
        Return "legacy"
    End Function

    '获取列表
    Private Structure McAssetsToken
        ''' <summary>
        ''' 文件的完整本地路径。
        ''' </summary>
        Public LocalPath As String
        ''' <summary>
        ''' Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        ''' </summary>
        Public SourcePath As String
        ''' <summary>
        ''' 文件大小。若无有效数据即为 0。
        ''' </summary>
        Public Size As Long
        ''' <summary>
        ''' 文件的 Hash 校验码。
        ''' </summary>
        Public Hash As String

        Public Overrides Function ToString() As String
            Return GetString(Size) & " | " & LocalPath
        End Function
    End Structure
    ''' <summary>
    ''' 获取 Minecraft 的资源文件列表。失败会抛出异常。
    ''' </summary>
    Private Function McAssetsListGet(instance As McInstance) As List(Of McAssetsToken)
        Dim indexName = McAssetsGetIndexName(instance)
        Try

            '初始化
            If Not File.Exists($"{McFolderSelected}assets\indexes\{indexName}.json") Then Throw New FileNotFoundException("未找到 Asset Index", McFolderSelected & "assets\indexes\" & indexName & ".json")
            Dim result As New List(Of McAssetsToken)
            Dim json As JsonObject = JsonObject.Parse(ReadFile($"{McFolderSelected}assets\indexes\{indexName}.json"))

            '读取列表
            For Each file As KeyValuePair(Of String,JsonNode) In json("objects").AsObject()
                Dim localPath As String
                If json("map_to_resources") IsNot Nothing AndAlso json("map_to_resources").GetValue(Of Boolean) Then
                    'Remap
                    localPath = instance.PathIndie & "resources\" & file.Key.Replace("/", "\")
                ElseIf json("virtual") IsNot Nothing AndAlso json("virtual").GetValue(Of Boolean) Then
                    'Virtual
                    localPath = McFolderSelected & "assets\virtual\legacy\" & file.Key.Replace("/", "\")
                Else
                    '正常
                    localPath = McFolderSelected & "assets\objects\" & Left(file.Value("hash").ToString, 2) & "\" & file.Value("hash").ToString
                End If
                result.Add(New McAssetsToken With {
                    .LocalPath = localPath,
                    .SourcePath = file.Key,
                    .Hash = file.Value("hash").ToString,
                    .Size = file.Value("size").ToString
                })
            Next
            Return result

        Catch ex As Exception
            Log(ex, "获取资源文件列表失败：" & indexName)
            Throw
        End Try
    End Function

    '获取缺失列表
    ''' <summary>
    ''' 获取实例缺失的资源文件所对应的 NetTaskFile。
    ''' </summary>
    Public Function McAssetsFixList(instance As McInstance, checkHash As Boolean, Optional ByRef progressFeed As LoaderBase = Nothing) As List(Of NetFile)
        '如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
        If checkHash Then
            Return McAssetsListGet(instance).
                Select(Function(token As McAssetsToken) New NetFile(
                    DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(token.Hash, 2)}/{token.Hash}"),
                    LocalPath:=token.LocalPath,
                    Checker:=New FileChecker(ActualSize:=If(token.Size = 0, -1, token.Size),
                                             Hash:=token.Hash))).ToList
        End If
        '如果不检查 Hash，则立即处理
        Dim result As New List(Of NetFile)

        Dim assetsList As List(Of McAssetsToken)
        Try
            assetsList = McAssetsListGet(instance)
            Dim token As McAssetsToken
            If progressFeed IsNot Nothing Then progressFeed.Progress = 0.04
            For i = 0 To assetsList.Count - 1
                '初始化
                token = assetsList(i)
                If progressFeed IsNot Nothing Then progressFeed.Progress = 0.05 + 0.94 * i / assetsList.Count
                '检查文件是否存在
                Dim file As New FileInfo(token.LocalPath)
                If File.Exists AndAlso (Token.Size = 0 OrElse Token.Size = File.Length) Then Continue For
                '文件不存在，添加下载
                result.Add(New NetFile(DlSourceAssetsGet($"https://resources.download.minecraft.net/{Left(token.Hash, 2)}/{token.Hash}"), token.LocalPath, New FileChecker(ActualSize:=If(token.Size = 0, -1, token.Size), Hash:=token.Hash)))
            Next
        Catch ex As Exception
            Log(ex, "获取实例缺失的资源文件下载列表失败")
        End Try
        If progressFeed IsNot Nothing Then progressFeed.Progress = 0.99
        Return result
    End Function

#End Region

    ''' <summary>
    ''' 发送 Minecraft 更新提示。
    ''' </summary>
    Public Sub McDownloadClientUpdateHint(versionName As String, json As JObject)
        Try

            '获取对应版本
            Dim version As JToken = Nothing
            For Each Token In json("versions")
                If Token("id") IsNot Nothing AndAlso Token("id").ToString = versionName Then
                    version = Token
                    Exit For
                End If
            Next
            '进行提示
            If version Is Nothing Then Return
            Dim time As Date = version("releaseTime")
            Dim msgBoxText As String = $"新版本：{versionName}{vbCrLf}" &
                If((Date.Now - time).TotalDays > 1, "更新时间：" & time.ToString, "更新于：" & TimeUtils.GetTimeSpanString(time - Date.Now, False))
            Dim msgResult = MyMsgBox(msgBoxText, "Minecraft 更新提示", "确定", "下载", If((Date.Now - time).TotalHours > 3, "更新日志", ""),
                Button3Action:=Sub() McUpdateLogShow(version))
            '弹窗结果
            If msgResult = 2 Then
                '下载
                RunInUi(
                Sub()
                    PageDownloadInstall.McVersionWaitingForSelect = versionName
                    FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall)
                End Sub)
            End If

        Catch ex As Exception
            Log(ex, "Minecraft 更新提示发送失败（" & If(versionName, "Nothing") & "）", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 比较两个版本名；等同 Left >= Right。
    ''' 无法比较两个预发布版的大小。
    ''' 支持的格式：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function CompareVersionGe(left As String, right As String) As Boolean
        Return CompareVersion(left, right) >= 0
    End Function
    ''' <summary>
    ''' 比较两个版本名，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1；等同 Left - Right。
    ''' 无法比较两个预发布版的大小。
    ''' 支持的格式：未知版本, 26.1-snapshot-1，1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    ''' </summary>
    Public Function CompareVersion(left As String, right As String) As Integer
        If left = "未知版本" OrElse right = "未知版本" Then
            If left = "未知版本" AndAlso right <> "未知版本" Then Return 1
            If left = "未知版本" AndAlso right = "未知版本" Then Return 0
            If left <> "未知版本" AndAlso right = "未知版本" Then Return -1
        End If
        left = left.ToLowerInvariant
        right = right.ToLowerInvariant
        Dim lefts = RegexSearch(left.Replace("快照", "snapshot").Replace("预览版", "pre"), "[a-z]+|[0-9]+")
        Dim rights = RegexSearch(right.Replace("快照", "snapshot").Replace("预览版", "pre"), "[a-z]+|[0-9]+")
        Dim i As Integer = 0
        While True
            '两边均缺失，感觉是一个东西
            If lefts.Count - 1 < i AndAlso rights.Count - 1 < i Then
                If left > right Then Return 1
                If left < right Then Return -1
                Return 0
            End If
            '确定两边的数值
            Dim leftValue As String = If(lefts.Count - 1 < i, 0, lefts(i))
            Dim rightValue As String = If(rights.Count - 1 < i, 0, rights(i))
            If leftValue = rightValue Then GoTo NextEntry
            If leftValue = "rc" Then leftValue = -1
            If leftValue = "pre" Then leftValue = -2
            If leftValue = "snapshot" Then leftValue = -3
            If leftValue = "experimental" Then leftValue = -4
            Dim leftValValue = Val(leftValue)
            If rightValue = "rc" Then rightValue = -1
            If rightValue = "pre" Then rightValue = -2
            If rightValue = "snapshot" Then rightValue = -3
            If rightValue = "experimental" Then rightValue = -4
            Dim rightValValue = Val(rightValue)
            If leftValValue = 0 AndAlso rightValValue = 0 Then
                '如果没有数值则直接比较字符串
                If leftValue > rightValue Then
                    Return 1
                ElseIf leftValue < rightValue Then
                    Return -1
                End If
            Else
                '如果有数值则比较数值
                '这会使得一边是数字一边是字母时数字方更大
                If leftValValue > rightValValue Then
                    Return 1
                ElseIf leftValValue < rightValValue Then
                    Return -1
                End If
            End If
NextEntry:
            i += 1
        End While
        Return 0
    End Function
    ''' <summary>
    ''' 比较两个版本名的排序器。
    ''' </summary>
    Public Class VersionComparer
        Implements IComparer(Of String)
        Public Function Compare(x As String, y As String) As Integer Implements IComparer(Of String).Compare
            Return CompareVersion(x, y)
        End Function
    End Class

    ''' <summary>
    ''' 打码字符串中的 AccessToken。
    ''' </summary>
    Public Function FilterAccessToken(Raw As String, FilterChar As Char) As String
        '打码 "accessToken " 后的内容
        If Raw.Contains("accessToken ") Then
            For Each Token In RegexSearch(Raw, "(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})")
                Raw = Raw.Replace(Token, New String(FilterChar, Token.Count))
            Next
        End If
        '打码当前登录的结果
        Dim AccessToken As String = McLoginLoader.Output.AccessToken
        If AccessToken IsNot Nothing AndAlso AccessToken.Length >= 10 AndAlso Raw.ContainsF(AccessToken, True) AndAlso
            McLoginLoader.Output.Uuid <> McLoginLoader.Output.AccessToken Then 'UUID 和 AccessToken 一样则不打码
            Raw = Raw.Replace(AccessToken, Left(AccessToken, 5) & New String(FilterChar, AccessToken.Length - 10) & Right(AccessToken, 5))
        End If
        Return Raw
    End Function
    ''' <summary>
    ''' 打码字符串中的 Windows 用户名。
    ''' </summary>
    Public Function FilterUserName(Raw As String, FilterChar As Char) As String
        Dim UserProfile As String = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        Dim UserName As String = UserProfile.Split("\").Last
        Dim MaskedProfile = UserProfile.Replace(UserName, New String(FilterChar, UserName.Length))
        Return Raw.Replace(UserProfile, MaskedProfile)
    End Function

End Module
