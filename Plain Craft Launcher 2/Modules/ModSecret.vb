Imports System.ComponentModel
Imports System.Management
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports PCL.Core.App
Imports PCL.Core.UI.Theme
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Utils.OS
Imports PCL.Core.Utils.Secret

Friend Module ModSecret

#Region "杂项"

#If DEBUG Then
    Public Const RegFolder As String = "PCLCEDebug" '社区开发版的注册表与社区常规版的注册表隔离，以防数据冲突
#Else
    Public Const RegFolder As String = "PCLCE" 'PCL 社区版的注册表与 PCL 的注册表隔离，以防数据冲突
#End If
    '用于微软登录的 ClientId
    Public ReadOnly OAuthClientId As String = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly:=True).ReplaceNullOrEmpty()
    'CurseForge API Key
    Public ReadOnly CurseForgeAPIKey As String = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly:=True).ReplaceNullOrEmpty()
    '遥测鉴权密钥
    Public ReadOnly TelemetryKey As String = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly:=True).ReplaceNullOrEmpty()
    'Natayark ID Client Id
    Public ReadOnly NatayarkClientId As String = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly:=True).ReplaceNullOrEmpty()
    'Natayark ID Client Secret，需要经过 PASSWORD HASH 处理（https://uutool.cn/php-password/）
    Public ReadOnly NatayarkClientSecret As String = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly:=True).ReplaceNullOrEmpty()
    '联机服务根地址
    Public ReadOnly LinkServers As String() = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly:=True).ReplaceNullOrEmpty().Split("|")

    Friend Sub SecretOnApplicationStart()
        '提升 UI 线程优先级
        Thread.CurrentThread.Priority = ThreadPriority.Highest
        '确保 .NET Framework 版本
        Try
            Dim VersionTest As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
        Catch ex As UriFormatException '修复 #3555
            Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"), EnvironmentVariableTarget.User)
            Dim VersionTest As New FormattedText("", Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Fonts.SystemTypefaces.First, 96, New MyColor, DPI)
        End Try
        '检测当前文件夹权限
        Dim dataPath = Paths.Data
        Try
            Directory.CreateDirectory(dataPath)
        Catch ex As Exception
            MsgBox($"PCL 无法创建 PCL 文件夹（{dataPath}），请尝试：" & vbCrLf &
                  "1. 将 PCL 移动到其他文件夹" & If(ExePath.StartsWithF("C:", True), "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 PCL 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
            Environment.[Exit](ProcessReturnValues.Cancel)
        End Try
        If Not CheckPermission(ExePath & "PCL") Then
            MsgBox("PCL 没有对当前文件夹的写入权限，请尝试：" & vbCrLf &
                  "1. 将 PCL 移动到其他文件夹" & If(ExePath.StartsWithF("C:", True), "，例如 C 盘和桌面以外的其他位置。", "。") & vbCrLf &
                  "2. 删除当前目录中的 PCL 文件夹，然后再试。" & vbCrLf &
                  "3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。",
                MsgBoxStyle.Critical, "运行环境错误")
            Environment.[Exit](ProcessReturnValues.Cancel)
        End If
    End Sub
    ''' <summary>
    ''' 展示社区版提示
    ''' </summary>
    ''' <param name="IsUpdate">是否为更新时启动</param>
    Public Sub ShowCEAnnounce()
        MyMsgBox($"你正在使用来自 PCL-Community 的 PCL 社区版本，遇到问题请不要向官方仓库反馈！
PCL-Community 及其成员与龙腾猫跃无从属关系，且均不会为您的使用做担保。

如果你是意外下载的社区版，建议下载官方版 PCL 使用。
如果你是意外下载的社区版，建议下载官方版 PCL 使用。
如果你是意外下载的社区版，建议下载官方版 PCL 使用。

该版本与官方版本的特性区别：
- 主题切换：仅部分固定蓝色系主题，没有计划新增其它主题。
- 百宝箱：缺失部分官方版中的内容（回声洞、千万别点）。

此提示会在启动器更新后展示一次。", "社区版本说明", "我知道了")
    End Sub

    ''' <summary>
    ''' 获取设备的短标识码
    ''' </summary>
    Friend Function SecretGetUniqueAddress() As String
        Return Identify.LauncherId
    End Function

    Friend Sub SecretLaunchJvmArgs(ByRef DataList As List(Of String))
        Dim DataJvmCustom As String = Setup.Get("VersionAdvanceJvm", instance:=McInstanceSelected)
        DataList.Insert(0, If(DataJvmCustom = "", Setup.Get("LaunchAdvanceJvm"), DataJvmCustom)) '可变 JVM 参数
        Select Case Setup.Get("LaunchPreferredIpStack")
            Case 0
                DataList.Add("-Djava.net.preferIPv4Stack=true")
                DataList.Add("-Djava.net.preferIPv4Addresses=true")
            Case 2
                DataList.Add("-Djava.net.preferIPv6Stack=true")
                DataList.Add("-Djava.net.preferIPv6Addresses=true")
        End Select
        McLaunchLog("当前剩余内存：" & Math.Round(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10) / 10 & "G")
        DataList.Add("-Xmn" & Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected) * 1024 * 0.15) & "m")
        DataList.Add("-Xmx" & Math.Floor(PageInstanceSetup.GetRam(McInstanceSelected) * 1024) & "m")
        If Not DataList.Any(Function(d) d.Contains("-Dlog4j2.formatMsgNoLookups=true")) Then DataList.Add("-Dlog4j2.formatMsgNoLookups=true")
    End Sub

#End Region

#Region "网络鉴权"
    Friend Function SecretCdnSign(UrlWithMark As String)
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Client As HttpRequestMessage, Optional UseBrowserUserAgent As Boolean = False, Optional CustomUserAgent As String = "")
        Client.Version = HttpVersion.Version20
        Client.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        If Url.Contains("api.curseforge.com") Then Client.Headers.Add("x-api-key", CurseForgeAPIKey)
        Dim userAgent As String = If(Not String.IsNullOrEmpty(CustomUserAgent),
                                     CustomUserAgent,
                                         If(UseBrowserUserAgent,
                                             $"PCLCE/{VersionStandardCode} PCL2/{UpstreamVersion}.{VersionBranchCode} Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0",
                                             $"PCLCE/{VersionStandardCode} PCL2/{UpstreamVersion}.{VersionBranchCode}"
                                         )
                                     )
        Client.Headers.Add("User-Agent", userAgent)

        Client.Headers.Add("Referer", "http://" & VersionCode & ".ce.open.pcl2.server/")
    End Sub

#End Region

#Region "主题"

#If DEBUG Then
    Public ReadOnly EnableCustomTheme As Boolean = Environment.GetEnvironmentVariable("PCL_CUSTOM_THEME") IsNot Nothing
    Private ReadOnly EnvThemeHue = Environment.GetEnvironmentVariable("PCL_THEME_HUE") '0 ~ 359
    Private ReadOnly EnvThemeSat = Environment.GetEnvironmentVariable("PCL_THEME_SAT") '0 ~ 100
    Private ReadOnly EnvThemeLight = Environment.GetEnvironmentVariable("PCL_THEME_LIGHT") '-20 ~ 20
    Private ReadOnly EnvThemeHueDelta = Environment.GetEnvironmentVariable("PCL_THEME_HUE_DELTA") '-90 ~ 90
    Private ReadOnly CustomThemeHue = If(EnvThemeHue Is Nothing, Nothing, CType(Integer.Parse(EnvThemeHue), Integer?))
    Private ReadOnly CustomThemeSat = If(EnvThemeSat Is Nothing, Nothing, CType(Integer.Parse(EnvThemeSat), Integer?))
    Private ReadOnly CustomThemeLight = If(EnvThemeLight Is Nothing, Nothing, CType(Integer.Parse(EnvThemeLight), Integer?))
    Private ReadOnly CustomThemeHueDelta = If(EnvThemeHueDelta Is Nothing, Nothing, CType(Integer.Parse(EnvThemeHueDelta), Integer?))
#End If

    Public ReadOnly Property IsDarkMode As Boolean
        Get
            Return ThemeService.IsDarkMode
        End Get
    End Property

    Public ReadOnly Property AppResources As ResourceDictionary
        Get
            Return Application.Current.Resources
        End Get
    End Property

    Public ColorGray1 As New MyColor(AppResources("ColorObjectGray1"))
    Public ColorGray4 As New MyColor(AppResources("ColorObjectGray4"))
    Public ColorGray5 As New MyColor(AppResources("ColorObjectGray5"))
    Public ColorSemiTransparent As New MyColor(AppResources("ColorBrushSemiTransparent"))

    Public ThemeNow As Integer = -1
    'Public ColorHue As Integer = If(IsDarkMode, 200, 210), ColorSat As Integer = If(IsDarkMode, 100, 85), ColorLightAdjust As Integer = If(IsDarkMode, 15, 0), ColorHueTopbarDelta As Object = 0
    Public ThemeDontClick As Integer = 0

    '深色模式事件
#If False
    ' 定义自定义事件
    Public Event ThemeChanged As EventHandler(Of Boolean)

    ' 触发事件的函数
    Public Sub RaiseThemeChanged(isDarkMode As Boolean)
        RaiseEvent ThemeChanged("", isDarkMode)
    End Sub
#End If
    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        'ThemeRefreshColor()
        'RaiseThemeChanged(IsDarkMode)
        ColorGray1 = New MyColor(AppResources("ColorObjectGray1"))
        ColorGray4 = New MyColor(AppResources("ColorObjectGray4"))
        ColorGray5 = New MyColor(AppResources("ColorObjectGray5"))
        ColorSemiTransparent = New MyColor(AppResources("ColorBrushSemiTransparent"))
        ThemeRefreshMain()
    End Sub

    Public Function GetDarkThemeLight(OriginalLight As Double) As Double
        If IsDarkMode Then
            Return OriginalLight * 0.2
        Else
            Return OriginalLight
        End If
    End Function

#If False
    Private ReadOnly HueList As Integer() = {200, 210, 225}
    Private ReadOnly SatList As Integer() = {100, 85, 70}
    Private ReadOnly LightList As Integer() = {7, 0, -2}

    Public Sub ThemeRefreshColor()
#If DEBUG Then
        If EnableCustomTheme Then
            If CustomThemeHue IsNot Nothing Then ColorHue = CustomThemeHue
            If CustomThemeSat IsNot Nothing Then ColorSat = CustomThemeSat
            If CustomThemeLight IsNot Nothing Then ColorLightAdjust = CustomThemeLight
            If CustomThemeHueDelta IsNot Nothing Then ColorHueTopbarDelta = CustomThemeHueDelta
        Else
#End If
            Dim colorIndex As Integer = If(IsDarkMode, Setup.Get("UiDarkColor"), Setup.Get("UiLightColor"))
            ColorHue = HueList(colorIndex)
            ColorSat = SatList(colorIndex)
            ColorLightAdjust = LightList(colorIndex)
            ColorHueTopbarDelta = 0
#If DEBUG Then
        End If
#End If
    End Sub
#End If

    Public Sub ThemeRefreshMain()
#If DEBUG Then
        If EnableCustomTheme Then ThemeNow = 14
#End If
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
#If False
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim lightAdjust = ColorLightAdjust * 1.2
            If ThemeNow = 5 Then
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 25)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 15)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, 25)})
                FrmMain.PanTitle.Background = Brush
                FrmMain.PanTitle.Background.Freeze()
            ElseIf Not (ThemeNow = 12 OrElse ThemeDontClick = 2) Then
                If TypeOf ColorHueTopbarDelta Is Integer Then
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue - ColorHueTopbarDelta, ColorSat, AdjustLight(48, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue, ColorSat, AdjustLight(54, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta, ColorSat, AdjustLight(48, lightAdjust))})
                Else
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(0), ColorSat, AdjustLight(48, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(1), ColorSat, AdjustLight(54, lightAdjust))})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + ColorHueTopbarDelta(2), ColorSat, AdjustLight(48, lightAdjust))})
                End If
                FrmMain.PanTitle.Background = Brush
                FrmMain.PanTitle.Background.Freeze()
            Else
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue - 21, ColorSat, AdjustLight(53, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.33, .Color = New MyColor().FromHSL2(ColorHue - 7, ColorSat, AdjustLight(47, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.67, .Color = New MyColor().FromHSL2(ColorHue + 7, ColorSat, AdjustLight(47, lightAdjust))})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + 21, ColorSat, AdjustLight(53, lightAdjust))})
                FrmMain.PanTitle.Background = Brush
            End If
#End If
            '主页面背景
            If Setup.Get("UiBackgroundColorful") Then
                Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Dim hue = ThemeService.GetCurrentThemeArgs().Hue
                Dim hue1 = hue - 15
                Dim hue2 = hue + 15
                Dim tone = ThemeService.CurrentTone
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = LabColor.FromLch(GetDarkThemeLight(0.84), tone.C5, hue1)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = LabColor.FromLch(GetDarkThemeLight(0.96), tone.C7, hue)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = LabColor.FromLch(GetDarkThemeLight(0.84), tone.C5, hue2)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = Application.Current.Resources("ColorBrushBackground")
            End If
            FrmMain.PanForm.Background.Freeze()

            ' 通用ContextMenu主题刷新
            RefreshAllContextMenuThemes()
            FrmMain.PanTitleSelect.Children.OfType(Of MyRadioButton)().ToList().ForEach(Sub(btn) btn.RefreshMyRadioButtonColor())
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function
    Friend Function ThemeCheckGold(Optional Code As String = Nothing) As Boolean
        Return False
    End Function
    Friend Function DonateCodeInput() As Boolean?
        Return Nothing
    End Function

#End Region

#Region "更新"

    Public IsCheckingUpdates As Boolean = False
    Public IsUpdateWaitingRestart As Boolean = False
    Public RemoteServer As New UpdatesWrapperModel({
        New UpdatesMirrorChyanModel(),
        New UpdatesRandomModel({
                New UpdatesMinioModel("https://s3.pysio.online/pcl2-ce/", "Pysio"),
                New UpdatesMinioModel("https://staticassets.naids.com/resources/pclce/", "Naids")
            }),
        New UpdatesMinioModel("https://github.com/PCL-Community/PCL2_CE_Server/raw/main/", "GitHub")
    })
    Public ReadOnly Property IsCurrentVersionBeta
        Get
            If VersionBaseName.Contains("beta") Then Return True
            Return Config.Update.UpdateChannel = 1
        End Get
    End Property

    Public Enum VersionStatus
        Latest
        NotLatest
        Unknown
    End Enum
    Public Function GetVersionStatus() As VersionStatus
        Try
            If IsCurrentVersionBeta AndAlso Not Config.Update.UpdateChannel = 1 Then
                Dim isNewerThanStable = RemoteServer.IsLatest(UpdateChannel.stable, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), SemVer.Parse(VersionBaseName), VersionCode)
                Dim isBetaLatest = RemoteServer.IsLatest(UpdateChannel.beta, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), SemVer.Parse(VersionBaseName), VersionCode)
                Return isNewerThanStable AndAlso isBetaLatest
            End If
            Return If(RemoteServer.IsLatest(
                If(IsCurrentVersionBeta, UpdateChannel.beta, UpdateChannel.stable),
                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64),
                SemVer.Parse(VersionBaseName),
                VersionCode), VersionStatus.Latest, VersionStatus.NotLatest)
        Catch ex As Exception
            Log(ex, "无法获取最新版本信息，请检查网络连接", LogLevel.Hint)
            Return VersionStatus.Unknown
        End Try
    End Function

    Public Enum UpdateType
        Silent = 0
        PromptOnly = 1
        DownloadAndPrompt = 2
        UpdateNow = 3
    End Enum

    Public UpdateLoader As LoaderCombo(Of JObject)
    Public Sub UpdateStart(type As UpdateType, Optional receivedKey As String = Nothing, Optional forceValidated As Boolean = False)
        Dim dlTargetPath As String = ExePath + "PCL\Plain Craft Launcher Community Edition.exe"
        RunInNewThread(Sub()
                           Try
                               Dim version = RemoteServer.GetLatestVersion(
                               If(IsCurrentVersionBeta, UpdateChannel.beta, UpdateChannel.stable),
                               If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
                               WriteFile($"{PathTemp}CEUpdateLog.md", version.Changelog)
                               Log($"[Update] 远程最新版本: {version.VersionName}, 当前版本: {VersionBaseName}")
                               If Not SemVer.Parse(version.VersionName) > SemVer.Parse(VersionBaseName) Then Return
                               If type = UpdateType.PromptOnly Then
                                   Log("[Test]")
                                   RunInUi(Sub()
                                       If MyMsgBox($"启动器有新版本可用（｛VersionBaseName｝ -> {version.VersionName}){vbCrLf}是否立即更新？", "启动器更新", "更新", "取消") = 1 Then
                                           FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate)
                                       End If
                                   End Sub)
                                   Return
                               End If
                               '构造步骤加载器
                               Dim loaders As New List(Of LoaderBase)
                               '下载
                               loaders.AddRange(RemoteServer.GetDownloadLoader(
                                                If(IsCurrentVersionBeta, UpdateChannel.beta, UpdateChannel.stable),
                                                If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), dlTargetPath))
                               loaders.Add(New LoaderTask(Of Integer, Integer)("校验更新", Sub()
                                                                                           Dim curHash = GetFileSHA256(dlTargetPath)
                                                                                           If curHash <> version.SHA256 Then
                                                                                               Throw New Exception($"更新文件 SHA256 不正确，应该为 {version.SHA256}，实际为 {curHash}")
                                                                                           End If
                                                                                       End Sub))
                               If type = UpdateType.UpdateNow Then
                                   loaders.Add(New LoaderTask(Of Integer, Integer)("安装更新", Sub() UpdateRestart(True, True)))
                               ElseIf type = UpdateType.Silent Then
                                   loaders.Add(New LoaderTask(Of Integer, Integer)("准备更新", Sub() IsUpdateWaitingRestart = True))
                               ElseIf type = UpdateType.DownloadAndPrompt Then
                                   loaders.Add(New LoaderTask(Of Integer, Integer)("显示按钮", Sub()
                                       IsUpdateWaitingRestart = True
                                       RunInUi(Sub()
                                           FrmMain.BtnExtraUpdateRestart.ToolTip = $"重启 PCL CE 以应用软件更新 ({VersionBaseName} → {version.VersionName})"
                                           FrmMain.BtnExtraUpdateRestart.ShowRefresh()
                                           FrmMain.BtnExtraUpdateRestart.Ribble()
                                       End Sub)
                                   End Sub) With {.Show = False})
                               End If
                               loaders.Add(New LoaderTask(Of Integer, Integer)("刷新设置 UI", Sub()
                                   If FrmSetupUpdate IsNot Nothing Then
                                       RunInUi(Sub() 
                                           FrmSetupUpdate.BtnUpdate.Text = "重启安装"
                                           FrmSetupUpdate.BtnUpdate.IsEnabled = True
                                       End Sub)
                                   End If
                               End Sub) With {.Show = False})
                               '启动
                               UpdateLoader = New LoaderCombo(Of JObject)("启动器更新", loaders)
                               UpdateLoader.Start()
                               If type = UpdateType.UpdateNow Then
                                   LoaderTaskbarAdd(UpdateLoader)
                                   FrmMain.BtnExtraDownload.ShowRefresh()
                                   FrmMain.BtnExtraDownload.Ribble()
                               End If
                           Catch ex As Exception
                               Log(ex, "[Update] 获取启动器更新失败", LogLevel.Debug)
                               If type <> UpdateType.Silent Then Hint("获取启动器更新失败，请检查网络连接", HintType.Critical)
                           End Try
                       End Sub)
    End Sub
    Public Sub UpdateRestart(triggerRestartAndByEnd As Boolean, Optional triggerRestart As Boolean = True)
        Try
            Dim fileName As String = ExePath + "PCL\Plain Craft Launcher Community Edition.exe"
            If Not File.Exists(fileName) Then
                Log("[System] 更新失败：未找到更新文件")
                Exit Sub
            End If
            ' id old new restart
            Dim text As String = $"update {Process.GetCurrentProcess().Id} ""{ExePathWithName}"" ""{fileName}"" {If(triggerRestart, "true", "false")}"
            Log("[System] 更新程序启动，参数：" + text, LogLevel.Normal, "出现错误")
            Process.Start(New ProcessStartInfo(fileName) With {.WindowStyle = ProcessWindowStyle.Hidden, .CreateNoWindow = True, .Arguments = text})
            If triggerRestartAndByEnd Then
                FrmMain.EndProgram(False, isUpdating:=True)
                Log("[System] 已由于更新强制结束程序", LogLevel.Normal, "出现错误")
            End If
        Catch ex As Win32Exception
            Log(ex, "自动更新时触发 Win32 错误，疑似被拦截", LogLevel.Debug, "出现错误")
            If MyMsgBox(String.Format("由于被 Windows 安全中心拦截，或者存在权限问题，导致 PCL 无法更新。{0}请将 PCL 所在文件夹加入白名单，或者手动用 {1}PCL\Plain Craft Launcher Community Edition.exe 替换当前文件！", vbCrLf, ModBase.ExePath), "更新失败", "查看帮助", "确定", "", True, True, False, Nothing, Nothing, Nothing) = 1 Then
                CustomEvent.Raise(CustomEvent.EventType.打开帮助, "启动器/Microsoft Defender 添加排除项.json")
            End If
        End Try
    End Sub
    ''' <summary>
    ''' 确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        Dim LatestPCLPath As String = PathTemp & "CE-Latest.exe"
        Dim target = RemoteServer.GetLatestVersion(UpdateChannel.stable, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64))
        If target Is Nothing Then Throw New Exception("无法获取更新")
        If File.Exists(LatestPCLPath) AndAlso GetFileSHA256(LatestPCLPath) = target.SHA256 Then
            Log("[System] 最新版 PCL 已存在，跳过下载")
            Exit Sub
        End If
        If GetFileSHA256(ExePathWithName) = target.SHA256 Then '正在使用的版本符合要求，直接拿来用
            CopyFile(ExePathWithName, LatestPCLPath)
            Exit Sub
        End If

        Dim loaders = RemoteServer.GetDownloadLoader(UpdateChannel.stable, If(IsArm64System, UpdateArch.arm64, UpdateArch.x64), LatestPCLPath)
        Dim loader As New LoaderCombo(Of Integer)("下载最新稳定版", loaders)
        loader.Start()
        loader.WaitForExit()
    End Sub

#End Region

#Region "联网通知"

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL CE 服务", AddressOf LoadOnlineInfo, Priority:=ThreadPriority.BelowNormal)

    Private Sub LoadOnlineInfo()
        Dim updateDesire = Setup.Get("SystemSystemUpdate")
        Dim AnnouncementDesire = Setup.Get("SystemSystemActivity")
        Select Case updateDesire
            Case 0 '静默更新
                Log("[Update] 更新设置: 自动下载并安装更新")
                If GetVersionStatus() <> VersionStatus.Latest Then
                    UpdateStart(UpdateType.Silent)
                End If
            Case 1 '自动下载，提示更新
                Log("[Update] 更新设置: 自动下载并提示更新")
                UpdateStart(UpdateType.DownloadAndPrompt)
            Case 2 '提示更新
                Log("[Update] 更新设置: 提示更新")
                UpdateStart(UpdateType.PromptOnly)
            Case Else
                Log("[Update] 更新设置: 不自动检查更新")
                Exit Sub
        End Select
        If AnnouncementDesire <= 1 Then
            Dim ShowedAnnounced = Setup.Get("SystemSystemAnnouncement").ToString().Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList()
            Dim ShowAnnounce = RemoteServer.GetAnnouncementList().content.Where(Function(x) Not ShowedAnnounced.Contains(x.id)).ToList()
            Log("[System] 需要展示的公告数量：" + ShowAnnounce.Count.ToString())
            RunInNewThread(Sub()
                               For Each item In ShowAnnounce
                                   Dim SelectedBtn = MyMsgBox(
                                   item.detail,
                                   item.title,
                                   If(item.btn1 Is Nothing, "", item.btn1.text),
                                   If(item.btn2 Is Nothing, "", item.btn2.text),
                                   "关闭",
                                   Button1Action:=Sub()
                                       CustomEvent.Raise(item.btn1.command, item.btn1.command_paramter)
                                                  End Sub,
                                   Button2Action:=Sub()
                                       CustomEvent.Raise(item.btn2.command, item.btn2.command_paramter)
                                                  End Sub
                                    )
                               Next
                           End Sub)
            ShowedAnnounced.AddRange(ShowAnnounce.Select(Function(x) x.id).ToList())
            ShowedAnnounced = ShowedAnnounced.Distinct().ToList()
            Setup.Set("SystemSystemAnnouncement", ShowedAnnounced.Join("|"))
        End If
    End Sub

#End Region

#Region "系统信息"
    Friend CPUName As String = Nothing
    ''' <summary>
    ''' 系统 GPU 信息
    ''' </summary>
    Friend GPUs As New List(Of GPUInfo)
    ''' <summary>
    ''' 已安装物理内存大小，单位 MB
    ''' </summary>
    Friend SystemMemorySize As Long = KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024
    ''' <summary>
    ''' 系统信息描述，例如 Microsoft Windows 11 专业工作站版 10.0.22635.0
    ''' </summary>
    Public OSInfo As String = RuntimeInformation.OSDescription & " " & Environment.OSVersion.Version.ToString()
    Class GPUInfo
        Friend Name As String
        ''' <summary>
        ''' 显存大小，单位 MB
        ''' </summary>
        Friend Memory As Long
        Friend DriverVersion As String
    End Class
    ''' <summary>
    ''' 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    ''' </summary>
    Friend Sub GetSystemInfo()
        'CPU
        Try
            Dim searcher As New ManagementObjectSearcher("root\CIMV2", "SELECT * FROM Win32_Processor")

            For Each queryObj As ManagementObject In searcher.Get()
                CPUName = queryObj("Name").ToString().Trim()
                Exit For '通常只需要第一个CPU的信息
            Next
        Catch ex As Exception
            Log(ex, "获取 CPU 信息时出错", LogLevel.Normal)
        End Try

        'GPU
        Try
            Dim searcher As New ManagementObjectSearcher("root\CIMV2", "SELECT * FROM Win32_VideoController")

            For Each queryObj As ManagementObject In searcher.Get()
                Dim gpuInfo As New GPUInfo

                If queryObj("Name") IsNot Nothing Then
                    gpuInfo.Name = queryObj("Name")
                End If
                If queryObj("AdapterRAM") IsNot Nothing Then
                    Dim ramMB As Long = CLng(queryObj("AdapterRAM")) \ (1024 * 1024)
                    gpuInfo.Memory = ramMB
                End If
                If queryObj("DriverVersion") IsNot Nothing Then
                    gpuInfo.DriverVersion = queryObj("DriverVersion")
                End If

                GPUs.Add(gpuInfo)
            Next

            Log("已获取系统环境信息")
        Catch ex As Exception
            Log(ex, "获取 GPU 信息时出错", LogLevel.Normal)
        End Try
    End Sub
#End Region

#Region "主题"

    ''' <summary>
    ''' 通用的ContextMenu主题刷新方法
    ''' </summary>
    Private Sub RefreshAllContextMenuThemes()
        Try
            ' 注册全局的ContextMenu主题刷新事件处理器
            EventManager.RegisterClassHandler(GetType(ContextMenu), ContextMenu.OpenedEvent, New RoutedEventHandler(AddressOf OnContextMenuOpened))

            ' 刷新当前打开的ContextMenu
            RunInUi(Sub()
                        ' 获取当前应用程序中所有的窗口
                        For Each window As Window In Application.Current.Windows
                            RefreshContextMenusInElement(window)
                        Next
                    End Sub)
        Catch ex As Exception
            Log(ex, "刷新ContextMenu主题时出错", LogLevel.Debug)
        End Try
    End Sub

    ''' <summary>
    ''' ContextMenu打开事件处理器，确保在显示时应用正确主题
    ''' </summary>
    Private Sub OnContextMenuOpened(sender As Object, e As RoutedEventArgs)
        Try
            If TypeOf sender Is ContextMenu Then
                Dim contextMenu As ContextMenu = CType(sender, ContextMenu)
                ' 强制重新应用样式
                contextMenu.ClearValue(FrameworkElement.StyleProperty)
                contextMenu.UpdateDefaultStyle()
            End If
        Catch ex As Exception
            ' 忽略个别错误
        End Try
    End Sub

    ''' <summary>
    ''' 递归刷新元素及其子元素中的ContextMenu
    ''' </summary>
    Private Sub RefreshContextMenusInElement(element As DependencyObject)
        If element Is Nothing Then Return

        Try
            ' 检查当前元素是否有ContextMenu
            If TypeOf element Is FrameworkElement Then
                Dim fe As FrameworkElement = CType(element, FrameworkElement)
                If fe.ContextMenu IsNot Nothing Then
                    ' 强制重新应用样式
                    fe.ContextMenu.ClearValue(FrameworkElement.StyleProperty)
                    fe.ContextMenu.UpdateDefaultStyle()
                End If
            End If

            ' 递归处理子元素
            Dim childrenCount As Integer = VisualTreeHelper.GetChildrenCount(element)
            For i As Integer = 0 To childrenCount - 1
                Dim child As DependencyObject = VisualTreeHelper.GetChild(element, i)
                RefreshContextMenusInElement(child)
            Next
        Catch ex As Exception
            ' 忽略个别元素的错误，继续处理其他元素
        End Try
    End Sub

#End Region

End Module
