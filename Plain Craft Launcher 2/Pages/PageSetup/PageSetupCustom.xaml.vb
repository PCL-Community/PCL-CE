Imports PCL.Core.Net

Public Class PageSetupCustom

    Private Shadows IsLoaded As Boolean = False

    Private Sub PageSetupCustom_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()


        AniControlEnabled += 1
        Reload() '#4826，在每次进入页面时都刷新一下
        AniControlEnabled -= 1

        LoadHomepages()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True


    End Sub
    Private Sub Reload()
        Try
            CType(FindName("RadioCustomType" & Setup.Load("UiCustomType", forceReload:=True)), MyRadioBox).Checked = True
            TextCustomNet.Text = Setup.Get("UiCustomNet")

        Catch ex As NullReferenceException
            Log(ex, "主页设置项存在异常，已被自动重置", LogLevel.Msgbox)
            Reset()
        Catch ex As Exception
            Log(ex, "重载主页设置时出错", LogLevel.Feedback)
        End Try
    End Sub

    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("UiCustomType")
            Setup.Reset("UiCustomNet")

            Log("[Setup] 已初始化主页设置！")
            Hint("已初始化主页设置", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化主页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    '有些暂时没用到，先留着吧，谁知道呢……
    'Private Shared Sub SliderChange(sender As MySlider, e As Object) Handles Nothing
    '   If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Value)
    'End Sub
    'Private Shared Sub ComboChange(sender As MyComboBox, e As Object) Handles Nothing
    '    If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.SelectedIndex)
    'End Sub
    'Private Shared Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles Nothing
    '   If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Checked)
    'End Sub
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextCustomNet.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub
    Private Shared Sub RadioBoxChange(sender As MyRadioBox, e As Object) Handles RadioCustomType0.Check, RadioCustomType1.Check, RadioCustomType2.Check
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag.ToString.Split("/")(0), sender.Tag.ToString.Split("/")(1))
        UiCustomType(sender.Tag.ToString.Split("/")(1))
    End Sub
    Private Sub PresetSelectedFromCard(sender As MyListItem, e As Object)
        RadioCustomType2.SetChecked(True, True)
        If AniControlEnabled = 0 Then Setup.Set("UiCustomNet", sender.Tag.ToString)
        TextCustomNet.Text = sender.Tag.ToString
        FrmLaunchRight.ForceRefresh()
    End Sub


    '主页
    Private Sub BtnCustomFile_Click(sender As Object, e As EventArgs) Handles BtnCustomFile.Click
        Try
            If File.Exists(ExePath & "PCL\Custom.xaml") Then
                If MyMsgBox("当前已存在布局文件，继续生成教学文件将会覆盖现有布局文件！", "覆盖确认", "继续", "取消", IsWarn:=True) = 2 Then Return
            End If
            WriteFile(ExePath & "PCL\Custom.xaml", GetResourceStream("Resources/Custom.xml"))
            Hint("教学文件已生成！", HintType.Finish)
            OpenExplorer(ExePath & "PCL\Custom.xaml")
        Catch ex As Exception
            Log(ex, "生成教学文件失败", LogLevel.Feedback)
        End Try
    End Sub
    Private Sub BtnCustomRefresh_Click() Handles BtnCustomRefresh.Click
        FrmLaunchRight.ForceRefresh()
        Hint("已刷新主页！", HintType.Finish)
    End Sub
    Private Sub BtnCustomTutorial_Click(sender As Object, e As EventArgs) Handles BtnCustomTutorial.Click
        MyMsgBox("1. 点击 生成教学文件 按钮，这会在 PCL 文件夹下生成 Custom.xaml 布局文件。" & vbCrLf &
                 "2. 使用记事本等工具打开这个文件并进行修改，修改完记得保存。" & vbCrLf &
                 "3. 点击 刷新主页 按钮，查看主页现在长啥样了。" & vbCrLf &
                 vbCrLf &
                 "你可以在生成教学文件后直接刷新主页，对照着进行修改，更有助于理解。" & vbCrLf &
                 "直接将主页文件拖进 PCL 窗口也可以快捷加载。", "主页自定义教程")
    End Sub

    '主页
    Private Shared Sub UiCustomType(value As Integer)
        Select Case value
            Case 0 '无
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustom.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustomWarn.Visibility = Visibility.Collapsed
            Case 1 '本地
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Visible
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Collapsed
                FrmSetupCustom.HintCustom.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustom.Theme = MyHint.Themes.Blue
                FrmSetupCustom.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupCustom.HintCustom.Text = $"从 PCL 文件夹下的 Custom.xaml 读取主页内容。{vbCrLf}你可以手动编辑该文件，向主页添加文本、图片、常用网站、快捷启动等功能。"
                FrmSetupCustom.HintCustom.EventType = ""
                FrmSetupCustom.HintCustom.EventData = ""
            Case 2 '联网
                FrmSetupCustom.PanCustomLocal.Visibility = Visibility.Collapsed
                FrmSetupCustom.PanCustomNet.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustom.Visibility = Visibility.Visible
                FrmSetupCustom.HintCustom.Theme = MyHint.Themes.Blue
                FrmSetupCustom.HintCustomWarn.Visibility = If(Setup.Get("HintCustomWarn"), Visibility.Collapsed, Visibility.Visible)
                FrmSetupCustom.HintCustom.Text = $"从指定网址联网获取主页内容。服主也可以用于动态更新服务器公告。{vbCrLf}如果你制作了稳定运行的联网主页，可以点击这条提示投稿，若合格即可加入预设！"
                FrmSetupCustom.HintCustom.EventType = "打开网页"
                FrmSetupCustom.HintCustom.EventData = "https://github.com/Hex-Dragon/PCL2/discussions/2528"
        End Select
        FrmSetupCustom.CardCustom.TriggerForceResize()
    End Sub
    Private Async Sub LoadHomepages()
        Dim url As String = "https://pclhomeplazaoss.lingyunawa.top:26993/d/Homepages/Ext1nguisher/homepages.json"

        Dim httpClient = NetworkService.GetClient()
        Try
            ' 获取字节数组而不是字符串
            Dim responseBytes() As Byte = Await httpClient.GetByteArrayAsync(url)

            ' 使用 UTF-8 编码解码
            Dim gbkEncoding As Encoding = Encoding.GetEncoding("UTF-8")
            Dim jsonString As String = gbkEncoding.GetString(responseBytes)
            Dim jsonObj As JObject = JObject.Parse(jsonString)
            Dim homepages As JObject = jsonObj("homepages")


            RunInUi(Sub()
                        HomepagesPan.Children.Clear()
                        Dim index As Integer = 1

                        For Each homepage As JProperty In homepages.Properties()
                            Dim item As JObject = homepage.Value
                            Dim isPreset As Boolean = item("preset").ToObject(Of Boolean)()
                            Dim listItem As New MyListItem With {
                                              .Margin = New Thickness(10, 8, 10, 8),
                                              .ToolTip = If(isPreset, "预设主页", "非预设主页"),
                                              .Title = item("alias").ToString(),
                                              .Info = item("desc").ToString(),
                                              .Type = MyListItem.CheckType.Clickable,
                                              .Logo = $"pack://application:,,,/images/Blocks/RedstoneLamp{If(isPreset, "On", "Off")}.png",
                                              .Tag = item("link")
                                          }

                            ' 添加点击事件处理
                            AddHandler listItem.Click, AddressOf PresetSelectedFromCard

                            HomepagesPan.Children.Add(listItem)
                            index += 1
                        Next
                    End Sub)
        Catch ex As Exception
            Log(ex, "加载联网主页列表失败", LogLevel.Debug)
        End Try
    End Sub

End Class