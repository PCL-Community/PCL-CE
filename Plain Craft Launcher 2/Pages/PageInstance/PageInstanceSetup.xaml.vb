Imports PCL.Core.App
Imports PCL.Core.Minecraft
Imports PCL.Core.Minecraft.Yggdrasil
Imports PCL.Core.UI
Imports PCL.Core.Utils.OS
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Minecraft.Java.UserPreference
Imports PCL.Core.IO
Imports System.Text.Json

Public Class PageInstanceSetup

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupSystem_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()
        RefreshRam(False)

        '由于各个实例不同，每次都需要重新加载
        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        '内存自动刷新
        Dim timer As New Threading.DispatcherTimer With {.Interval = New TimeSpan(0, 0, 0, 1)}
        AddHandler timer.Tick, AddressOf RefreshRam
        timer.Start()

    End Sub
    Public Sub Reload()
        Try

            '启动参数
            TextArgumentTitle.Text = Setup.Get("VersionArgumentTitle", instance:=PageInstanceLeft.Instance)
            CheckArgumentTitleEmpty.Checked = Setup.Get("VersionArgumentTitleEmpty", instance:=PageInstanceLeft.Instance)
            TextArgumentInfo.Text = Setup.Get("VersionArgumentInfo", instance:=PageInstanceLeft.Instance)
            Dim _unused = PageInstanceLeft.Instance.PathIndie '触发自动判定
            ComboArgumentIndieV2.SelectedIndex = If(Setup.Get("VersionArgumentIndieV2", instance:=PageInstanceLeft.Instance), 0, 1)
            CheckArgumentTitleEmpty.Visibility = If(TextArgumentTitle.Text.Length > 0, Visibility.Collapsed, Visibility.Visible)
            TextArgumentTitle.HintText = If(CheckArgumentTitleEmpty.Checked, "默认", "跟随全局设置")
            RefreshJavaComboBox()

            '游戏内存
            CType(FindName("RadioRamType" & Setup.Load("VersionRamType", instance:=PageInstanceLeft.Instance)), MyRadioBox).Checked = True
            SliderRamCustom.Value = Setup.Get("VersionRamCustom", instance:=PageInstanceLeft.Instance)
            ComboRamOptimize.SelectedIndex = Setup.Get("VersionRamOptimize", instance:=PageInstanceLeft.Instance)

            '服务器
            TextServerEnter.Text = Setup.Get("VersionServerEnter", instance:=PageInstanceLeft.Instance)
            ComboServerLoginRequire.SelectedIndex = Setup.Get("VersionServerLoginRequire", instance:=PageInstanceLeft.Instance)
            ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex
            ServerLogin(ComboServerLoginRequire.SelectedIndex)
            TextServerAuthServer.Text = Setup.Get("VersionServerAuthServer", instance:=PageInstanceLeft.Instance)
            TextServerAuthName.Text = Setup.Get("VersionServerAuthName", instance:=PageInstanceLeft.Instance)
            TextServerAuthRegister.Text = Setup.Get("VersionServerAuthRegister", instance:=PageInstanceLeft.Instance)

            '高级设置
            ComboAdvanceRenderer.SelectedIndex = Setup.Get("VersionAdvanceRenderer", instance:=PageInstanceLeft.Instance)
            TextAdvanceJvm.Text = Setup.Get("VersionAdvanceJvm", instance:=PageInstanceLeft.Instance)
            TextAdvanceGame.Text = Setup.Get("VersionAdvanceGame", instance:=PageInstanceLeft.Instance)
            TextAdvanceRun.Text = Setup.Get("VersionAdvanceRun", instance:=PageInstanceLeft.Instance)
            CheckAdvanceRunWait.Checked = Setup.Get("VersionAdvanceRunWait", instance:=PageInstanceLeft.Instance)
            If Setup.Get("VersionAdvanceAssets", instance:=PageInstanceLeft.Instance) = 2 Then
                Log("[Setup] 已迁移老版本的关闭文件校验设置")
                Setup.Reset("VersionAdvanceAssets", instance:=PageInstanceLeft.Instance)
                Setup.Set("VersionAdvanceAssetsV2", True, instance:=PageInstanceLeft.Instance)
            End If
            CheckAdvanceAssetsV2.Checked = Setup.Get("VersionAdvanceAssetsV2", instance:=PageInstanceLeft.Instance)
            CheckAdvanceUseProxyV2.Checked = Setup.Get("VersionAdvanceUseProxyV2", instance:=PageInstanceLeft.Instance)
            CheckAdvanceJava.Checked = Setup.Get("VersionAdvanceJava", instance:=PageInstanceLeft.Instance)
            If IsArm64System Then
                CheckAdvanceDisableJLW.Checked = True
                CheckAdvanceDisableJLW.IsEnabled = False
                CheckAdvanceDisableJLW.ToolTip = "在启动游戏时不使用 Java Wrapper 进行包装。&#xa;由于系统为 ARM64 架构，Java Wrapper 已被强制禁用。"
            Else
                CheckAdvanceDisableJLW.Checked = Setup.Get("VersionAdvanceDisableJLW", instance:=PageInstanceLeft.Instance)
            End If

        Catch ex As Exception
            Log(ex, "重载实例独立设置时出错", LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            If Not Setup.Get("VersionServerLoginLock", PageInstanceLeft.Instance) Then
                Setup.Reset("VersionServerLoginRequire", instance:=PageInstanceLeft.Instance)
                Setup.Reset("VersionServerAuthServer", instance:=PageInstanceLeft.Instance)
                Setup.Reset("VersionServerAuthRegister", instance:=PageInstanceLeft.Instance)
                Setup.Reset("VersionServerAuthName", instance:=PageInstanceLeft.Instance)
            End If
            Setup.Reset("VersionServerEnter", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionArgumentTitle", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionArgumentInfo", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionArgumentIndieV2", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionRamType", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionRamCustom", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionRamOptimize", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceJvm", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceGame", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceAssets", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceAssetsV2", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceJava", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceDisableJlw", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceRun", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceRunWait", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceDisableJLW", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceUseProxyV2", instance:=PageInstanceLeft.Instance)
            Setup.Reset("VersionAdvanceRenderer", instance:=PageInstanceLeft.Instance)

            Setup.Reset("VersionArgumentJavaSelect", instance:=PageInstanceLeft.Instance)

            Log("[Setup] 已初始化实例独立设置")
            Hint("已初始化实例独立设置！", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化实例独立设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioRamType0.Check, RadioRamType1.Check, RadioRamType2.Check
        Dim gotCfg = sender.Tag.ToString.Split("/")
        If AniControlEnabled = 0 Then Setup.Set(gotCfg(0), Integer.Parse(gotCfg(1)), instance:=PageInstanceLeft.Instance)
    End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextServerEnter.ValidatedTextChanged, TextArgumentInfo.ValidatedTextChanged, TextAdvanceGame.ValidatedTextChanged, TextAdvanceJvm.ValidatedTextChanged, TextServerAuthName.ValidatedTextChanged, TextServerAuthRegister.ValidatedTextChanged, TextServerAuthServer.ValidatedTextChanged, TextArgumentTitle.TextChanged, TextAdvanceRun.ValidatedTextChanged
        If AniControlEnabled = 0 Then
            '#3194，不能删减 /
            'Dim HandledText As String = sender.Text
            'If sender.Tag = "VersionServerAuthServer" OrElse sender.Tag = "VersionServerAuthRegister" Then HandledText = HandledText.TrimEnd("/")
            Setup.Set(sender.Tag, sender.Text, instance:=PageInstanceLeft.Instance)
        End If
    End Sub
    Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles SliderRamCustom.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value, instance:=PageInstanceLeft.Instance)
    End Sub
    Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles ComboRamOptimize.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex, instance:=PageInstanceLeft.Instance)
    End Sub
    Private Shared Sub CheckBoxLikeComboChange(sender As MyComboBox, e As Object) Handles ComboArgumentIndieV2.SelectionChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex = 0, instance:=PageInstanceLeft.Instance)
    End Sub
    Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckArgumentTitleEmpty.Change, CheckAdvanceRunWait.Change, CheckAdvanceAssetsV2.Change, CheckAdvanceJava.Change, CheckAdvanceDisableJLW.Change, CheckAdvanceUseProxyV2.Change, CheckAdvanceDisableRW.Change
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked, instance:=PageInstanceLeft.Instance)
    End Sub

#Region "游戏内存"

    Public Sub RamType(Type As Integer)
        If SliderRamCustom Is Nothing Then Return
        SliderRamCustom.IsEnabled = (Type = 1)
    End Sub

    ''' <summary>
    ''' 刷新 UI 上的 RAM 显示。
    ''' </summary>
    Public Sub RefreshRam(ShowAnim As Boolean)
        If LabRamGame Is Nothing OrElse LabRamUsed Is Nothing OrElse FrmMain.PageCurrent <> FormMain.PageType.InstanceSetup OrElse FrmInstanceLeft.PageID <> FormMain.PageSubType.VersionSetup Then Return
        '获取内存情况
        Dim RamGame As Double = Math.Round(GetRam(PageInstanceLeft.Instance), 5)
        Dim phyRam = KernelInterop.GetPhysicalMemoryBytes()
        Dim RamTotal As Double = Math.Round(phyRam.Total / 1024 / 1024 / 1024, 1)
        Dim RamAvailable As Double = Math.Round(phyRam.Available / 1024 / 1024 / 1024, 1)
        Dim RamGameActual As Double = Math.Round(Math.Min(RamGame, RamAvailable), 5)
        Dim RamUsed As Double = Math.Round(RamTotal - RamAvailable, 5)
        Dim RamEmpty As Double = Math.Round(MathClamp(RamTotal - RamUsed - RamGame, 0, 1000), 1)
        '设置最大可用内存
        If RamTotal <= 1.5 Then
            SliderRamCustom.MaxValue = Math.Max(Math.Floor((RamTotal - 0.3) / 0.1), 1)
        ElseIf RamTotal <= 8 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 1.5) / 0.5) + 12
        ElseIf RamTotal <= 16 Then
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 8) / 1) + 25
        Else
            SliderRamCustom.MaxValue = Math.Floor((RamTotal - 16) / 2) + 33
        End If
        '设置文本
        LabRamGame.Text = If(RamGame = Math.Floor(RamGame), RamGame & ".0", RamGame) & " GB" &
                          If(RamGame <> RamGameActual, " (可用 " & If(RamGameActual = Math.Floor(RamGameActual), RamGameActual & ".0", RamGameActual) & " GB)", "")
        LabRamUsed.Text = If(RamUsed = Math.Floor(RamUsed), RamUsed & ".0", RamUsed) & " GB"
        LabRamTotal.Text = " / " & If(RamTotal = Math.Floor(RamTotal), RamTotal & ".0", RamTotal) & " GB"
        LabRamWarn.Visibility = If(RamGame = 1 AndAlso Not IsGameSet64BitJava(PageInstanceLeft.Instance) AndAlso Not Is32BitSystem AndAlso Javas.ExistAnyJava(), Visibility.Visible, Visibility.Collapsed)
        HintRamTooHigh.Visibility = If(RamGame / RamTotal > 0.75, Visibility.Visible, Visibility.Collapsed)
        If ShowAnim Then
            '宽度动画
            AniStart({
                AaGridLengthWidth(ColumnRamUsed, RamUsed - ColumnRamUsed.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamGame, RamGameActual - ColumnRamGame.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong)),
                AaGridLengthWidth(ColumnRamEmpty, RamEmpty - ColumnRamEmpty.Width.Value, 800,, New AniEaseOutFluent(AniEasePower.Strong))
            }, "VersionSetup Ram Grid")
        Else
            '宽度设置
            ColumnRamUsed.Width = New GridLength(RamUsed, GridUnitType.Star)
            ColumnRamGame.Width = New GridLength(RamGameActual, GridUnitType.Star)
            ColumnRamEmpty.Width = New GridLength(RamEmpty, GridUnitType.Star)
        End If
    End Sub
    Private Sub RefreshRam() Handles SliderRamCustom.Change, RadioRamType0.Check, RadioRamType1.Check, RadioRamType2.Check
        RefreshRam(True)
    End Sub

    Private RamTextLeft As Integer = 2, RamTextRight As Integer = 1
    ''' <summary>
    ''' 刷新 UI 上的文本位置。
    ''' </summary>
    Private Sub RefreshRamText() Handles RectRamGame.SizeChanged, RectRamEmpty.SizeChanged, LabRamGame.SizeChanged
        '获取宽度信息
        Dim RectUsedWidth = RectRamUsed.ActualWidth
        Dim TotalWidth = PanRamDisplay.ActualWidth
        Dim LabGameWidth = LabRamGame.ActualWidth, LabUsedWidth = LabRamUsed.ActualWidth, LabTotalWidth = LabRamTotal.ActualWidth
        Dim LabGameTitleWidth = LabRamGameTitle.ActualWidth, LabUsedTitleWidth = LabRamUsedTitle.ActualWidth
        '左侧
        Dim Left As Integer
        If RectUsedWidth - 30 < LabUsedWidth OrElse RectUsedWidth - 30 < LabUsedTitleWidth Then
            '全写不下了
            Left = 0
        ElseIf RectUsedWidth - 25 < (LabUsedWidth + LabTotalWidth) Then
            '显示不下完整数据
            Left = 1
        Else
            '正常
            Left = 2
        End If
        If RamTextLeft <> Left Then
            RamTextLeft = Left
            Select Case Left
                Case 0
                    AniStart({
                            AaOpacity(LabRamUsed, -LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, -LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft")
                Case 1
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, -LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft")
                Case 2
                    AniStart({
                            AaOpacity(LabRamUsed, 1 - LabRamUsed.Opacity, 100),
                            AaOpacity(LabRamTotal, 1 - LabRamTotal.Opacity, 100),
                            AaOpacity(LabRamUsedTitle, 0.7 - LabRamUsedTitle.Opacity, 100)
                        }, "VersionSetup Ram TextLeft")
            End Select
        End If
        '右侧
        Dim Right As Integer
        If TotalWidth < LabGameWidth + 2 + RectUsedWidth OrElse TotalWidth < LabGameTitleWidth + 2 + RectUsedWidth Then
            '挤到最右边
            Right = 0
        Else
            '正常情况
            Right = 1
        End If
        If Right = 0 Then
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("VersionSetup Ram TextRight")) Then
                '需要动画
                AniStart({
                        AaX(LabRamGame, TotalWidth - LabGameWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, TotalWidth - LabGameTitleWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "VersionSetup Ram TextRight")
            Else
                '不需要动画
                LabRamGame.Margin = New Thickness(TotalWidth - LabGameWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(TotalWidth - LabGameTitleWidth, 0, 0, 5)
            End If
        Else
            If AniControlEnabled = 0 AndAlso (RamTextRight <> Right OrElse AniIsRun("VersionSetup Ram TextRight")) Then
                '需要动画
                AniStart({
                        AaX(LabRamGame, 2 + RectUsedWidth - LabRamGame.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak)),
                        AaX(LabRamGameTitle, 2 + RectUsedWidth - LabRamGameTitle.Margin.Left, 100,, New AniEaseOutFluent(AniEasePower.Weak))
                }, "VersionSetup Ram TextRight")
            Else
                '不需要动画
                LabRamGame.Margin = New Thickness(2 + RectUsedWidth, 3, 0, 0)
                LabRamGameTitle.Margin = New Thickness(2 + RectUsedWidth, 0, 0, 5)
            End If
        End If
        RamTextRight = Right
    End Sub

    ''' <summary>
    ''' 获取当前设置的 RAM 值。单位为 GB。
    ''' </summary>
    Public Shared Function GetRam(Version As McInstance, Optional Is32BitJava As Boolean? = Nothing) As Double
        '跟随全局设置
        If Setup.Get("VersionRamType", instance:=Version) = 2 Then
            Return PageSetupLaunch.GetRam(Version, True, Is32BitJava)
        End If

        '------------------------------------------
        ' 修改下方代码时需要一并修改 PageSetupLaunch
        '------------------------------------------

        '使用当前实例的设置
        Dim RamGive As Double
        If Setup.Get("VersionRamType", instance:=Version) = 0 Then
            '自动配置
            Dim RamAvailable As Double = Math.Round(KernelInterop.GetAvailablePhysicalMemoryBytes() / 1024 / 1024 / 1024 * 10) / 10
            '确定需求的内存值
            Dim RamMininum As Double '无论如何也需要保证的最低限度内存
            Dim RamTarget1 As Double '估计能勉强带动了的内存
            Dim RamTarget2 As Double '估计没啥问题了的内存
            Dim RamTarget3 As Double '安装过多附加组件需要的内存
            If Version IsNot Nothing AndAlso Not Version.IsLoaded Then Version.Load()
            If Version IsNot Nothing AndAlso Version.Modable Then
                '可安装 Mod 的实例
                Dim ModDir As New DirectoryInfo(Version.PathIndie & "mods\")
                Dim ModCount As Integer = If(ModDir.Exists, ModDir.GetFiles.Length, 0)
                RamMininum = 0.5 + ModCount / 150
                RamTarget1 = 1.5 + ModCount / 90
                RamTarget2 = 2.7 + ModCount / 50
                RamTarget3 = 4.5 + ModCount / 25
            ElseIf Version IsNot Nothing AndAlso Version.Info.HasOptiFine Then
                'OptiFine 实例
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 3
                RamTarget3 = 5
            Else
                '普通实例
                RamMininum = 0.5
                RamTarget1 = 1.5
                RamTarget2 = 2.5
                RamTarget3 = 4
            End If
            Dim RamDelta As Double
            '预分配内存，阶段一，0 ~ T1，100%
            RamDelta = RamTarget1
            RamGive += Math.Min(RamAvailable, RamDelta)
            RamAvailable -= RamDelta
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段二，T1 ~ T2，70%
            RamDelta = RamTarget2 - RamTarget1
            RamGive += Math.Min(RamAvailable * 0.7, RamDelta)
            RamAvailable -= RamDelta / 0.7
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段三，T2 ~ T3，40%
            RamDelta = RamTarget3 - RamTarget2
            RamGive += Math.Min(RamAvailable * 0.4, RamDelta)
            RamAvailable -= RamDelta / 0.4
            If RamAvailable < 0.1 Then GoTo PreFin
            '预分配内存，阶段四，T3 ~ T3 * 2，15%
            RamDelta = RamTarget3
            RamGive += Math.Min(RamAvailable * 0.15, RamDelta)
            RamAvailable -= RamDelta / 0.15
            If RamAvailable < 0.1 Then GoTo PreFin
PreFin:
            '不低于最低值
            RamGive = Math.Round(Math.Max(RamGive, RamMininum), 1)
        Else
            '手动配置
            Dim Value As Integer = Setup.Get("VersionRamCustom", instance:=Version)
            If Value <= 12 Then
                RamGive = Value * 0.1 + 0.3
            ElseIf Value <= 25 Then
                RamGive = (Value - 12) * 0.5 + 1.5
            ElseIf Value <= 33 Then
                RamGive = (Value - 25) * 1 + 8
            Else
                RamGive = (Value - 33) * 2 + 16
            End If
        End If
        '若使用 32 位 Java，则限制为 1G
        If If(Is32BitJava, Not IsGameSet64BitJava(PageInstanceLeft.Instance)) Then RamGive = Math.Min(1, RamGive)
        Return RamGive
    End Function

#End Region

#Region "服务器"

    '全局
    Private ComboServerLoginLast As Integer
    Private Sub ComboServerLogin_Changed() Handles ComboServerLoginRequire.SelectionChanged, TextServerAuthServer.ValidatedTextChanged, TextServerAuthRegister.ValidatedTextChanged
        If AniControlEnabled <> 0 Then Exit Sub
        ServerLogin(ComboServerLoginRequire.SelectedIndex)
        '检查是否输入正确，正确才触发设置改变
        If TextServerAuthServer.IsValidated Then
            BtnServerAuthLock.IsEnabled = True
        Else
            BtnServerAuthLock.IsEnabled = False
        End If
        If (ComboServerLoginRequire.SelectedIndex = 2 OrElse ComboServerLoginRequire.SelectedIndex = 3) AndAlso Not TextServerAuthServer.IsValidated Then Exit Sub
        '检查结果是否发生改变，未改变则不触发设置改变
        If ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex Then Exit Sub
        '触发
        ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex
        ComboChange(ComboServerLoginRequire, Nothing)
    End Sub
    Private Sub TextServerAuthServer_MouseLeave() Handles TextServerAuthServer.LostFocus
        If String.IsNullOrWhiteSpace(TextServerAuthServer.Text) Then Exit Sub
        Hint("正在检查 API 地址信息，请稍后！")

        Dispatcher.BeginInvoke(Async Function() As Task
            Dim originAddress = TextServerAuthServer.Text
            Try
                TextServerAuthServer.Text = Await ApiLocation.TryRequestAsync(TextServerAuthServer.Text)
                Hint("检查 API 地址信息成功，验证服务器地址已更新", HintType.Finish)
            Catch ex As Exception
                Log(ex,"检查验证服务器地址失败",LogLevel.Hint)
                TextServerAuthServer.Text = originAddress
            Finally
                ComboServerLoginLast = ComboServerLoginRequire.SelectedIndex
                ComboChange(ComboServerLoginRequire, Nothing)

            End Try
        End Function)
    End Sub
    Public Sub ServerLogin(Type As Integer)
        LabServerAuthName.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthName.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        LabServerAuthRegister.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthRegister.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        LabServerAuthServer.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        TextServerAuthServer.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        BtnServerAuthLittle.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        BtnServerNewProfile.Visibility = If(Type = 2 OrElse Type = 3, Visibility.Visible, Visibility.Collapsed)
        If Type = 0 OrElse Type = 1 Then
            BtnServerAuthLock.Visibility = Visibility.Collapsed
        Else
            BtnServerAuthLock.Visibility = Visibility.Visible
        End If
        If Setup.Get("VersionServerLoginLock", PageInstanceLeft.Instance) Then
            HintServerLoginLock.Visibility = Visibility.Visible
            ComboServerLoginRequire.IsEnabled = False
            TextServerAuthServer.IsEnabled = False
            TextServerAuthName.IsEnabled = False
            TextServerAuthRegister.IsEnabled = False
            BtnServerAuthLittle.IsEnabled = False
        Else
            HintServerLoginLock.Visibility = Visibility.Collapsed
            ComboServerLoginRequire.IsEnabled = True
            TextServerAuthServer.IsEnabled = True
            TextServerAuthName.IsEnabled = True
            TextServerAuthRegister.IsEnabled = True
            BtnServerAuthLittle.IsEnabled = True
        End If
        CardServer.TriggerForceResize()
        '避免正版验证和离线验证出现此提示
        If Not (Type = 2 OrElse Type = 3) Then
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed
            LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed
            ' 如果开头为 http:// 给予警告
        ElseIf TextServerAuthServer.Text.StartsWithF("https://") Then
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Visible
            LabServerAuthServerSecurityCL.Visibility = Visibility.Visible
        ElseIf TextServerAuthServer.Text.StartsWithF("http://") Then
            LabServerAuthServerSecurity.Visibility = Visibility.Visible
            LabServerAuthServerSecurityCL.Visibility = Visibility.Visible
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed
        Else
            LabServerAuthServerSecurity.Visibility = Visibility.Collapsed
            LabServerAuthServerSecurityVerify.Visibility = Visibility.Collapsed
            LabServerAuthServerSecurityCL.Visibility = Visibility.Collapsed
        End If
    End Sub

    'LittleSkin
    Private Sub BtnServerAuthLittle_Click(sender As Object, e As EventArgs) Handles BtnServerAuthLittle.Click
        If TextServerAuthServer.Text <> "" AndAlso TextServerAuthServer.Text <> "https://littleskin.cn/api/yggdrasil" AndAlso
            MyMsgBox("即将把第三方登录设置覆盖为 LittleSkin 登录。" & vbCrLf & "除非你是服主，或者服主要求你这样做，否则请不要继续。" & vbCrLf & vbCrLf & "是否确实需要覆盖当前设置？",
                     "设置覆盖确认", "继续", "取消") = 2 Then Return
        TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil"
        TextServerAuthRegister.Text = "https://littleskin.cn/auth/register"
        TextServerAuthName.Text = "LittleSkin 登录"
    End Sub

    '锁定设置
    Private Sub BtnServerAuthLock_Click() Handles BtnServerAuthLock.Click
        If MyMsgBox($"你正在选择锁定此实例的验证方式。锁定之后，将无法再更改此实例的验证方式要求，启动此实例将必须使用指定的验证方式。{vbCrLf}此功能可能会帮助一些服主吧。{vbCrLf}是否继续？", "锁定验证方式确认", "确定", "取消", IsWarn:=True) = 1 Then
            Setup.Set("VersionServerLoginLock", True, instance:=PageInstanceLeft.Instance)
            Reload()
        End If
    End Sub

    '跳转新建档案
    Private Sub BtnServerNewProfile_Click() Handles BtnServerNewProfile.Click
        FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
        PageLoginAuth.DraggedAuthServer = TextServerAuthServer.Text
        RunInNewThread(Sub()
                           Thread.Sleep(150)
                           RunInUi(Sub() FrmLaunchLeft.RefreshPage(True, McLoginType.Auth))
                       End Sub)
    End Sub

    Private Shared Sub TextServerEnter_Change(sender As MyTextBox, e As Object) Handles TextServerEnter.LostFocus
        sender.Text = sender.Text.Replace("：", ":")
    End Sub
#End Region

#Region "Java 选择"

    '刷新 Java 下拉框显示
    Public Sub RefreshJavaComboBox()
        If ComboArgumentJava Is Nothing Then Return

        ' 获取实例的 Java 偏好（已兼容新旧格式）
        Dim preference = GetInstanceJavaPreference(PageInstanceLeft.Instance)

        ' === 1. 初始化固定选项（使用类型安全的 Tag） ===
        ComboArgumentJava.Items.Clear()

        ' 选项 0: 跟随全局设置
        ComboArgumentJava.Items.Add(New MyComboBoxItem With {
            .Content = "跟随全局设置",
            .Tag = New UseGlobalPreference()
        })

        ' 选项 1: 自动选择
        ComboArgumentJava.Items.Add(New MyComboBoxItem With {
            .Content = "自动选择合适的 Java",
            .Tag = New AutoSelect()  ' Nothing 表示自动选择
        })

        ' 选项 2: 相对路径选项
        Dim relativePathItem As MyComboBoxItem
        If TypeOf preference Is UseRelativePath Then
            Dim relPref = DirectCast(preference, UseRelativePath)
            Dim absPath = IO.Path.GetFullPath(IO.Path.Combine(Basics.ExecutableDirectory, relPref.RelativePath))
            Dim javaEntry = Javas.Get(absPath)

            If Files.IsPathWithinDirectory(absPath, Basics.ExecutableDirectory) AndAlso
                javaEntry IsNot Nothing AndAlso
                javaEntry.IsEnabled Then
                ' 有效路径：显示具体 Java 信息
                relativePathItem = New MyComboBoxItem With {
                    .Content = $"启动器目录下的 Java | {javaEntry}",
                    .Tag = New UseRelativePath(relPref.RelativePath),
                    .ToolTip = $"相对路径: {relPref.RelativePath}{vbCrLf}解析路径: {absPath}"
                }
            Else
                ' 无效路径：提示用户重新选择
                relativePathItem = New MyComboBoxItem With {
                    .Content = "选择启动器目录下的 Java（当前路径无效）",
                    .Tag = New UseRelativePath(relPref.RelativePath),
                    .ToolTip = $"无效路径: {absPath}{vbCrLf}点击此项重新选择有效 Java"
                }
            End If
        Else
            ' 未配置相对路径：使用默认模板
            relativePathItem = New MyComboBoxItem With {
                .Content = "选择启动器目录下的 Java",
                .Tag = New UseRelativePath("jre\bin\java.exe"),
                .ToolTip = "将选择相对于实例目录的 Java 路径"
            }
        End If
        ComboArgumentJava.Items.Add(relativePathItem)

        ' === 2. 添加所有可用 Java 运行时 ===
        Dim selectedItem As MyComboBoxItem = Nothing
        Try
            For Each curJava In Javas.GetSortedJavaList()
                Dim item = New MyComboBoxItem With {
                .Content = curJava.ToString(),
                .ToolTip = $"路径: {curJava.Installation.JavaExePath}{vbCrLf}版本: {curJava.Installation.Version}{vbCrLf}来源: {curJava.Source}",
                .Tag = curJava
            }
                ToolTipService.SetInitialShowDelay(item, 300)
                ToolTipService.SetBetweenShowDelay(item, 100)
                ComboArgumentJava.Items.Add(item)
            Next
        Catch ex As Exception
            Setup.Set("VersionArgumentJavaSelect", "使用全局设置", instance:=PageInstanceLeft.Instance)
            Log(ex, "更新实例设置 Java 下拉框失败", LogLevel.Feedback)
            ComboArgumentJava.Items.Clear()
            ComboArgumentJava.Items.Add(New MyComboBoxItem With {
                .Content = "列表加载失败，请重试",
                .IsEnabled = False
            })
            ComboArgumentJava.SelectedIndex = 0
            RefreshRam(True)
            Return
        End Try

        ' === 3. 根据当前偏好设置选中项（优先使用新格式 preference） ===
        If preference Is Nothing Then
            ' 自动选择
            selectedItem = TryCast(ComboArgumentJava.Items(1), MyComboBoxItem)
        ElseIf TypeOf preference Is UseGlobalPreference Then
            selectedItem = TryCast(ComboArgumentJava.Items(0), MyComboBoxItem)
        ElseIf TypeOf preference Is UseRelativePath Then
            selectedItem = TryCast(ComboArgumentJava.Items(2), MyComboBoxItem)
        ElseIf TypeOf preference Is ExistingJava Then
            Dim existPref = DirectCast(preference, ExistingJava)
            ' 在 Java 列表中查找匹配项（从索引 3 开始）
            For i As Integer = 3 To ComboArgumentJava.Items.Count - 1
                Dim item = TryCast(ComboArgumentJava.Items(i), MyComboBoxItem)
                If item IsNot Nothing AndAlso TypeOf item.Tag Is JavaEntry Then
                    Dim javaEntry = DirectCast(item.Tag, JavaEntry)
                    If String.Equals(javaEntry.Installation.JavaExePath, existPref.JavaExePath, StringComparison.OrdinalIgnoreCase) Then
                        selectedItem = item
                        Exit For
                    End If
                End If
            Next
        End If

        ' 降级处理：无匹配项时回退到自动选择
        If selectedItem Is Nothing AndAlso ComboArgumentJava.Items.Count > 1 Then
            selectedItem = TryCast(ComboArgumentJava.Items(1), MyComboBoxItem)
        End If

        ' 设置选中项
        If selectedItem IsNot Nothing Then
            ComboArgumentJava.SelectedItem = selectedItem
        End If

        ' === 4. 无可用 Java 时的降级处理 ===
        If Not Javas.ExistAnyJava() AndAlso ComboArgumentJava.Items.Count <= 3 Then
            ComboArgumentJava.Items.Clear()
            Dim noJavaItem = New MyComboBoxItem With {
                .Content = "未检测到可用的 Java 运行时",
                .ToolTip = "请在设置中手动指定 Java 路径，或点击'扫描'按钮重新检测",
                .IsEnabled = False
            }
            ComboArgumentJava.Items.Add(noJavaItem)
            ComboArgumentJava.SelectedItem = noJavaItem
        End If

        ' === 5. 刷新关联控件 ===
        RefreshRam(True)
    End Sub

    ' 阻止在无效状态下展开下拉框
    Private Sub ComboArgumentJava_DropDownOpened(sender As Object, e As EventArgs) Handles ComboArgumentJava.DropDownOpened
        If ComboArgumentJava.SelectedItem Is Nothing Then
            ComboArgumentJava.IsDropDownOpen = False
            Return
        End If

        Dim firstItem = TryCast(ComboArgumentJava.Items(0), MyComboBoxItem)
        If firstItem IsNot Nothing AndAlso (firstItem.Content = "未检测到可用的 Java 运行时" OrElse firstItem.Content = "列表加载失败，请重试") Then
            ComboArgumentJava.IsDropDownOpen = False
        End If
    End Sub

    ' 下拉框选择更改处理（保存新格式配置）
    Private Sub JavaSelectionUpdate(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentJava.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If ComboArgumentJava.SelectedItem Is Nothing Then Return

        Dim selectedItem = TryCast(ComboArgumentJava.SelectedItem, MyComboBoxItem)
        If selectedItem Is Nothing OrElse selectedItem.Tag Is Nothing AndAlso selectedItem.Content <> "自动选择合适的 Java" Then Return

        Dim preference As JavaPreference = Nothing
        Dim logMessage As String = ""

        ' 根据 Tag 类型生成偏好对象
        If selectedItem.Tag Is Nothing Then
            ' 自动选择：存储空字符串
            preference = New AutoSelect()
            logMessage = "[Java] 修改实例 Java 选择设置：自动选择"
        ElseIf TypeOf selectedItem.Tag Is UseGlobalPreference Then
            preference = New UseGlobalPreference()
            logMessage = "[Java] 修改实例 Java 选择设置：跟随全局设置"
        ElseIf TypeOf selectedItem.Tag Is UseRelativePath Then
            ' 相对路径：需要用户选择实际文件
            Dim ret = SystemDialogs.SelectFile("Java 程序(java.exe)|java.exe", "选择 Java 程序", Basics.ExecutableDirectory)
            If String.IsNullOrWhiteSpace(ret) Then
                ' 用户取消，不保存配置，保持原选择
                Return
            End If

            ret = IO.Path.GetFullPath(ret)
            Dim relativePath = IO.Path.GetRelativePath(Basics.ExecutableDirectory, ret)

            ' 验证路径是否在启动器目录内
            If Not Files.IsPathWithinDirectory(relativePath, Basics.ExecutableDirectory) Then
                Hint("超出路径允许范围，请选择启动器文件夹或其子文件夹下的文件", HintType.Critical)
                Return
            End If

            preference = New UseRelativePath(relativePath)
            logMessage = $"[Java] 修改实例 Java 选择设置：相对路径 | {relativePath}"
        ElseIf TypeOf selectedItem.Tag Is JavaEntry Then
            Dim javaEntry = DirectCast(selectedItem.Tag, JavaEntry)
            preference = New ExistingJava(javaEntry.Installation.JavaExePath)
            logMessage = $"[Java] 修改实例 Java 选择设置：{javaEntry}"
        End If

        ' 保存配置
        Dim json = JsonSerializer.Serialize(preference)
        Config.Instance.SelectedJava(PageInstanceLeft.Instance.PathInstance) = json


        Log(logMessage)
        RefreshRam(True)
    End Sub

#End Region

#Region "其他设置"

    '版本隔离警告
    Private IsReverting As Boolean = False
    Private Sub ComboArgumentIndieV2_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboArgumentIndieV2.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If IsReverting Then Return
        If MyMsgBox("调整版本隔离后，你可能得把游戏存档、Mod 等文件手动迁移到新的游戏文件夹中。" & vbCrLf &
                    "如果修改后发现存档消失，把这项设置改回来就能恢复。" & vbCrLf &
                    "如果你不会迁移存档，不建议修改这项设置！",
                    "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
            IsReverting = True
            ComboArgumentIndieV2.SelectedItem = e.RemovedItems(0)
            IsReverting = False
        End If
    End Sub

    '游戏窗口
    Private Sub CheckArgumentTitleEmpty_Change(sender As MyCheckBox, e As Object) Handles CheckArgumentTitleEmpty.Change
        TextArgumentTitle.HintText = If(CheckArgumentTitleEmpty.Checked, "默认", "跟随全局设置")
    End Sub
    Private Sub TextArgumentTitle_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextArgumentTitle.TextChanged
        CheckArgumentTitleEmpty.Visibility = If(TextArgumentTitle.Text.Length > 0, Visibility.Collapsed, Visibility.Visible)
    End Sub

#End Region

#Region "高级设置"

    Private Sub TextAdvanceRun_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextAdvanceRun.TextChanged
        CheckAdvanceRunWait.Visibility = If(TextAdvanceRun.Text = "", Visibility.Collapsed, Visibility.Visible)
    End Sub

    Private Sub ComboAdvanceRenderer_SelectionChanged(sender As MyComboBox, e As Object) Handles ComboAdvanceRenderer.SelectionChanged
        If AniControlEnabled <> 0 Then Return
        If Not Setup.Get("HintRenderer") AndAlso ComboAdvanceRenderer.SelectedIndex <> 0 Then
            If MyMsgBox("修改此项会严重影响游戏的稳定性与性能。如果你不知道你在做什么，不要修改此选项！" & vbCrLf & "你确定要继续修改吗？", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
                ComboAdvanceRenderer.SelectedItem = e.RemovedItems(0)
            Else
                Setup.Set(sender.Tag, sender.SelectedIndex, instance:=PageInstanceLeft.Instance)
                Setup.Set("HintRenderer", True)
            End If
        Else
            Setup.Set(sender.Tag, sender.SelectedIndex, instance:=PageInstanceLeft.Instance)
        End If
    End Sub
    
    Private Sub CheckAdvanceRenderer_CheckChanged(sender As MyCheckBox, e As Object) Handles CheckUseDebugLog4j2Config.Change
        If AniControlEnabled <> 0 Then Return
        If CheckUseDebugLog4j2Config.Checked <> 0  AndAlso Not States.Hint.DebugLog4j2Config Then
            If MyMsgBox("本选项会修改游戏日志级别修改为最低，大量日志输出会消耗大量磁盘空间并可能影响游戏性能。这也可能带来一定安全风险。如果你不知道你在做什么，不要修改此选项！" & vbCrLf & "你确定要继续修改吗？", "警告", "我知道我在做什么", "取消", IsWarn:=True) = 2 Then
                sender.Checked = False
            Else
                Config.Instance.UseDebugLof4j2Config(PageInstanceLeft.Instance) = sender.Checked
                Setup.Set(sender.Tag, sender.Checked, instance:=PageInstanceLeft.Instance)
                States.Hint.DebugLog4j2Config = True
            End If
        Else
            Config.Instance.UseDebugLof4j2Config(PageInstanceLeft.Instance) = sender.Checked
        End If
    End Sub

#End Region

    '切换到全局设置
    Private Sub BtnSwitch_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnSwitch.Click
        FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupLaunch)
    End Sub

End Class
