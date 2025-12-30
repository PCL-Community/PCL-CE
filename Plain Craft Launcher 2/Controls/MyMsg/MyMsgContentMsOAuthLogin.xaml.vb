Imports PCL.Core.UI.Controls
Imports Newtonsoft.Json.Linq
Imports System.Threading

Partial Public Class MyMsgContentLogin
    Inherits MyMsgContent

    Private Data As JObject
    Private UserCode As String '需要用户在网页上输入的设备代码
    Private DeviceCode As String '用于轮询的设备代码
    Private Website As String '验证网页的网址
    Private OAuthUrl As String = "" 'OAuth 轮询验证地址

    Public Sub New(loginData As JObject, authUrl As String)
        InitializeComponent()
        Data = loginData
        OAuthUrl = authUrl
    End Sub

    Public Overrides Sub Initialize()
        UserCode = Data("user_code")
        DeviceCode = Data("device_code")
        
        ' 设置设备代码显示
        LabUserCode.Text = UserCode
        
        ' 复制设备代码到剪贴板
        ClipboardSet(DeviceCode)
        
        ' 设置说明文本
        If Data("verification_uri_complete") IsNot Nothing Then
            Website = Data("verification_uri_complete")
            LabCaption.Text = $"登录网页将自动开启，授权码将自动填充。" & vbCrLf & vbCrLf &
            $"如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" & vbCrLf &
            $"如果没有自动填充，请在页面内粘贴上述设备代码（已自动复制）" & vbCrLf &
            $"你也可以用其他设备打开 {Website} 并输入设备代码。"
        Else
            Website = Data("verification_uri")
            LabCaption.Text = $"登录网页将自动开启，请在网页中输入下方设备代码（已自动复制）。" & vbCrLf & vbCrLf &
            $"如果网络环境不佳，网页可能一直加载不出来，届时请使用 VPN 并重试。" & vbCrLf &
            $"你也可以用其他设备打开 {Website} 并输入此设备代码。"
        End If
        
        ' 设置按钮事件数据
        BtnOpenWebsite.EventData = Website
        BtnCopyCode.EventData = UserCode
        
        ' 启动工作线程
        RunInNewThread(AddressOf WorkThread, "MyMsgContentLogin")
    End Sub

    Public Overrides Function GetResult() As Object
        ' Login 类型的结果在 WorkThread 中设置
        Return Item.Result
    End Function

    Private Sub WorkThread()
        Thread.Sleep(3000)
        If Item IsNot Nothing AndAlso Item.IsExited Then Return
        OpenWebsite(Website)
        ClipboardSet(UserCode)
        Thread.Sleep((Data("interval").ToObject(Of Integer) - 1) * 1000)
        '轮询
        Dim UnknownFailureCount As Integer = 0
        Do While Item IsNot Nothing AndAlso Not Item.IsExited
            Try
                Dim Result = NetRequestOnce(
                    OAuthUrl, "POST",
                    "grant_type=urn:ietf:params:oauth:grant-type:device_code" & "&" &
                    "client_id=" & OAuthClientId & "&" &
                    "device_code=" & DeviceCode & "&" &
                    "scope=XboxLive.signin%20offline_access",
                    "application/x-www-form-urlencoded", 5000 + UnknownFailureCount * 5000, MakeLog:=False)
                '获取结果
                Dim ResultJson As JObject = GetJson(Result)
                ProfileLog($"令牌过期时间：{ResultJson("expires_in")} 秒")
                Hint("网页登录成功！", HintType.Finish)
                Finished({ResultJson("access_token").ToString, ResultJson("refresh_token").ToString})
                Return
            Catch ex As HttpWebException
                Dim response As String = ex.InnerHttpException.WebResponse
                If response.Contains("authorization_declined") Then
                    Finished(New Exception("$你拒绝了 PCL 申请的权限……"))
                    Return
                ElseIf response.Contains("expired_token") Then
                    Finished(New Exception("$登录用时太长啦，重新试试吧！"))
                    Return
                ElseIf response.Contains("Account security interrupt") Then
                    Finished(New Exception("$非常抱歉，该账号由于安全问题无法登陆，请前往 Microsoft 账户页获取更多信息。"))
                    Return
                ElseIf response.Contains("service abuse") Then
                    Finished(New Exception("$非常抱歉，该账号已被微软封禁，无法登录。"))
                    Return
                ElseIf response.Contains("AADSTS70000") Then '可能不能判 "invalid_grant"，见 #269
                    Finished(New RestartException)
                    Return
                ElseIf response.Contains("authorization_pending") Then
                    Thread.Sleep(2000)
                ElseIf UnknownFailureCount <= 2 Then
                    UnknownFailureCount += 1
                    Log(ex, $"正版验证轮询第 {UnknownFailureCount} 次失败")
                    Log("原始返回内容: " & response)
                    Thread.Sleep(2000)
                Else
                    Finished(New Exception("正版验证轮询失败", ex))
                    Return
                End If
            Catch ex As Exception
                If UnknownFailureCount <= 2 Then
                    UnknownFailureCount += 1
                    Log(ex, $"正版验证轮询第 {UnknownFailureCount} 次失败")
                    Log(ex.Message)
                    Thread.Sleep(2000)
                Else
                    Finished(New Exception("正版验证轮询失败", ex))
                    Return
                End If
            End Try
        Loop
    End Sub

    Private Sub Finished(Result As Object)
        If Item Is Nothing OrElse Item.IsExited Then Return
        Item.IsExited = True
        Item.Result = Result
        Item.WaitFrame.Continue = False
        ' 窗口关闭由 MyMsgCustom 处理
        Thread.Sleep(200)
        RunInUi(Sub() FrmMain.ShowWindowToTop())
    End Sub

End Class

