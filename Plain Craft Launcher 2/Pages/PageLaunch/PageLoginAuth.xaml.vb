Imports System.Net.Http
Imports System.Text.RegularExpressions
Imports PCL.Core.Minecraft.Yggdrasil
Imports PCL.Core.Net

Public Class PageLoginAuth
    Public Shared DraggedAuthServer As String = Nothing
    Private Sub Reload() Handles Me.Loaded
        Dim serverItems = TextServer.Items
        serverItems.Clear()
        For Each serverName In PredefinedAuthServers.Keys
            serverItems.Add(New MyComboBoxItem() With {.Content = serverName})
        Next
        If DraggedAuthServer IsNot Nothing Then
            TextServer.Text = DraggedAuthServer
            DraggedAuthServer = Nothing
        End If
    End Sub
    Private Sub BtnBack_Click(sender As Object, e As EventArgs) Handles BtnBack.Click
        TextServer.Text = Nothing
        TextName.Text = Nothing
        TextPass.Password = Nothing
        FrmLaunchLeft.RefreshPage(True)
    End Sub
    Private Shared ReadOnly PatternHttpUri As New Regex("^(http[s]?)\://")
    Private Sub BtnLogin_Click(sender As Object, e As EventArgs) Handles BtnLogin.Click
        If String.IsNullOrWhiteSpace(TextServer.Text) OrElse String.IsNullOrWhiteSpace(TextName.Text) OrElse String.IsNullOrWhiteSpace(TextPass.Password) Then
            Hint("验证服务器、用户名与密码均不能为空！", HintType.Critical)
            Exit Sub
        End If
        If Not PatternHttpUri.IsMatch(TextServer.Text) Then
            Hint("输入的验证服务器地址无效", HintType.Critical)
            Exit Sub
        End If
        BtnLogin.IsEnabled = False
        BtnBack.IsEnabled = False
        Dim LoginData As New McLoginServer(McLoginType.Auth) With {.BaseUrl = If(TextServer.Text.EndsWithF("/"),
            TextServer.Text & "authserver", TextServer.Text & "/authserver"), .UserName = TextName.Text, .Password = TextPass.Password, .Description = "Authlib-Injector", .Type = McLoginType.Auth}
        Dispatcher.BeginInvoke(Async Function() As Task
            Try
                IsCreatingProfile = True
                McLoginAuthLoader.Start(LoginData, IsForceRestart:=True)
                Do While McLoginAuthLoader.State = LoadState.Loading
                    BtnLogin.Text = Math.Round(McLoginAuthLoader.Progress * 100) & "%"
                    Await Task.Delay(50)
                Loop
                If McLoginAuthLoader.State = LoadState.Finished Then
                    FrmLaunchLeft.RefreshPage(True)
                ElseIf McLoginAuthLoader.State = LoadState.Aborted Then
                    Hint("已取消登录！")
                ElseIf McLoginAuthLoader.Error Is Nothing Then
                    Throw New Exception("未知错误！")
                Else
                    Throw New Exception(McLoginAuthLoader.Error.Message, McLoginAuthLoader.Error)
                End If
            Catch ex As Exception
                If ex.Message = "$$" Then
                ElseIf ex.Message.StartsWith("$") Then
                    Hint(ex.Message.TrimStart("$"), HintType.Critical)
                Else
                    Log(ex, "第三方登录尝试失败", LogLevel.Msgbox)
                End If
            Finally
                IsCreatingProfile = False
                BtnLogin.IsEnabled = True
                BtnBack.IsEnabled = True
                BtnLogin.Text = "登录"
            End Try
        End Function)
    End Sub
    '获取验证服务器名称
    Private Sub GetServerName() Handles TextServer.LostKeyboardFocus
        Dim serverUriInput = TextServer.Text
        If String.IsNullOrWhiteSpace(serverUriInput) Then
            TextServerName.Visibility = Visibility.Hidden
            Exit Sub
        End If
        Dispatcher.BeginInvoke(Async Function() As Task
            Dim serverUri As String = Nothing
            Dim serverName As String = Nothing
            Try
                serverUri = Await ApiLocation.TryRequest(serverUriInput)
                Dim response = Await HttpRequestBuilder.Create(serverUri, HttpMethod.Get).SendAsync()
                Dim responseText As String = Await response.AsStringAsync()
                serverName = Await Task.Run(Function() JObject.Parse(responseText)("meta")("serverName").ToString())
            Catch ex As Exception
                Log(ex, "从服务器获取名称失败", LogLevel.Debug)
            End Try
            If serverUri IsNot Nothing Then TextServer.Text = serverUri
            If serverName Is Nothing Then
                TextServerName.Visibility = Visibility.Hidden
            Else
                TextServerName.Text = "验证服务器: " & serverName
                TextServerName.Visibility = Visibility.Visible
            End If
        End Function)
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
    '预设服务器
    Private Shared ReadOnly PredefinedAuthServers As New Dictionary(Of String, String) From {
        {"预设 - LittleSkin", "https://littleskin.cn/api/yggdrasil"},
        {"自定义", ""}
    }
    Private Sub TextServer_TextChanged(sender As Object, e As TextChangedEventArgs)
        Dim server As String = Nothing
        PredefinedAuthServers.TryGetValue(TextServer.Text, server)
        If server IsNot Nothing Then
            TextServer.Text = server
        End If
    End Sub
End Class
