Class PageSetupLink

    Private Shadows IsLoaded As Boolean = False
    Private IsFirstLoad As Boolean = True

    Private Sub PageSetupLink_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoaded Then Return
        IsLoaded = True

        AniControlEnabled += 1
        Reload()
        AniControlEnabled -= 1

    End Sub
    Public Sub Reload()
        TextLinkRelay.Text = Setup.Get("LinkName")
        If String.IsNullOrWhiteSpace(Setup.Get("LinkNaidRefreshToken")) Then
            CardLogged.Visibility = Visibility.Collapsed
            CardNotLogged.Visibility = Visibility.Visible
        Else
            CardLogged.Visibility = Visibility.Visible
            CardNotLogged.Visibility = Visibility.Collapsed
            TextUsername.Text = "正在从 Natayark Network 获取账号信息..."
            TextStatus.Text = ""
            If IsFirstLoad Then
                ReloadNaidData()
                IsFirstLoad = False
            Else
                TextUsername.Text = $"已以 {NaidProfile.Username} 的身份登录至 Natayark Network"
                TextStatus.Text = $"账号状态：{If(NaidProfile.Status = 0, "正常", "异常")} / {If(NaidProfile.IsRealname, "已完成实名验证", "尚未进行实名验证")}"
            End If
        End If
    End Sub
    Private Sub ReloadNaidData()
        RunInNewThread(Sub()
                           Try
                               GetNaidData(Setup.Get("LinkNaidRefreshToken"), True, IsSilent:=True)
                               While String.IsNullOrWhiteSpace(NaidProfile.Username)
                                   Thread.Sleep(1000)
                               End While
                               RunInUi(Sub()
                                           TextUsername.Text = $"已以 {NaidProfile.Username} 的身份登录至 Natayark Network"
                                           TextStatus.Text = $"账号状态：{If(NaidProfile.Status = 0, "正常", "异常")} / {If(NaidProfile.IsRealname, "已完成实名验证", "尚未进行实名验证")}"
                                           CardLogged.Visibility = Visibility.Visible
                                           CardNotLogged.Visibility = Visibility.Collapsed
                                       End Sub)
                           Catch ex As Exception
                               Log("[Link] 刷新 Natayark ID 信息失败，需要重新登录")
                               CardLogged.Visibility = Visibility.Collapsed
                               CardNotLogged.Visibility = Visibility.Visible
                           End Try
                       End Sub)
    End Sub
    Private Sub BtnLogin_Click(sender As Object, e As RoutedEventArgs) Handles BtnLogin.Click
        If MyMsgBox($"PCL 将会打开一个登录页面，请在浏览器中完成登录操作后复制浏览器地址栏中的链接，并将其粘贴到输入框中。{vbCrLf}验证完成后的代码存在有效期，请快速操作避免超时。",
                    "登录至 Natayark Network", "继续", "取消") = 1 Then
            OpenWebsite($"https://account.naids.com/oauth2/authorize?response_type=code&client_id={NatayarkClientId}&redirect_uri=https://ce.open.pcl2.dev")
            Dim Code As String = MyMsgBoxInput("登录至 Natayark Network", $"在浏览器中登录完成后，将地址栏中的链接粘贴到此处。{vbCrLf}若操作过慢可能会超时，此时你需要重新进行验证。", Button1:="确定", Button2:="取消")
            If Not String.IsNullOrWhiteSpace(Code) Then
                If Code.Contains("code=") Then Code = Code.AfterFirst("code=")
                GetNaidData(Code)
            End If
        End If
    End Sub
    Private Sub BtnLogout_Click(sender As Object, e As RoutedEventArgs) Handles BtnLogout.Click
        If MyMsgBox("你确定要退出登录吗？", "退出登录", "确定", "取消") = 1 Then
            Setup.Set("LinkNaidRefreshToken", "")
            Reload()
            Log("[Link] 已退出登录 Natayark Network")
            Hint("已退出登录！", HintType.Finish, False)
        End If
    End Sub
    '初始化
    Public Sub Reset()
        Try
            Setup.Reset("LinkName")

            Log("[Setup] 已初始化联机页设置")
            Hint("已初始化联机页设置！", HintType.Finish, False)
        Catch ex As Exception
            Log(ex, "初始化联机页设置失败", LogLevel.Msgbox)
        End Try

        Reload()
    End Sub

    '将控件改变路由到设置改变
    Private Shared Sub TextBoxChange(sender As MyTextBox, e As Object) Handles TextLinkRelay.ValidatedTextChanged
        If AniControlEnabled = 0 Then Setup.Set(sender.Tag, sender.Text)
    End Sub

End Class
