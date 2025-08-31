Imports System.Reflection
Imports System.Windows.Media.Effects
Imports PCL.Core.Net
Imports PCL.Core.ProgramSetup
Imports PCL.Core.Utils.OS

Public Class ModSetup
#Region "基础"

    Public Sub New()
        AddHandler SetupService.SetupChanged, AddressOf OnSetupChanged
    End Sub

    Private Sub OnSetupChanged(entry As SetupEntry, oldValue As Object, newValue As Object, gamePath As String)
        Dim method As MethodInfo = GetType(ModSetup).GetMethod(entry.KeyName)
        If method IsNot Nothing Then method.Invoke(Me, {If(newValue, entry.DefaultValue)})
    End Sub

    ''' <summary>
    ''' 改变某个设置项的值。
    ''' </summary>
    Public Sub [Set](key As String, value As Object, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Dim type As Type = entry.DefaultValue.GetType()
        If type = GetType(Boolean) Then
            SetupService.SetBool(entry, value, instance?.Path)
        ElseIf type = GetType(Integer) Then
            SetupService.SetInt32(entry, value, instance?.Path)
        ElseIf type = GetType(String) Then
            SetupService.SetString(entry, value, instance?.Path)
        Else
            Throw New NotSupportedException("请让开发者完善配置系统迁移……")
        End If
    End Sub

    ''' <summary>
    ''' 应用某个设置项的值。
    ''' </summary>
    Public Function Load(key As String, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Dim type As Type = entry.DefaultValue.GetType()
        Dim value
        If type = GetType(Boolean) Then 
            value = SetupService.GetBool(entry, instance?.Path)
        ElseIf type = GetType(Integer) Then 
            value = SetupService.GetInt32(entry, instance?.Path)
        ElseIf type = GetType(String) Then
            value = SetupService.GetString(entry, instance?.Path)
        Else
            Throw New NotSupportedException("请让开发者完善配置系统迁移……")
        End If
        SetupService.RaiseSetupChanged(SetupEntries.ForKeyName(key), value, value, instance?.Path)
        Return value
    End Function

    ''' <summary>
    ''' 获取某个设置项的值。
    ''' </summary>
    Public Function [Get](key As String, Optional instance As McInstance = Nothing)
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Dim type As Type = entry.DefaultValue.GetType()
        If type = GetType(Boolean) Then 
            Return SetupService.GetBool(entry, instance?.Path)
        ElseIf type = GetType(Integer) Then 
            Return SetupService.GetInt32(entry, instance?.Path)
        ElseIf type = GetType(String) Then
            Return SetupService.GetString(entry, instance?.Path)
        Else
            Throw New NotSupportedException("请让开发者完善配置系统迁移……")
        End If
    End Function

    ''' <summary>
    ''' 初始化某个设置项的值。
    ''' </summary>
    Public Sub Reset(key As String, Optional forceReload As Boolean = False, Optional instance As McInstance = Nothing)
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Dim type As Type = entry.DefaultValue.GetType()
        If type = GetType(Boolean) Then 
            SetupService.DeleteBool(entry, instance?.Path)
        ElseIf type = GetType(Integer) Then 
            SetupService.DeleteInt32(entry, instance?.Path)
        ElseIf type = GetType(String) Then
            SetupService.DeleteString(entry, instance?.Path)
        Else
            Throw New NotSupportedException("请让开发者完善配置系统迁移……")
        End If
    End Sub

    ''' <summary>
    ''' 获取某个设置项的默认值。
    ''' </summary>
    Public Function GetDefault(key As String)
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Return entry.DefaultValue
    End Function

    ''' <summary>
    ''' 某个设置项是否从未被设置过。
    ''' </summary>
    Public Function IsUnset(key As String, Optional instance As McInstance = Nothing) As Boolean
        Dim entry As SetupEntry = SetupEntries.ForKeyName(key)
        Return SetupService.IsUnset(entry, instance?.Path)
    End Function

#End Region

#Region "Launch"

    '切换选择
    Public Sub LaunchInstanceSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 版本：" & Value)
        WriteIni(PathMcFolder & "PCL.ini", "Version", If(IsNothing(McInstanceCurrent), "", McInstanceCurrent.Name))
    End Sub
    Public Sub LaunchFolderSelect(Value As String)
        Log("[Setup] 当前选择的 Minecraft 文件夹：" & Value.ToString.Replace("$", ExePath))
        PathMcFolder = Value.ToString.Replace("$", ExePath)
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

    '视频背景
    Public Sub UiAutoPauseVideo(Value As Boolean)
        If Value = False Then
            ModVideoBack.ForcePlay = True
            VideoPlay()
        Else
            ModVideoBack.ForcePlay = False
            If ModVideoBack.IsGaming = True Then VideoPause()
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

    '字体
    Public Sub UiFont(value As String)
        Try
            SetLaunchFont(value)
        Catch ex As Exception
            Log(ex, "字体加载失败", LogLevel.Hint)
        End Try
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
            IsDarkMode = SystemTheme.IsSystemInDarkMode()
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
        Application.Current.Resources("BlurRadius") = Value * 1.0
    End Sub
    Public Sub UiBlurSamplingRate(Value As Integer)
        Application.Current.Resources("BlurSamplingRate") = Value * 0.01
    End Sub
    Public Sub UiBlurType(Value As Integer)
        Application.Current.Resources("BlurType") = CType(Value, KernelType)
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
                    FrmMain.ImageTitleLogo.Source = ExePath & "PCL\Logo.png"
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
        Try
            HttpProxyManager.Instance.CustomProxyAddress = New Uri(value)
        Catch ex As Exception
        End Try
    End Sub

    Public Sub SystemHttpProxyType(value As Integer)
        HttpProxyManager.Instance.Mode = [Enum].Parse(GetType(HttpProxyManager.ProxyMode), value)
    End Sub

    Public Sub SystemHttpProxyCustomUsername(value As String)
        If Not String.IsNullOrEmpty(value) Then
            Dim password As String = Setup.Get("SystemHttpProxyCustomPassword")
            HttpProxyManager.Instance.Credentials = New NetworkCredential(value, password)
        Else
            HttpProxyManager.Instance.Credentials = Nothing
        End If
    End Sub

    Public Sub SystemHttpProxyCustomPassword(value As String)
        Dim username As String = Setup.Get("SystemHttpProxyCustomUsername")
        If Not String.IsNullOrEmpty(username) Then
            HttpProxyManager.Instance.Credentials = New NetworkCredential(username, value)
        Else
            HttpProxyManager.Instance.Credentials = Nothing
        End If
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
