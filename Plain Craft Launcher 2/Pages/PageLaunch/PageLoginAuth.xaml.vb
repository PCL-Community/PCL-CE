Imports PCL.Core.Minecraft.Yggdrasil

Public Class PageLoginAuth
    Public Shared SelectedAuthServer As String
    Private Sub Reload() Handles Me.Loaded
        If SelectedAuthServer IsNot Nothing Then
            GetServerName()
        End If
    End Sub
    Private Sub BtnBack_Click(sender As Object, e As EventArgs) Handles BtnBack.Click
        RunInUi(Sub()
                    SelectedAuthServer = Nothing
                    TextName.Text = Nothing
                    TextPass.Password = Nothing
                    FrmLaunchLeft.RefreshPage(True)
                End Sub)
    End Sub
    Private Sub BtnLogin_Click(sender As Object, e As EventArgs) Handles BtnLogin.Click
        If String.IsNullOrWhiteSpace(SelectedAuthServer) OrElse String.IsNullOrWhiteSpace(TextName.Text) OrElse String.IsNullOrWhiteSpace(TextPass.Password) Then
            Hint("验证服务器、用户名与密码均不能为空！", HintType.Critical)
            Exit Sub
        End If
        BtnLogin.IsEnabled = False
        BtnBack.IsEnabled = False
        Dim LoginData As New McLoginServer(McLoginType.Auth) With {.BaseUrl = If(SelectedAuthServer.EndsWithF("/"),
            SelectedAuthServer & "authserver", SelectedAuthServer & "/authserver"), .UserName = TextName.Text, .Password = TextPass.Password, .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        RunInNewThread(Sub()
                           Try
                               IsCreatingProfile = True
                               McLoginAuthLoader.Start(LoginData, IsForceRestart:=True)
                               Do While McLoginAuthLoader.State = LoadState.Loading
                                   RunInUi(Sub() BtnLogin.Text = Math.Round(McLoginAuthLoader.Progress * 100) & "%")
                                   Thread.Sleep(50)
                               Loop
                               If McLoginAuthLoader.State = LoadState.Finished Then
                                   RunInUi(Sub()
                                               SelectedAuthServer = Nothing
                                               TextName.Text = Nothing
                                               TextPass.Password = Nothing
                                               FrmLaunchLeft.RefreshPage(True)
                                           End Sub)
                               ElseIf McLoginAuthLoader.State = LoadState.Aborted Then
                                   Throw New ThreadInterruptedException
                               ElseIf McLoginAuthLoader.Error Is Nothing Then
                                   Throw New Exception("未知错误！")
                               Else
                                   Throw New Exception(McLoginAuthLoader.Error.Message, McLoginAuthLoader.Error)
                               End If
                           Catch ex As ThreadInterruptedException
                               Hint("已取消登录！")
                           Catch ex As Exception
                               If ex.Message = "$$" Then
                               ElseIf ex.Message.StartsWith("$") Then
                                   Hint(ex.Message.TrimStart("$"), HintType.Critical)
                               Else
                                   Log(ex, "第三方登录尝试失败", LogLevel.Msgbox)
                               End If
                           Finally
                               RunInUi(
                               Sub()
                                   IsCreatingProfile = False
                                   BtnLogin.IsEnabled = True
                                   BtnBack.IsEnabled = True
                                   BtnLogin.Text = "登录"
                               End Sub)
                           End Try
                       End Sub)
    End Sub
    '处理选择的服务器
    Private Sub SetServer(sender As Object, e As SelectionChangedEventArgs) Handles ComboServer.SelectionChanged
        Select Case ComboServer.SelectedIndex
            Case 0
                SelectedAuthServer = "https://littleskin.cn/api/yggdrasil"
                GetServerName()
            Case 1
                Dim Validate As New ValidateHttp()
                SelectedAuthServer = MyMsgBoxInput("请输入验证服务器", "", "", New ObjectModel.Collection(Of Validate) From {Validate}, "请输入服务器地址")
                If String.IsNullOrEmpty(SelectedAuthServer) Then
                    ComboServer.SelectedIndex = -1
                Else
                    GetServerName()
                End If
        End Select
    End Sub
    '获取验证服务器名称
    Private Sub GetServerName()
        Dim serverUriInput = SelectedAuthServer
        RunInNewThread(Sub()
                           Dim serverUri As String = Nothing
                           Dim serverName As String = Nothing
                           Try
                               serverUri = ApiLocation.TryRequest(serverUriInput).GetAwaiter().GetResult()
                               Dim response As String = NetGetCodeByRequestRetry(serverUri, Encoding.UTF8)
                               serverName = JObject.Parse(response)("meta")("serverName").ToString()
                           Catch ex As Exception
                               Log(ex, "从服务器获取名称失败", LogLevel.Debug)
                           End Try
                           RunInUi(Sub()
                                       If serverUri IsNot Nothing Then SelectedAuthServer = serverUri
                                       If serverName Is Nothing Then
                                           TextServerName.Visibility = Visibility.Hidden
                                       Else
                                           TextServerName.Text = "验证服务器: " & serverName
                                           TextServerName.Visibility = Visibility.Visible
                                       End If
                                   End Sub)
                       End Sub)
    End Sub
    '链接处理
    Private Sub ComboName_TextChanged() Handles TextName.TextChanged
        BtnLink.Content = If(TextName.Text = "", "注册账号", "找回密码")
    End Sub
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        If BtnLink.Content = "注册账号" Then
            OpenWebsite(If(McInstanceCurrent IsNot Nothing, Setup.Get("VersionServerAuthRegister", instance:=McInstanceCurrent), ""))
        Else
            Dim Website As String = If(McInstanceCurrent IsNot Nothing, Setup.Get("VersionServerAuthRegister", instance:=McInstanceCurrent), "")
            OpenWebsite(Website.Replace("/auth/register", "/auth/forgot"))
        End If
    End Sub
    '切换注册按钮可见性
    Private Sub ReloadRegisterButton() Handles Me.Loaded
        Dim Address As String = If(McInstanceCurrent IsNot Nothing, Setup.Get("VersionServerAuthRegister", instance:=McInstanceCurrent), "")
        BtnLink.Visibility = If(String.IsNullOrEmpty(New ValidateHttp().Validate(Address)), Visibility.Visible, Visibility.Collapsed)
    End Sub
End Class