Imports PCL.Core.Network

Public Class ModSetup

    ''' <summary>
    ''' 设置的更新号。
    ''' </summary>
    Public Const VersionSetup As Integer = 1
    ''' <summary>
    ''' 设置列表。
    ''' </summary>
    Private ReadOnly SetupDict As New Dictionary(Of String, SetupEntry) From {
        {"Identify", New SetupEntry("", source:=SetupSource.AppData)},
        {"WindowHeight", New SetupEntry(550)},
        {"WindowWidth", New SetupEntry(900)},
        {"HintDownloadThread", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintNotice", New SetupEntry(0, source:=SetupSource.AppData)},
        {"HintDownload", New SetupEntry(0, source:=SetupSource.AppData)},
        {"HintInstallBack", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintHide", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintHandInstall", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintBuy", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintClearRubbish", New SetupEntry(0, source:=SetupSource.AppData)},
        {"HintUpdateMod", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintCustomCommand", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintCustomWarn", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintMoreAdvancedSetup", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintIndieSetup", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintProfileSelect", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintExportConfig", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintMaxLog", New SetupEntry(False, source:=SetupSource.AppData)},
        {"HintDisableGamePathCheckTip", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemEula", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemCount", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemLaunchCount", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemLastVersionReg", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemHighestSavedBetaVersionReg", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemHighestBetaVersionReg", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemHighestAlphaVersionReg", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"SystemSetupVersionReg", New SetupEntry(VersionSetup, source:=SetupSource.AppData)},
        {"SystemSetupVersionIni", New SetupEntry(VersionSetup)},
        {"SystemDebugMode", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemDebugAnim", New SetupEntry(9, source:=SetupSource.AppData)},
        {"SystemDebugDelay", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemDebugSkipCopy", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemSystemCache", New SetupEntry("", source:=SetupSource.AppData)},
        {"SystemSystemUpdate", New SetupEntry(0)},
        {"SystemSystemUpdateBranch", New SetupEntry(If(VersionBaseName.Contains("beta"), 1, 0))},
        {"SystemSystemActivity", New SetupEntry(0)},
        {"SystemSystemAnnouncement", New SetupEntry("", source:=SetupSource.AppData)},
        {"SystemHttpProxy", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"SystemUseDefaultProxy", New SetupEntry(True, source:=SetupSource.AppData)},
        {"SystemDisableHardwareAcceleration", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemTelemetry", New SetupEntry(False, source:=SetupSource.AppData)},
        {"SystemMirrorChyanKey", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"SystemMaxLog", New SetupEntry(13, source:=SetupSource.AppData)},
        {"CacheExportConfig", New SetupEntry("", source:=SetupSource.AppData)},
        {"CacheSavedPageUrl", New SetupEntry("", source:=SetupSource.AppData)},
        {"CacheSavedPageInstance", New SetupEntry("", source:=SetupSource.AppData)},
        {"CacheDownloadFolder", New SetupEntry("", source:=SetupSource.AppData)},
        {"ToolDownloadCustomUserAgent", New SetupEntry("", source:=SetupSource.AppData)},
        {"CacheJavaListVersion", New SetupEntry(0, source:=SetupSource.AppData)},
        {"CacheAuthUuid", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"CacheAuthName", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"CacheAuthUsername", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"CacheAuthPass", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"CacheAuthServerServer", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"CompFavorites", New SetupEntry("[]", source:=SetupSource.AppData)},
        {"LaunchInstanceSelect", New SetupEntry("")},
        {"LaunchFolderSelect", New SetupEntry("")},
        {"LaunchFolders", New SetupEntry("", source:=SetupSource.AppData)},
        {"LaunchArgumentTitle", New SetupEntry("")},
        {"LaunchArgumentInfo", New SetupEntry("PCL")},
        {"LaunchArgumentJavaSelect", New SetupEntry("", source:=SetupSource.AppData)},
        {"LaunchArgumentJavaUser", New SetupEntry("[]", source:=SetupSource.AppData)},
        {"LaunchArgumentIndie", New SetupEntry(0)},
        {"LaunchArgumentIndieV2", New SetupEntry(4)},
        {"LaunchArgumentVisible", New SetupEntry(5, source:=SetupSource.AppData)},
        {"LaunchArgumentPriority", New SetupEntry(1, source:=SetupSource.AppData)},
        {"LaunchArgumentWindowWidth", New SetupEntry(854)},
        {"LaunchArgumentWindowHeight", New SetupEntry(480)},
        {"LaunchArgumentWindowType", New SetupEntry(1)},
        {"LaunchPreferredIpStack", New SetupEntry(1, source:=SetupSource.AppData)},
        {"LaunchArgumentRam", New SetupEntry(False, source:=SetupSource.AppData)},
        {"LaunchAdvanceJvm", New SetupEntry("-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true")},
        {"LaunchAdvanceGame", New SetupEntry("")},
        {"LaunchAdvanceRun", New SetupEntry("")},
        {"LaunchAdvanceRunWait", New SetupEntry(True)},
        {"LaunchAdvanceDisableJLW", New SetupEntry(False)},
        {"LaunchAdvanceDisableRW", New SetupEntry(False)},
        {"LaunchAdvanceGraphicCard", New SetupEntry(True, source:=SetupSource.AppData)},
        {"LaunchAdvanceNoJavaw", New SetupEntry(False, source:=SetupSource.AppData)},
        {"LaunchRamType", New SetupEntry(0)},
        {"LaunchRamCustom", New SetupEntry(15)},
        {"LaunchUuid", New SetupEntry(String.Empty, source:=SetupSource.AppData)},
        {"ToolFixAuthlib", New SetupEntry(True, source:=SetupSource.AppData)},
        {"LinkEula", New SetupEntry(False, source:=SetupSource.AppData)},
        {"LinkAnnounceCache", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"LinkAnnounceCacheVer", New SetupEntry(0, source:=SetupSource.AppData)},
        {"LinkRelayType", New SetupEntry(0, source:=SetupSource.AppData)},
        {"LinkServerType", New SetupEntry(1, source:=SetupSource.AppData)},
        {"LinkProxyType", New SetupEntry(1, source:=SetupSource.AppData)},
        {"LinkRelayServer", New SetupEntry("", source:=SetupSource.AppData)},
        {"LinkNaidRefreshToken", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"LinkNaidRefreshExpiresAt", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"LinkFirstTimeNetTest", New SetupEntry(True, source:=SetupSource.AppData)},
        {"LoginLegacyName", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"LoginMsJson", New SetupEntry("{}", source:=SetupSource.AppData, encoded:=True)}, '{UserName: OAuthToken, ...}
        {"LoginMsAuthType", New SetupEntry(1, source:=SetupSource.AppData)},
        {"ToolHelpChinese", New SetupEntry(True, source:=SetupSource.AppData)},
        {"ToolDownloadThread", New SetupEntry(63, source:=SetupSource.AppData)},
        {"ToolDownloadSpeed", New SetupEntry(42, source:=SetupSource.AppData)},
        {"ToolDownloadSource", New SetupEntry(1, source:=SetupSource.AppData)},
        {"ToolDownloadVersion", New SetupEntry(1, source:=SetupSource.AppData)},
        {"ToolDownloadTranslate", New SetupEntry(0, source:=SetupSource.AppData)},
        {"ToolDownloadTranslateV2", New SetupEntry(1, source:=SetupSource.AppData)},
        {"ToolDownloadIgnoreQuilt", New SetupEntry(True, source:=SetupSource.AppData)},
        {"ToolDownloadClipboard", New SetupEntry(False, source:=SetupSource.AppData)},
        {"ToolDownloadCert", New SetupEntry(True, source:=SetupSource.AppData)},
        {"ToolDownloadMod", New SetupEntry(1, source:=SetupSource.AppData)},
        {"ToolModLocalNameStyle", New SetupEntry(0, source:=SetupSource.AppData)},
        {"ToolUpdateAlpha", New SetupEntry(0, source:=SetupSource.AppData, encoded:=True)},
        {"ToolUpdateRelease", New SetupEntry(False, source:=SetupSource.AppData)},
        {"ToolUpdateSnapshot", New SetupEntry(False, source:=SetupSource.AppData)},
        {"ToolUpdateReleaseLast", New SetupEntry("", source:=SetupSource.AppData)},
        {"ToolUpdateSnapshotLast", New SetupEntry("", source:=SetupSource.AppData)},
        {"ToolDownloadAutoSelectVersion", New SetupEntry(True, source:=SetupSource.AppData)},
        {"UiLauncherTransparent", New SetupEntry(600)}, '避免与 PCL1 设置冲突（UiLauncherOpacity）
        {"UiLauncherHue", New SetupEntry(180)},
        {"UiLauncherSat", New SetupEntry(80)},
        {"UiLauncherDelta", New SetupEntry(90)},
        {"UiLauncherLight", New SetupEntry(20)},
        {"UiLauncherTheme", New SetupEntry(0)},
        {"UiLauncherThemeGold", New SetupEntry("", source:=SetupSource.AppData, encoded:=True)},
        {"UiLauncherThemeHide", New SetupEntry("0|1|2|3|4", source:=SetupSource.AppData, encoded:=True)},
        {"UiLauncherThemeHide2", New SetupEntry("0|1|2|3|4", source:=SetupSource.AppData, encoded:=True)},
        {"UiLauncherLogo", New SetupEntry(True)},
        {"UiLauncherCEHint", New SetupEntry(True, source:=SetupSource.AppData)},
        {"UiLauncherCEHintCount", New SetupEntry(0, source:=SetupSource.AppData)},
        {"UiBlur", New SetupEntry(False)},
        {"UiBlurValue", New SetupEntry(16)},
        {"UiBackgroundColorful", New SetupEntry(True)},
        {"UiBackgroundOpacity", New SetupEntry(1000)},
        {"UiBackgroundBlur", New SetupEntry(0)},
        {"UiBackgroundSuit", New SetupEntry(0)},
        {"UiCustomType", New SetupEntry(0)},
        {"UiCustomPreset", New SetupEntry(0)},
        {"UiCustomNet", New SetupEntry("")},
        {"UiDarkMode", New SetupEntry(2, source:=SetupSource.AppData)},
        {"UiDarkColor", New SetupEntry(1, source:=SetupSource.AppData)},
        {"UiLightColor", New SetupEntry(1, source:=SetupSource.AppData)},
        {"UiLockWindowSize", New SetupEntry(False, source:=SetupSource.AppData)},
        {"UiLogoType", New SetupEntry(1)},
        {"UiLogoText", New SetupEntry("")},
        {"UiLogoLeft", New SetupEntry(False)},
        {"UiMusicVolume", New SetupEntry(500)},
        {"UiMusicStop", New SetupEntry(False)},
        {"UiMusicStart", New SetupEntry(False)},
        {"UiMusicRandom", New SetupEntry(True)},
        {"UiMusicSMTC", New SetupEntry(True)},
        {"UiMusicAuto", New SetupEntry(True)},
        {"UiHiddenPageDownload", New SetupEntry(False)},
        {"UiHiddenPageLink", New SetupEntry(False)},
        {"UiHiddenPageSetup", New SetupEntry(False)},
        {"UiHiddenPageOther", New SetupEntry(False)},
        {"UiHiddenFunctionSelect", New SetupEntry(False)},
        {"UiHiddenFunctionModUpdate", New SetupEntry(False)},
        {"UiHiddenFunctionHidden", New SetupEntry(False)},
        {"UiHiddenSetupLaunch", New SetupEntry(False)},
        {"UiHiddenSetupUi", New SetupEntry(False)},
        {"UiHiddenSetupSystem", New SetupEntry(False)},
        {"UiHiddenOtherHelp", New SetupEntry(False)},
        {"UiHiddenOtherFeedback", New SetupEntry(False)},
        {"UiHiddenOtherVote", New SetupEntry(False)},
        {"UiHiddenOtherAbout", New SetupEntry(False)},
        {"UiHiddenOtherTest", New SetupEntry(False)},
        {"UiHiddenVersionEdit", New SetupEntry(False)},
        {"UiHiddenVersionExport", New SetupEntry(False)},
        {"UiHiddenVersionSave", New SetupEntry(False)},
        {"UiHiddenVersionScreenshot", New SetupEntry(False)},
        {"UiHiddenVersionMod", New SetupEntry(False)},
        {"UiHiddenVersionResourcePack", New SetupEntry(False)},
        {"UiHiddenVersionShader", New SetupEntry(False)},
        {"UiHiddenVersionSchematic", New SetupEntry(False)},
        {"UiSchematicFirstTimeHintShown", New SetupEntry(False, source:=SetupSource.AppData)},
        {"UiAniFPS", New SetupEntry(59, source:=SetupSource.AppData)},
        {"UiFont", New SetupEntry("")},
        {"VersionAdvanceJvm", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionAdvanceGame", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionAdvanceAssets", New SetupEntry(0, source:=SetupSource.Instance)},
        {"VersionAdvanceAssetsV2", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionAdvanceJava", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionAdvanceDisableJlw", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionAdvanceRun", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionAdvanceRunWait", New SetupEntry(True, source:=SetupSource.Instance)},
        {"VersionAdvanceDisableJLW", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionAdvanceUseProxyV2", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionAdvanceDisableRW", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionRamType", New SetupEntry(2, source:=SetupSource.Instance)},
        {"VersionRamCustom", New SetupEntry(15, source:=SetupSource.Instance)},
        {"VersionRamOptimize", New SetupEntry(0, source:=SetupSource.Instance)},
        {"VersionArgumentTitle", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionArgumentTitleEmpty", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionArgumentInfo", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionArgumentIndie", New SetupEntry(-1, source:=SetupSource.Instance)},
        {"VersionArgumentIndieV2", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionArgumentJavaSelect", New SetupEntry("使用全局设置", source:=SetupSource.Instance)},
        {"VersionServerEnter", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionServerLoginRequire", New SetupEntry(0, source:=SetupSource.Instance)},
        {"VersionServerAuthRegister", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionServerAuthName", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionServerAuthServer", New SetupEntry("", source:=SetupSource.Instance)},
        {"VersionServerLoginLock", New SetupEntry(False, source:=SetupSource.Instance)},
        {"VersionLaunchCount", New SetupEntry(0, source:=SetupSource.Instance)}}

#Region "Register 存储"

    Private _LocalRegisterData As LocalJsonFileConfig = Nothing
    Private ReadOnly Property LocalRegisterData As LocalJsonFileConfig
        Get
            Dim ConfigFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & $"\.{RegFolder}\Config.json"
            Try
                If _LocalRegisterData Is Nothing Then _LocalRegisterData = New LocalJsonFileConfig(ConfigFilePath)
            Catch ex As Exception
                Rename(ConfigFilePath, $"{ConfigFilePath}.{GetStringMD5(DateTime.Now.ToString())}.bak")
                _LocalRegisterData = New LocalJsonFileConfig(ConfigFilePath)
                MsgBox("读取本地配置文件失败，可能文件损坏。" & vbCrLf &
                       $"请将 {_LocalRegisterData} 文件删除，并使用备份配置文件 {_LocalRegisterData}.bak",
                        MsgBoxStyle.Critical)
                FormMain.EndProgramForce(ProcessReturnValues.Fail)
            End Try
            Return _LocalRegisterData
        End Get
    End Property


    Public Class LocalJsonFileConfig
        Private ReadOnly _ConfigData As JObject
        Private _ConfigFilePath As String

        Public Sub New(JsonFilePath As String)
            _ConfigFilePath = JsonFilePath
            If File.Exists(JsonFilePath) Then
                Try
                    Dim JsonText = ReadFile(JsonFilePath)
                    _ConfigData = JObject.Parse(JsonText)
                Catch ex As Exception
                    Throw
                End Try
            Else
                _ConfigData = New JObject()
            End If
        End Sub

        Private Sub Save()
            Dim tempPath = _ConfigFilePath & ".tmp"
            Dim backupPath = _ConfigFilePath & ".bak"
            Try
                ' 先写入临时文件
                WriteFile(tempPath, _ConfigData.ToString())
                ' 原子化替换文件
                If File.Exists(_ConfigFilePath) Then
                    File.Replace(tempPath, _ConfigFilePath, backupPath)
                Else
                    File.Move(tempPath, _ConfigFilePath)
                End If
            Catch ex As Exception
                If File.Exists(tempPath) Then File.Delete(tempPath)
                Throw
            End Try
        End Sub

        Private ReadOnly _OpLock As New Object()
        Public Sub [Set](key As String, value As String)
            SyncLock _OpLock
                _ConfigData(key) = value
                Save()
            End SyncLock
        End Sub

        Public Function [Get](Key As String) As String
            If _ConfigData.ContainsKey(Key) Then
                Return _ConfigData(Key)
            Else
                Return Nothing
            End If
        End Function

        Public Sub Remove(key As String)
            SyncLock _OpLock
                _ConfigData.Remove(key)
                Save()
            End SyncLock
        End Sub

        Public Function Contains(key As String) As Boolean
            Return _ConfigData.ContainsKey(key)
        End Function

        Public ReadOnly Property RawJObject As JObject
            Get
                Return _ConfigData
            End Get
        End Property
    End Class
#End Region

#Region "基础"

    Private Enum SetupSource
        Normal
        AppData
        Instance
    End Enum
    Private Class SetupEntry

        Public Encoded As Boolean
        Public DefaultValue
        Public DefaultValueEncoded
        Public Value
        Public Source As SetupSource

        ''' <summary>
        ''' 加载状态：0/未读取  1/已读取未处理  2/已处理
        ''' 我也不知道当年写这坨的时候为啥没用 Enum……
        ''' </summary>
        Public State As Byte = 0
        Public Type As Type

        Public Sub New(value, Optional source = SetupSource.Normal, Optional encoded = False)
            Try
                Me.DefaultValue = value
                Me.Encoded = encoded
                Me.Value = value
                Me.Source = source
                Me.Type = If(value, New Object).GetType
                Me.DefaultValueEncoded = If(encoded, SecretEncrypt(value), value)
            Catch ex As Exception
                Log(ex, "初始化 SetupEntry 失败", LogLevel.Feedback) '#5095 的 fallback
            End Try
        End Sub

    End Class

    ''' <summary>
    ''' 改变某个设置项的值。
    ''' </summary>
    Public Sub [Set](key As String, value As Object, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        [Set](key, value, SetupDict(key), forceReload, instance)
    End Sub
    Private Sub [Set](key As String, value As Object, e As SetupEntry, forceReload As Boolean, instance As McInstance)
        Try

            value = CTypeDynamic(value, e.Type)
            If e.State = 2 Then
                '如果已应用，且值相同，则无需再次更改
                If e.Value = value AndAlso Not forceReload Then Return
            Else
                '如果未应用，则直接更改并应用
                If e.Source <> SetupSource.Instance Then e.State = 2
            End If
            '设置新值
            e.Value = value
            '写入值
            If e.Encoded Then
                Try
                    If value Is Nothing Then value = ""
                    value = SecretEncrypt(value)
                Catch ex As Exception
                    Log(ex, "加密设置失败：" & key, LogLevel.Developer)
                End Try
            End If
            Select Case e.Source
                Case SetupSource.Normal
                    WriteIni("Setup", key, value)
                Case SetupSource.AppData
                    LocalRegisterData.Set(key, value)
                Case SetupSource.Instance
                    If instance Is Nothing Then Throw New Exception($"更改版本设置 {key} 时未提供目标版本")
                    WriteIni(instance.Path & "PCL\Setup.ini", key, value)
            End Select
            '应用
            '例如 VersionServerLoginRequire 要求在设置之后再引发事件
            Dim Method As Reflection.MethodInfo = GetType(ModSetup).GetMethod(key)
            If Method IsNot Nothing Then Method.Invoke(Me, {value})

        Catch ex As Exception
            Log(ex, "设置设置项时出错（" & key & ", " & value & "）", LogLevel.Feedback)
        End Try
    End Sub

    ''' <summary>
    ''' 应用某个设置项的值。
    ''' </summary>
    Public Function Load(key As String, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        Return Load(key, SetupDict(key), forceReload, instance)
    End Function
    Private Function Load(key As String, e As SetupEntry, forceReload As Boolean, instance As McInstance)
        '如果已经应用过，则什么也不干
        If e.State = 2 AndAlso Not forceReload Then Return e.Value
        '读取，应用并设置状态
        Read(key, e, instance)
        If e.Source <> SetupSource.Instance Then e.State = 2
        Dim Method As Reflection.MethodInfo = GetType(ModSetup).GetMethod(key)
        If Method IsNot Nothing Then Method.Invoke(Me, {e.Value})
        Return e.Value
    End Function

    ''' <summary>
    ''' 获取某个设置项的值。
    ''' </summary>
    Public Function [Get](key As String, Optional instance As McInstance = Nothing)
        If Not SetupDict.ContainsKey(key) Then Throw New KeyNotFoundException("未找到设置项：" & key) With {.Source = key}
        Return [Get](key, SetupDict(key), instance)
    End Function
    Private Function [Get](key As String, e As SetupEntry, instance As McInstance)
        '获取强制值
        Dim Force As String = ForceValue(key)
        If Force IsNot Nothing Then
            e.Value = CTypeDynamic(Force, e.Type)
            e.State = 1
        End If
        '如果尚未读取过，则读取
        If e.State = 0 Then
            Read(key, e, instance)
            If e.Source <> SetupSource.Instance Then e.State = 1
        End If
        '返回现在的值
        Return e.Value
    End Function

    ''' <summary>
    ''' 初始化某个设置项的值。
    ''' </summary>
    Public Sub Reset(key As String, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        Dim E As SetupEntry = SetupDict(key)
        [Set](key, E.DefaultValue, E, forceReload, instance)
        Select Case SetupDict(key).Source
            Case SetupSource.Normal
                DeleteIniKey("Setup", key)
            Case SetupSource.AppData
                DeleteReg(key)
                LocalRegisterData.Remove(key)
            Case Else 'SetupSource.Instance
                If instance Is Nothing Then Throw New Exception($"重置实例设置 {key} 时未提供目标实例")
                DeleteIniKey(instance.Path & "PCL\Setup.ini", key)
        End Select
    End Sub
    ''' <summary>
    ''' 获取某个设置项的默认值。
    ''' </summary>
    Public Function GetDefault(key As String) As String
        Return SetupDict(key).DefaultValue
    End Function
    ''' <summary>
    ''' 某个设置项是否从未被设置过。
    ''' </summary>
    Public Function IsUnset(key As String, Optional instance As McInstance = Nothing) As Boolean
        Select Case SetupDict(key).Source
            Case SetupSource.Normal
                Return Not HasIniKey("Setup", key)
            Case SetupSource.AppData
                Return Not HasReg(key) AndAlso Not LocalRegisterData.Contains(key)
            Case Else 'SetupSource.Instance
                If instance Is Nothing Then Throw New Exception($"判断实例设置 {key} 是否存在时未提供目标实例")
                Return Not HasIniKey(instance.Path & "PCL\Setup.ini", key)
        End Select
    End Function

    ''' <summary>
    ''' 读取设置。
    ''' </summary>
    Private Sub Read(key As String, ByRef e As SetupEntry, instance As McInstance)
        Try
            If Not e.State = 0 Then Return
            Dim SourceValue As String = Nothing '先用 String 储存，避免类型转换
            Select Case e.Source
                Case SetupSource.Normal
                    SourceValue = ReadIni("Setup", key, e.DefaultValueEncoded)
                Case SetupSource.AppData
                    Dim OldSourceData = ReadReg(key)
                    If Not String.IsNullOrWhiteSpace(OldSourceData) Then
                        If LocalRegisterData.Contains(key) Then '如果本地配置文件中已经存在该项，则不覆盖
                            OldSourceData = LocalRegisterData.Get(key)
                        Else
                            If e.Encoded Then OldSourceData = SecretEncrypt(SecretDecrptyOld(OldSourceData))
                            LocalRegisterData.Set(key, OldSourceData)
                            DeleteReg(key)
                        End If
                        SourceValue = OldSourceData
                    Else
                        SourceValue = LocalRegisterData.Get(key)
                    End If
                    If String.IsNullOrEmpty(SourceValue) Then
                        SourceValue = e.DefaultValueEncoded
                    End If
                Case SetupSource.Instance
                    If instance Is Nothing Then
                        Throw New Exception("读取实例设置 " & key & " 时未提供目标实例")
                    Else
                        SourceValue = ReadIni(instance.Path & "PCL\Setup.ini", key, e.DefaultValueEncoded)
                    End If
            End Select
            If e.Encoded Then
                If SourceValue.Equals(e.DefaultValueEncoded) Then
                    SourceValue = e.DefaultValue
                Else
                    Try
                        SourceValue = SecretDecrypt(SourceValue)
                    Catch ex As Exception
                        Log(ex, "解密设置失败：" & key, LogLevel.Developer)
                        SourceValue = e.DefaultValue
                        Setup.Set(key, e.DefaultValue, True)
                    End Try
                End If
            End If
            e.Value = CTypeDynamic(SourceValue, e.Type)
        Catch ex As Exception
            Log(ex, "读取设置失败：" & key, LogLevel.Hint)
            e.Value = CTypeDynamic(e.DefaultValue, e.Type)
        End Try
    End Sub

    '对部分设置强制赋值
    Private Function ForceValue(key As String) As String
#If RELEASE Or BETA Then
        If Key = "UiLauncherTheme" Then Return "0"
#End If
        Return Nothing
    End Function

#End Region

#Region "Launch"

    '切换选择
    Public Sub LaunchInstanceSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 版本：" & Value)
        WriteIni(PathMcFolder & "PCL.ini", "Version", If(IsNothing(McInstanceCurrent), "", McInstanceCurrent.Name))
    End Sub
    Public Sub LaunchFolderSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 文件夹：" & Value.ToString.Replace("$", Path))
        PathMcFolder = Value.ToString.Replace("$", Path)
    End Sub

    '游戏内存
    Public Sub LaunchRamType(Type As Integer)
        If FrmSetupLaunch Is Nothing Then Return
        FrmSetupLaunch.RamType(Type)
    End Sub

#End Region

#Region "Tool"

    Public Sub ToolDownloadThread(Value As Integer)
        NetTaskThreadLimit = Value + 1
    End Sub
    Public Sub ToolDownloadCert(Value As Boolean)
        ServicePointManager.ServerCertificateValidationCallback =
        Function(Sender, Certificate, Chain, Failure)
            Dim Request As HttpWebRequest = TryCast(Sender, HttpWebRequest)
            If Failure = Net.Security.SslPolicyErrors.None Then Return True '已通过验证
            '基于 #3018 和 #5879，只在访问正版登录 API 时跳过证书验证
            Log($"[System] 未通过 SSL 证书验证（{Failure}），提供的证书为 {Certificate?.Subject}，URL：{Request?.Address}", LogLevel.Debug)
            If Request Is Nothing Then
                Return Not Value
            ElseIf Request.Address.Host.Contains("xboxlive") OrElse Request.Address.Host.Contains("minecraftservices") Then
                Return Not Value '根据设置决定是否忽略错误
            Else
                Return False
            End If
        End Function
    End Sub
    Public Sub ToolDownloadSpeed(Value As Integer)
        If Value <= 14 Then
            NetTaskSpeedLimitHigh = (Value + 1) * 0.1 * 1024 * 1024L
        ElseIf Value <= 31 Then
            NetTaskSpeedLimitHigh = (Value - 11) * 0.5 * 1024 * 1024L
        ElseIf Value <= 41 Then
            NetTaskSpeedLimitHigh = (Value - 21) * 1024 * 1024L
        Else
            NetTaskSpeedLimitHigh = -1
        End If
    End Sub

#End Region

#Region "UI"

    '启动器
    Public Sub UiLauncherTransparent(Value As Integer)
        FrmMain.Opacity = Value / 1000 + 0.4
    End Sub
    Public Sub UiLauncherTheme(Value As Integer)
        ThemeRefresh(Value)
    End Sub
    Public Sub UiBackgroundColorful(Value As Boolean)
        ThemeRefresh()
    End Sub

    Public Sub UiLockWindowSize(Value As Boolean)
        If Value Then
            FrmMain.RemoveResizer()
        Else
            FrmMain.AddResizer()
        End If
    End Sub

    '背景图片
    Public Sub UiBackgroundOpacity(Value As Integer)
        FrmMain.ImgBack.Opacity = Value / 1000
    End Sub
    Public Sub UiBackgroundBlur(Value As Integer)
        If Value = 0 Then
            FrmMain.ImgBack.Effect = Nothing
        Else
            FrmMain.ImgBack.Effect = New Effects.BlurEffect With {.Radius = Value + 1}
        End If
        FrmMain.ImgBack.Margin = New Thickness(-(Value + 1) / 1.8)
    End Sub
    Public Sub UiBackgroundSuit(Value As Integer)
        If IsNothing(FrmMain.ImgBack.Background) Then Return
        Dim Width As Double = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
        Dim Height As Double = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
        If Value = 0 Then
            '智能：当图片较小时平铺，较大时适应
            If Width < FrmMain.PanMain.ActualWidth / 2 AndAlso Height < FrmMain.PanMain.ActualHeight / 2 Then
                Value = 4 '平铺
            Else
                Value = 2 '适应
            End If
        End If
        CType(FrmMain.ImgBack.Background, ImageBrush).TileMode = TileMode.None
        CType(FrmMain.ImgBack.Background, ImageBrush).Viewport = New Rect(0, 0, 1, 1)
        CType(FrmMain.ImgBack.Background, ImageBrush).ViewportUnits = BrushMappingMode.RelativeToBoundingBox
        Select Case Value
            Case 1 '居中
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Center
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Center
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 2 '适应
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.UniformToFill
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 3 '拉伸
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.Fill
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 4 '平铺
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Stretch
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Stretch
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                CType(FrmMain.ImgBack.Background, ImageBrush).TileMode = TileMode.Tile
                CType(FrmMain.ImgBack.Background, ImageBrush).Viewport = New Rect(0, 0, CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width, CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height)
                CType(FrmMain.ImgBack.Background, ImageBrush).ViewportUnits = BrushMappingMode.Absolute
                FrmMain.ImgBack.Width = Double.NaN
                FrmMain.ImgBack.Height = Double.NaN
            Case 5 '左上
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 6 '右上
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Top
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 7 '左下
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Left
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
            Case 8 '右下
                FrmMain.ImgBack.HorizontalAlignment = HorizontalAlignment.Right
                FrmMain.ImgBack.VerticalAlignment = VerticalAlignment.Bottom
                CType(FrmMain.ImgBack.Background, ImageBrush).Stretch = Stretch.None
                FrmMain.ImgBack.Width = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Width
                FrmMain.ImgBack.Height = CType(FrmMain.ImgBack.Background, ImageBrush).ImageSource.Height
        End Select
    End Sub

    '主页
    Public Sub UiCustomType(Value As Integer)
        If FrmSetupUI Is Nothing Then Return
        Select Case Value
            Case 0 '无
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                FrmSetupUI.HintCustom.EventType = ""
                FrmSetupUI.HintCustom.EventData = ""
            Case 2 '联网
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupUI.HintCustom.Visibility = Visibility.Visible
                FrmSetupUI.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupUI.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                FrmSetupUI.HintCustom.EventType = "打开网页"
                FrmSetupUI.HintCustom.EventData = "https://github.com/Meloong-Git/PCL/discussions/2528"
            Case 3 '预设
                FrmSetupUI.PanCustomPreset.Visibility = Visibility.Visible
                FrmSetupUI.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupUI.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupUI.HintCustomWarn.Visibility = Visibility.Collapsed
        End Select
        FrmSetupUI.CardCustom.TriggerForceResize()
    End Sub
    '颜色模式
    Public Sub UiDarkMode(Value As Integer)
        If Value = 0 Then
            IsDarkMode = False
        ElseIf Value = 1 Then
            IsDarkMode = True
        Else
            IsDarkMode = IsSystemInDarkMode()
        End If
        ThemeRefresh()
    End Sub
    '高级材质
    Public Sub UiBlur(Value As Boolean)
        FrmSetupUI.PanBlurValue.Visibility = If(Value, Visibility.Visible, Visibility.Collapsed)
        If Value Then
            UiBlurValue(Setup.Get("UiBlurValue"))
        Else
            UiBlurValue(0)
        End If
    End Sub
    Public Sub UiBlurValue(Value As Integer)
        Application.Current.Resources("BlurValue") = CType(Value, Double)
    End Sub
    '顶部栏
    Public Sub UiLogoType(Value As Integer)
        Select Case Value
            Case 0 '无
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.CELogo.Visibility = Visibility.Collapsed
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Visible
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
            Case 1 '默认
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Visible
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.CELogo.Visibility = Visibility.Visible
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
            Case 2 '文本
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Visible
                FrmMain.ImageTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.CELogo.Visibility = Visibility.Visible
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Visible
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Collapsed
                End If
                Setup.Load("UiLogoText", True)
            Case 3 '图片
                FrmMain.ShapeTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.LabTitleLogo.Visibility = Visibility.Collapsed
                FrmMain.ImageTitleLogo.Visibility = Visibility.Visible
                FrmMain.CELogo.Visibility = Visibility.Visible
                If Not IsNothing(FrmSetupUI) Then
                    FrmSetupUI.CheckLogoLeft.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoText.Visibility = Visibility.Collapsed
                    FrmSetupUI.PanLogoChange.Visibility = Visibility.Visible
                End If
                Try
                    FrmMain.ImageTitleLogo.Source = Path & "PCL\Logo.png"
                Catch ex As Exception
                    FrmMain.ImageTitleLogo.Source = Nothing
                    Log(ex, "显示标题栏图片失败", LogLevel.Msgbox)
                End Try
        End Select
        Setup.Load("UiLogoLeft", True)
        If Not IsNothing(FrmSetupUI) Then FrmSetupUI.CardLogo.TriggerForceResize()
    End Sub
    Public Sub UiLogoText(Value As String)
        FrmMain.LabTitleLogo.Text = Value
    End Sub
    Public Sub UiLogoLeft(Value As Boolean)
        FrmMain.PanTitleMain.ColumnDefinitions(0).Width = New GridLength(If(Value AndAlso (Setup.Get("UiLogoType") = 0), 0, 1), GridUnitType.Star)
    End Sub

    '功能隐藏
    Public Sub UiHiddenPageLink(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageDownload(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageSetup(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenPageOther(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionSelect(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionModUpdate(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenFunctionHidden(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupLaunch(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupUi(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenSetupSystem(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherHelp(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherFeedback(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherVote(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherAbout(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenOtherTest(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionEdit(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionExport(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionSave(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionScreenshot(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionMod(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionResourcePack(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionShader(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub
    Public Sub UiHiddenVersionSchematic(Value As Boolean)
        PageSetupUI.HiddenRefresh()
    End Sub


#End Region

#Region "System"

    '调试选项
    Public Sub SystemDebugMode(Value As Boolean)
        ModeDebug = Value
    End Sub
    Public Sub SystemDebugAnim(Value As Integer)
        AniSpeed = If(Value >= 30, 200, MathClamp(Value * 0.1 + 0.1, 0.1, 3))
    End Sub

    Public Sub SystemHttpProxy(value As String)
        HttpProxyManager.Instance.ProxyAddress = value
    End Sub

    Public Sub SystemUseDefaultProxy(value As Boolean)
        HttpProxyManager.Instance.DisableProxy = value
    End Sub

#End Region

#Region "Version"

    '游戏内存
    Public Sub VersionRamType(Type As Integer)
        If FrmInstanceSetup Is Nothing Then Return
        FrmInstanceSetup.RamType(Type)
    End Sub

    '服务器
    Public Sub VersionServerLogin(Type As Integer)
        If FrmInstanceSetup Is Nothing Then Return
        '为第三方登录清空缓存以更新描述
        WriteIni(PathMcFolder & "PCL.ini", "InstanceCache", "")
        If PageInstanceLeft.Instance Is Nothing Then Return
        PageInstanceLeft.Instance = New McInstance(PageInstanceLeft.Instance.Name).Load()
        LoaderFolderRun(McInstanceListLoader, PathMcFolder, LoaderFolderRunType.ForceRun, MaxDepth:=1, ExtraPath:="versions\")
    End Sub

#End Region

End Class
