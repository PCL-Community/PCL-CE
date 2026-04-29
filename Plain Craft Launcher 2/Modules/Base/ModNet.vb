Imports System.Net.Http
Imports System.Threading.Tasks
Imports System
Imports System.Buffers
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts
Imports PCL.Core.Utils.TimeUtils
Imports PCL.Core.App
Imports PCL.Core.IO.Net

Public Module ModNet
    Public Const NetDownloadEnd As String = ".PCLDownloading"

    ''' <summary>
    ''' 测试 Ping。失败则返回 -1。
    ''' </summary>
    Public Function Ping(Ip As String, Optional Timeout As Integer = 10000, Optional MakeLog As Boolean = True) As Integer
        Dim PingResult As NetworkInformation.PingReply
        Try
            PingResult = (New NetworkInformation.Ping).Send(Ip)
        Catch ex As Exception
            If MakeLog Then Log("[Net] Ping " & Ip & " 失败：" & ex.Message)
            Return -1
        End Try
        If PingResult.Status = NetworkInformation.IPStatus.Success Then
            If MakeLog Then Log("[Net] Ping " & Ip & " 结束：" & PingResult.RoundtripTime & "ms")
            Return PingResult.RoundtripTime
        Else
            If MakeLog Then Log("[Net] Ping " & Ip & " 失败")
            Return -1
        End If
    End Function

    ''' <summary>
    ''' 当调用 <see cref="EnsureSuccessStatusCode"/> 时，若给定响应的 <c>IsSuccessStatusCode</c> 属性不为 <c>True</c> 则抛出该异常。
    ''' </summary>
    Public Class HttpRequestFailedException
        Inherits HttpRequestException
        Public Overloads ReadOnly Property StatusCode As HttpStatusCode
        Public ReadOnly Property ReasonPhrase As String
        ''' <summary>
        ''' 不要尝试读取 <c>Content</c> 属性的内容，它已经被 dispose 了
        ''' </summary>
        Public ReadOnly Property Response As HttpResponseMessage
        ''' <summary>
        ''' 站点的原始返回内容
        ''' </summary>
        Public ReadOnly Property WebResponse As String
        Public Sub New(response As HttpResponseMessage, Optional webResponse As String = Nothing)
            MyBase.New($"HTTP 响应失败: {response.ReasonPhrase} ({CType(response.StatusCode, Integer)})")
            Me.Response = response
            StatusCode = response.StatusCode
            ReasonPhrase = response.ReasonPhrase
            Me.WebResponse = webResponse
        End Sub
    End Class

    ''' <summary>
    ''' <see cref="HttpRequestFailedException"/> 的套壳，包含 <c>StatusCode</c> 属性。<br/>
    ''' 在此，向龙猫的石山代码致敬。
    ''' </summary>
    Public Class HttpWebException
        Inherits WebException
        Public ReadOnly Property InnerHttpException As HttpRequestFailedException
        Public ReadOnly Property StatusCode As HttpStatusCode
            Get
                Return InnerHttpException.StatusCode
            End Get
        End Property
        Public Sub New(message As String, ex As HttpRequestFailedException)
            MyBase.New(message, ex)
            InnerHttpException = ex
        End Sub
    End Class

    ''' <summary>
    ''' <see cref="HttpResponseMessage.EnsureSuccessStatusCode"/> 的改进版，将抛出附带 <c>StatusCode</c> 和 <c>ReasonPhrase</c> 属性的异常。
    ''' 这个改进已经在 .NET 5 官方实装，鬼知道为什么 .NET Framework 连最新的 4.8.1 都这么原始。
    ''' </summary>
    ''' <exception cref="HttpRequestFailedException">HTTP 响应失败</exception>
    Private Sub EnsureSuccessStatusCode(response As HttpResponseMessage)
        If Not response.IsSuccessStatusCode Then
            Dim content As String = response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            response.Content?.Dispose()
            Throw New HttpRequestFailedException(response, content)
        End If
    End Sub

    ''' <summary>
    ''' 以 WebRequest 获取网页源代码或 Json。会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
    ''' </summary>
    ''' <param name="Url">网页的 Url。</param>
    ''' <param name="Encode">网页的编码，通常为 UTF-8。</param>
    ''' <param name="BackupUrl">如果第一次尝试失败，换用的备用 URL。</param>
    ''' <param name="IsJson">是否解析为 Json。</param>
    ''' <param name="Accept">请求的套接字类型。</param>
    ''' <param name="UseBrowserUserAgent">是否使用浏览器 User-Agent。</param>
    Public Function NetGetCodeByRequestRetry(Url As String, Optional Encode As Encoding = Nothing, Optional Accept As String = "",
                                             Optional IsJson As Boolean = False, Optional BackupUrl As String = Nothing, Optional UseBrowserUserAgent As Boolean = False)
        Dim RetryCount As Integer = 0
        Dim RetryException As Exception = Nothing
        Dim StartTime As Long = TimeUtils.GetTimeTick()
        While RetryCount <= 3
            RetryCount += 1
            Try
                Select Case RetryCount
                    Case 0 '正常尝试
                        Return NetGetCodeByRequestOnce(Url, Encode, 10000, IsJson, Accept, UseBrowserUserAgent)
                    Case 1 '慢速重试
                        Thread.Sleep(500)
                        Return NetGetCodeByRequestOnce(If(BackupUrl, Url), Encode, 30000, IsJson, Accept, UseBrowserUserAgent)
                    Case Else '快速重试
                        If TimeUtils.GetTimeTick() - StartTime > 5500 Then
                            '若前两次加载耗费 5 秒以上，才进行重试
                            Thread.Sleep(500)
                            Return NetGetCodeByRequestOnce(If(BackupUrl, Url), Encode, 4000, IsJson, Accept, UseBrowserUserAgent)
                        Else
                            Throw RetryException
                        End If
                End Select
            Catch ex As ThreadInterruptedException
                Throw
            Catch ex As Exception
                RetryException = ex
            End Try
        End While
        Throw RetryException
    End Function
    Public Function NetGetCodeByRequestOnce(Url As String, Optional Encode As Encoding = Nothing, Optional Timeout As Integer = 30000, Optional IsJson As Boolean = False, Optional Accept As String = "", Optional UseBrowserUserAgent As Boolean = False)
        If RunInUi() AndAlso Not Url.Contains("//127.") Then Throw New Exception("在 UI 线程执行了网络请求")
        Try
            Url = SecretCdnSign(Url)
            Log($"[Net] 获取网络结果：{Url}，超时 {Timeout}ms{If(IsJson, "，要求 Json", "")}")
            Using cts As New CancellationTokenSource
                cts.CancelAfter(Timeout)
                Using request As New HttpRequestMessage(HttpMethod.Get, Url)
                    request.Headers.Accept.ParseAdd(Accept)
                    SecretHeadersSign(Url, request, UseBrowserUserAgent)
                    Using response = NetworkService.GetClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult()
                        EnsureSuccessStatusCode(response)
                        If Encode Is Nothing Then Encode = Encoding.UTF8
                        Using responseStream As Stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                            '读取流并转换为字符串
                            Using reader As New StreamReader(responseStream, Encode)
                                Dim content As String = reader.ReadToEnd()
                                If String.IsNullOrEmpty(content) Then Throw New WebException("获取结果失败，内容为空（" & Url & "）")
                                Return If(IsJson, GetJson(content), content)
                            End Using
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As TaskCanceledException
            Throw New TimeoutException("连接服务器超时（" & Url & "）", ex)
        Catch ex As HttpRequestFailedException
            Throw New HttpWebException("获取结果失败，" & ex.Message & "（" & Url & "）", ex)
        Catch ex As Exception
            Throw New WebException("获取结果失败，" & ex.Message & "（" & Url & "）", ex)
        End Try
    End Function

    ''' <summary>
    ''' 以多线程下载网页文件的方式获取网页源代码。
    ''' </summary>
    ''' <param name="Url">网页的 Url。</param>
    Public Function NetGetCodeByLoader(Url As String, Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional UseBrowserUserAgent As Boolean = False) As String
        Dim Temp As String = RequestTaskTempFolder() & "download.txt"
        Dim NewTask As New LoaderDownload("源码获取 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile({Url}, Temp, New FileChecker With {.IsJson = IsJson}, UseBrowserUserAgent)})
        Try
            NewTask.WaitForExitTime(Timeout, TimeoutMessage:="连接服务器超时（" & Url & "）")
            NetGetCodeByLoader = ReadFile(Temp)
            File.Delete(Temp)
        Finally
            NewTask.Abort()
        End Try
    End Function
    ''' <summary>
    ''' 以多线程下载网页文件的方式获取网页源代码。
    ''' </summary>
    ''' <param name="Urls">网页的 Url 列表。</param>
    Public Function NetGetCodeByLoader(Urls As IEnumerable(Of String), Optional Timeout As Integer = 45000, Optional IsJson As Boolean = False, Optional UseBrowserUserAgent As Boolean = False) As String
        Dim Temp As String = RequestTaskTempFolder() & "download.txt"
        Dim NewTask As New LoaderDownload("源码获取 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile(Urls, Temp, New FileChecker With {.IsJson = IsJson}, UseBrowserUserAgent)})
        Try
            NewTask.WaitForExitTime(Timeout, TimeoutMessage:="连接服务器超时（第一下载源：" & Urls.First & "）")
            NetGetCodeByLoader = ReadFile(Temp)
            File.Delete(Temp)
        Finally
            NewTask.Abort()
        End Try
    End Function

    ''' <summary>
    ''' 使用 HttpClient 从网络中下载文件。这不能下载 CDN 中的文件。
    ''' </summary>
    ''' <param name="Url">网络 Url。</param>
    ''' <param name="LocalFile">下载的本地地址。</param>
    Public Async Function NetDownloadByClient(Url As String, LocalFile As String, Optional UseBrowserUserAgent As Boolean = False) As Task
        Log("[Net] 直接下载文件：" & Url)
        Try
            Directory.CreateDirectory(GetPathFromFullPath(LocalFile))
            If File.Exists(LocalFile) Then File.Delete(LocalFile)
            Using request As New HttpRequestMessage(HttpMethod.Get, Url)
                SecretHeadersSign(Url, request, UseBrowserUserAgent)
                Using response As HttpResponseMessage = Await NetworkService.GetClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                    EnsureSuccessStatusCode(response)
                    Using httpStream As Stream = Await response.Content.ReadAsStreamAsync()
                        Using fileStream As New FileStream(LocalFile, FileMode.Create)
                            Await httpStream.CopyToAsync(fileStream)
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As TaskCanceledException When ex.InnerException Is Nothing
            Throw New TimeoutException($"下载超时（{Url}）", ex)
        Catch ex As HttpRequestFailedException
            Throw New HttpWebException($"下载失败：{ex.Message}（{Url}）", ex)
        Catch ex As Exception
            If File.Exists(LocalFile) Then File.Delete(LocalFile)
            Throw New WebException($"下载失败：{ex.Message}（{Url}）", ex)
        End Try
    End Function

    ''' <summary>
    ''' 简单的多线程下载文件。可以下载 CDN 中的文件。
    ''' </summary>
    ''' <param name="Url">文件的 Url。</param>
    ''' <param name="LocalFile">下载的本地地址。</param>
    Public Sub NetDownloadByLoader(Url As String, LocalFile As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional UseBrowserUserAgent As Boolean = False)
        Dim NewTask As New LoaderDownload("文件下载 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile({Url}, LocalFile, Check, UseBrowserUserAgent)})
        Try
            NewTask.WaitForExit(LoaderToSyncProgress:=LoaderToSyncProgress)
        Catch ex As Exception
            Throw New WebException($"多线程直接下载文件失败（{Url}）", ex)
        Finally
            NewTask.Abort()
        End Try
    End Sub

    ''' <summary>
    ''' 简单的多线程下载文件。可以下载 CDN 中的文件。
    ''' </summary>
    ''' <param name="Urls">文件的 Url 列表。</param>
    ''' <param name="LocalFile">下载的本地地址。</param>
    Public Sub NetDownloadByLoader(Urls As IEnumerable(Of String), LocalFile As String, Optional LoaderToSyncProgress As LoaderBase = Nothing, Optional Check As FileChecker = Nothing, Optional UseBrowserUserAgent As Boolean = False)
        Dim NewTask As New LoaderDownload("文件下载 " & GetUuid() & "#", New List(Of NetFile) From {New NetFile(Urls, LocalFile, Check, UseBrowserUserAgent)})
        Try
            NewTask.WaitForExit(LoaderToSyncProgress:=LoaderToSyncProgress)
        Catch ex As Exception
            Throw New WebException($"多线程直接下载文件失败（第一下载源：" & Urls.First() & "）", ex)
        Finally
            NewTask.Abort()
        End Try
    End Sub

    ''' <summary>
    ''' 发送一个网络请求并获取返回内容，会重试三次并在最长 45s 后超时。
    ''' </summary>
    ''' <param name="Url">请求的服务器地址。</param>
    ''' <param name="Method">请求方式（POST 或 GET）。</param>
    ''' <param name="Data">请求的内容。</param>
    ''' <param name="ContentType">请求的套接字类型。</param>
    ''' <param name="DontRetryOnRefused">当返回 40x 时不重试。</param>
    Public Function NetRequestRetry(Url As String, Method As String, Data As Object, ContentType As String, Optional DontRetryOnRefused As Boolean = True, Optional Headers As Dictionary(Of String, String) = Nothing) As String
        Dim RetryCount As Integer = 0
        Dim RetryException As Exception = Nothing
        Dim StartTime As Long = TimeUtils.GetTimeTick()
        While RetryCount <= 3
            RetryCount += 1
            Try
                Select Case RetryCount
                    Case 0 '正常尝试
                        Return NetRequestOnce(Url, Method, Data, ContentType, 15000, Headers)
                    Case 1 '慢速重试
                        Thread.Sleep(500)
                        Return NetRequestOnce(Url, Method, Data, ContentType, 25000, Headers)
                    Case Else '快速重试
                        If TimeUtils.GetTimeTick() - StartTime > 5500 Then
                            '若前两次加载耗费 5 秒以上，才进行重试
                            Thread.Sleep(500)
                            Return NetRequestOnce(Url, Method, Data, ContentType, 4000, Headers)
                        Else
                            Throw RetryException
                        End If
                End Select
            Catch ex As ThreadInterruptedException
                Throw
            Catch ex As Exception
                If ex.InnerException IsNot Nothing AndAlso
                    TypeOf ex.InnerException Is HttpRequestFailedException AndAlso
                    CInt(CType(ex.InnerException, HttpRequestFailedException).StatusCode).ToString().StartsWithF("4") AndAlso
                    DontRetryOnRefused Then Throw
                RetryException = ex
                Log(ex, $"[Net] 网络请求第 {RetryCount} 次失败（{Url}）", LogLevel.Debug)
            End Try
        End While
        Throw RetryException
    End Function
    ''' <summary>
    ''' 发送一次网络请求并获取返回内容。
    ''' </summary>
    ''' <param name="Url"></param>
    ''' <param name="Method"></param>
    ''' <param name="Data"></param>
    ''' <param name="ContentType">仅 Data 为 string 时可用</param>
    ''' <param name="Timeout"></param>
    ''' <param name="Headers"></param>
    ''' <param name="MakeLog"></param>
    ''' <param name="UseBrowserUserAgent"></param>
    ''' <returns></returns>
    Public Function NetRequestOnce(Url As String, Method As String, Data As Object, ContentType As String, Optional Timeout As Integer = 25000, Optional Headers As Dictionary(Of String, String) = Nothing, Optional MakeLog As Boolean = True, Optional UseBrowserUserAgent As Boolean = False) As String
        If RunInUi() AndAlso Not Url.Contains("//127.") Then Throw New Exception("在 UI 线程执行了网络请求")
        Url = SecretCdnSign(Url)
        If MakeLog Then Log("[Net] 发起网络请求（" & Method & "，" & Url & "），最大超时 " & Timeout)
        Try
            Using cts As New CancellationTokenSource
                cts.CancelAfter(Timeout)
                Dim RequestMethod As HttpMethod = HttpMethod.Get
                Select Case Method.ToUpper() '我不相信上面的输入.jpg
                    Case "POST"
                        RequestMethod = HttpMethod.Post
                    Case "PUT"
                        RequestMethod = HttpMethod.Put
                    Case "DELETE"
                        RequestMethod = HttpMethod.Delete
                    Case "HEAD"
                        RequestMethod = HttpMethod.Head
                    Case "OPTIONS"
                        RequestMethod = HttpMethod.Options
                End Select
                Using request As New HttpRequestMessage(RequestMethod, Url)
                    SecretHeadersSign(Url, request, UseBrowserUserAgent)
                    If {HttpMethod.Post, HttpMethod.Put}.Contains(RequestMethod) Then
                        If Not IsNothing(Data) Then
                            If TypeOf Data Is Byte() Then
                                request.Content = New ByteArrayContent(Data)
                            ElseIf TypeOf Data Is String Then
                                request.Content = New StringContent(Data, Encoding.UTF8, ContentType)
                            ElseIf Data.GetType().IsSubclassOf(GetType(HttpContent)) Then
                                request.Content = CType(Data, HttpContent)
                            Else
                                Throw New ArgumentException("Data 参数类型不支持")
                            End If
                        End If
                    End If
                    If Headers IsNot Nothing Then
                        For Each Pair In Headers
                            If String.IsNullOrWhiteSpace(Pair.Key) OrElse String.IsNullOrWhiteSpace(Pair.Value) Then Continue For
                            '标头覆盖
                            If request.Headers.Contains(Pair.Key) Then
                                request.Headers.Remove(Pair.Key)
                            End If
                            request.Headers.Add(Pair.Key, Pair.Value)
                        Next
                    End If
                    Using response = NetworkService.GetClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter().GetResult()
                        EnsureSuccessStatusCode(response)
                        Using responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                            Using reader As New StreamReader(responseStream, Encoding.UTF8)
                                Return reader.ReadToEnd()
                            End Using
                        End Using
                    End Using
                End Using
            End Using
        Catch ex As ThreadInterruptedException
            Throw
        Catch ex As Exception
            Dim nx = If(TypeOf ex Is HttpRequestFailedException,
                        New HttpWebException("网络请求失败（" & Url & "）", ex),
                        New WebException("网络请求失败（" & Url & "）", ex))
            If MakeLog Then Log(nx, "NetRequestOnce 请求失败", LogLevel.Developer)
            Throw nx
        End Try
    End Function

    Public Class ResponsedWebException
        Inherits WebException
        ''' <summary>
        ''' 远程服务器给予的回复。
        ''' </summary>
        Public Overloads Property Response As String
        Public Sub New(Message As String, Response As String, InnerException As Exception)
            MyBase.New(Message, InnerException)
            Me.Response = Response
        End Sub
    End Class

    ''' <summary>
    ''' 最大线程数。
    ''' </summary>
    Public NetTaskThreadLimit As Integer
    ''' <summary>
    ''' 速度下限。
    ''' </summary>
    Public NetTaskSpeedLimitLow As Long = 256 * 1024L '256K/s
    ''' <summary>
    ''' 速度上限。若无限制则为 -1。
    ''' </summary>
    Public NetTaskSpeedLimitHigh As Long = -1
    ''' <summary>
    ''' 基于限速，当前可以下载的剩余量。
    ''' </summary>
    Public NetTaskSpeedLimitLeft As Long = -1
    Private ReadOnly NetTaskSpeedLimitLeftLock As New Object
    Private NetTaskSpeedLimitLeftLast As Long
    ''' <summary>
    ''' 正在运行中的线程数。
    ''' </summary>
    Public NetTaskThreadCount As Integer = 0
    Private ReadOnly NetTaskThreadCountLock As New Object

    ''' <summary>
    ''' 下载源。
    ''' </summary>
    Public Class NetSource
        Public Id As Integer
        Public Url As String
        Public FailCount As Integer
        Public Ex As Exception
        ''' <summary>
        ''' 若该下载源正在进行强制单线程下载，标记这个唯一的线程。
        ''' </summary>
        Public SingleThread As NetThread
        Public IsFailed As Boolean
        Public Overrides Function ToString() As String
            Return Url
        End Function
    End Class
    ''' <summary>
    ''' 下载进度标示。
    ''' </summary>
    Public Enum NetState
        ''' <summary>
        ''' 尚未进行已存在检查。
        ''' </summary>
        WaitingToCheck = -1
        ''' <summary>
        ''' 尚未开始。
        ''' </summary>
        WaitingToDownload = 0
        ''' <summary>
        ''' 正在连接，尚未获取文件大小。
        ''' </summary>
        Connecting = 1
        ''' <summary>
        ''' 已获取文件大小，尚未有有效下载。
        ''' </summary>
        [Reading] = 2
        ''' <summary>
        ''' 正在下载。
        ''' </summary>
        Downloading = 3
        ''' <summary>
        ''' 正在合并文件。
        ''' </summary>
        Merging = 4
        ''' <summary>
        ''' 已完成。
        ''' </summary>
        Finished = 5
        ''' <summary>
        ''' 已失败或中断。
        ''' </summary>
        [Interrupted] = 6
    End Enum
    ''' <summary>
    ''' 预下载检查行为。
    ''' </summary>
    Public Enum NetPreDownloadBehaviour
        ''' <summary>
        ''' 当文件已存在时，显示提示以提醒用户是否继续下载。
        ''' </summary>
        HintWhileExists
        ''' <summary>
        ''' 当文件已存在或正在下载时，直接退出下载函数执行，不对用户进行提示。
        ''' </summary>
        ExitWhileExistsOrDownloading
        ''' <summary>
        ''' 不进行已存在检查。
        ''' </summary>
        IgnoreCheck
    End Enum

    ''' <summary>
    ''' 下载线程。
    ''' </summary>
    Public Class NetThread
        Implements IEnumerable(Of NetThread), IEquatable(Of NetThread)

        ''' <summary>
        ''' 对应的下载任务。
        ''' </summary>
        Public Task As NetFile
        ''' <summary>
        ''' 对应的线程。
        ''' </summary>
        Public Thread As Thread
        ''' <summary>
        ''' 链表中的下一个线程。
        ''' </summary>
        Public NextThread As NetThread
        Private ReadOnly Iterator Property [Next]() As IEnumerable(Of NetThread)
            Get
                Dim CurrentChain As NetThread = Me
                While CurrentChain IsNot Nothing
                    Yield CurrentChain
                    CurrentChain = CurrentChain.NextThread
                End While
            End Get
        End Property
        Public Function GetEnumerator() As IEnumerator(Of NetThread) Implements IEnumerable(Of NetThread).GetEnumerator
            Return [Next].GetEnumerator()
        End Function
        Private Function IEnumerable_GetEnumerator() As IEnumerator Implements IEnumerable.GetEnumerator
            Return [Next].GetEnumerator()
        End Function

        ''' <summary>
        ''' 分配给任务中每个线程（无论其是否失败）的编号。
        ''' </summary>
        Public Uuid As Integer
        ''' <summary>
        ''' 是否为第一个线程。
        ''' </summary>
        Public ReadOnly Property IsFirstThread As Boolean
            Get
                Return DownloadStart = 0 AndAlso Task.FileSize = -2
            End Get
        End Property
        ''' <summary>
        ''' 该线程的缓存文件。
        ''' </summary>
        Public Temp As String

        ''' <summary>
        ''' 线程下载起始位置。
        ''' </summary>
        Public DownloadStart As Long
        ''' <summary>
        ''' 线程下载结束位置。
        ''' </summary>
        Public ReadOnly Property DownloadEnd As Long
            Get
                SyncLock Task.LockChain
                    If NextThread Is Nothing Then
                        If Task.IsUnknownSize Then
                            Return 5 * 1024 * 1024 * 1024L '5G
                        Else
                            Return Task.FileSize - 1
                        End If
                    Else
                        Return NextThread.DownloadStart - 1
                    End If
                End SyncLock
            End Get
        End Property
        ''' <summary>
        ''' 线程未下载的文件大小。
        ''' </summary>
        Public ReadOnly Property DownloadUndone As Long
            Get
                Return DownloadEnd - (DownloadStart + DownloadDone) + 1
            End Get
        End Property
        ''' <summary>
        ''' 线程已下载的文件大小。
        ''' </summary>
        Public DownloadDone As Long = 0

        ''' <summary>
        ''' 上次记速时的时间。
        ''' </summary>
        Private SpeedLastTime As Long = TimeUtils.GetTimeTick()
        ''' <summary>
        ''' 上次记速时的已下载大小。
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' 当前的下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If TimeUtils.GetTimeTick() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = TimeUtils.GetTimeTick() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' 线程初始化时的时间。
        ''' </summary>
        Public InitTime As Long = TimeUtils.GetTimeTick()
        ''' <summary>
        ''' 上次接受到有效数据的时间，-1 表示尚未有有效数据。
        ''' </summary>
        Public LastReceiveTime As Long = -1

        ''' <summary>
        ''' 当前线程的状态。
        ''' </summary>
        Public State As NetState = NetState.WaitingToDownload
        ''' <summary>
        ''' 是否已经结束。
        ''' </summary>
        Public ReadOnly Property IsEnded As Boolean
            Get
                Return State = NetState.Finished OrElse State = NetState.Interrupted
            End Get
        End Property

        ''' <summary>
        ''' 当前选取的是哪一个 Url。
        ''' </summary>
        Public Source As NetSource

        '允许进行 UUID 比较
        Public Overloads Function Equals(other As NetThread) As Boolean Implements IEquatable(Of NetThread).Equals
            Return other IsNot Nothing AndAlso Uuid = other.Uuid
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Equals(TryCast(obj, NetThread))
        End Function
        Public Shared Operator =(left As NetThread, right As NetThread) As Boolean
            Return EqualityComparer(Of NetThread).Default.Equals(left, right)
        End Operator
        Public Shared Operator <>(left As NetThread, right As NetThread) As Boolean
            Return Not left = right
        End Operator
    End Class

    ''' <summary>
    ''' 下载单个文件。
    ''' </summary>
    Public Class NetFile

#Region "属性"

        ''' <summary>
        ''' 所属的文件列表任务。
        ''' </summary>
        Public Tasks As New SafeList(Of LoaderDownload)
        ''' <summary>
        ''' 所有下载源。
        ''' </summary>
        Public Sources As SafeList(Of NetSource)
        ''' <summary>
        ''' 用于在第一个线程出错时切换下载源。
        ''' </summary>
        Private FirstThreadSource As Integer = 0
        ''' <summary>
        ''' 所有已经被标记为失败的，但未完整尝试过的，不允许断点续传的下载源。
        ''' </summary>
        Public SourcesOnce As New SafeList(Of NetSource)
        ''' <summary>
        ''' 仅当合并失败或首次下载失败时，会将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载。
        ''' 这一策略可以兼容多个下载源中的一部分返回错误的文件的情况，以及部分在多线程下载时会抽风的源。
        ''' </summary>
        Private Retried As Boolean = False
        ''' <summary>
        ''' 获取从某个源开始，第一个可用的源。
        ''' </summary>
        Private Function GetSource(Optional Id As Integer = 0) As NetSource
            If Sources.Count = 0 Then Return Nothing
            Id = Id Mod Sources.Count
            SyncLock LockSource
                If HasAvailableSource(False) Then
                    '存在多线程可用源
                    Dim CurrentSource As NetSource = Sources(Id)
                    While CurrentSource.IsFailed
                        Id += 1
                        If Id >= Sources.Count Then Id = 0
                        CurrentSource = Sources(Id)
                    End While
                    Return CurrentSource
                ElseIf SourcesOnce.Any Then
                    '仅存在单线程可用源
                    Return SourcesOnce(0)
                Else
                    '没有可用源
                    Return Nothing
                End If
            End SyncLock
        End Function
        ''' <summary>
        ''' 是否存在可用源。
        ''' </summary>
        Public Function HasAvailableSource(Optional AllowOnceSource As Boolean = True) As Boolean
            SyncLock LockSource
                If Sources.Any(Function(s) Not s.IsFailed) Then Return True '存在多线程可用源
                If AllowOnceSource AndAlso SourcesOnce.Any Then Return True '存在单线程可用源
            End SyncLock
            Return False
        End Function

        ''' <summary>
        ''' 存储在本地的带文件名的地址。
        ''' </summary>
        Public LocalPath As String = Nothing
        ''' <summary>
        ''' 存储在本地的文件名。
        ''' </summary>
        Public LocalName As String = Nothing

        ''' <summary>
        ''' 当前的下载状态。
        ''' </summary>
        Public State As NetState = NetState.WaitingToCheck
        ''' <summary>
        ''' 导致下载失败的原因。
        ''' </summary>
        Public Ex As New List(Of Exception)

        ''' <summary>
        ''' 作为文件组成部分的线程链表。
        ''' 如果没有线程，可以为 Nothing。
        ''' </summary>
        Public Threads As NetThread

        ''' <summary>
        ''' 文件的总大小。若为 -2 则为未获取，若为 -1 则为无法获取准确大小。
        ''' </summary>
        Public FileSize As Long = -2
        ''' <summary>
        ''' 该文件是否无法获取准确大小。
        ''' </summary>
        Public IsUnknownSize As Boolean = False
        ''' <summary>
        ''' 该文件是否不需要分割。
        ''' </summary>
        Public ReadOnly Property IsNoSplit As Boolean
            Get
                Return IsUnknownSize OrElse FileSize < FilePieceLimit
            End Get
        End Property
        ''' <summary>
        ''' 为不需要分割的小文件进行临时存储。
        ''' </summary>
        Private SmallFileCache As MemoryStream

        ''' <summary>
        ''' 文件的已下载大小。
        ''' </summary>
        Public DownloadDone As Long = 0
        Private ReadOnly LockDone As New Object
        ''' <summary>
        ''' 文件的校验规则。
        ''' </summary>
        Public Check As FileChecker
        ''' <summary>
        ''' 下载时是否添加浏览器 UA。
        ''' </summary>
        Public UseBrowserUserAgent As Boolean

        ''' <summary>
        ''' 是否允许多线程下载
        ''' </summary>
        Public AllowMuiltThread As Boolean = True

        ''' <summary>
        ''' 自定义User-Agent
        ''' </summary>
        Public CustomUserAgent As String = ""

        ''' <summary>
        ''' 上次记速时的时间。
        ''' </summary>
        Private SpeedLastTime As Long = TimeUtils.GetTimeTick()
        ''' <summary>
        ''' 上次记速时的已下载大小。
        ''' </summary>
        Private SpeedLastDone As Long = 0
        ''' <summary>
        ''' 当前的下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public ReadOnly Property Speed As Long
            Get
                If TimeUtils.GetTimeTick() - SpeedLastTime > 200 Then
                    Dim DeltaTime As Long = TimeUtils.GetTimeTick() - SpeedLastTime
                    _Speed = (DownloadDone - SpeedLastDone) / (DeltaTime / 1000)
                    SpeedLastDone = DownloadDone
                    SpeedLastTime += DeltaTime
                End If
                Return _Speed
            End Get
        End Property
        Private _Speed As Long = 0

        ''' <summary>
        ''' 该文件是否由本地文件直接拷贝完成。
        ''' </summary>
        Public IsCopy As Boolean = False
        ''' <summary>
        ''' 本文件的显示进度。
        ''' </summary>
        Public ReadOnly Property Progress As Double
            Get
                Select Case State
                    Case NetState.WaitingToCheck
                        Return 0
                    Case NetState.WaitingToDownload
                        Return 0.01
                    Case NetState.Connecting
                        Return 0.02
                    Case NetState.Reading
                        Return 0.04
                    Case NetState.Downloading
                        '正在下载中，对应 5% ~ 98%
                        Dim OriginalProgress As Double = If(IsUnknownSize, 0.5, DownloadDone / Math.Max(FileSize, 1))
                        OriginalProgress = 1 - (1 - OriginalProgress) ^ 0.9
                        Return OriginalProgress * 0.93 + 0.05
                    Case NetState.Merging
                        Return 0.99
                    Case NetState.Finished, NetState.Interrupted
                        Return 1
                    Case Else
                        Return 0.5
                        'Throw New ArgumentOutOfRangeException("文件状态未知：" & State)
                End Select
            End Get
        End Property

        ''' <summary>
        ''' 各个线程建立连接成功的总次数。
        ''' </summary>
        Private ConnectCount As Integer = 0
        ''' <summary>
        ''' 各个线程建立连接成功的总时间。
        ''' </summary>
        Private ConnectTime As Long = 0
        ''' <summary>
        ''' 各个线程建立连接成功的平均时间，单位为毫秒，-1 代表尚未有成功连接。
        ''' </summary>
        Private ReadOnly Property ConnectAverage As Integer
            Get
                SyncLock LockCount
                    Return If(ConnectCount = 0, -1, ConnectTime / ConnectCount)
                End SyncLock
            End Get
        End Property

        Private Const FilePieceLimit As Long = 256 * 1024
        Public ReadOnly LockCount As New Object
        Public ReadOnly LockState As New Object
        Public ReadOnly LockChain As New Object
        Public ReadOnly LockSource As New Object

        Public ReadOnly Uuid As Integer = GetUuid()
        Public Overrides Function Equals(obj As Object) As Boolean
            Dim file = TryCast(obj, NetFile)
            Return file IsNot Nothing AndAlso Uuid = file.Uuid
        End Function

#End Region

        ''' <summary>
        ''' 新建一个需要下载的文件。
        ''' </summary>
        ''' <param name="localPath">包含文件名的本地地址。</param>
        Public Sub New(urls As IEnumerable(Of String), localPath As String, Optional checker As FileChecker = Nothing, Optional useBrowserUserAgent As Boolean = False, Optional customUserAgent As String = "")
            Dim sources As New List(Of NetSource)
            Dim count As Integer = 0
            urls = urls.Distinct.ToArray
            For Each source As String In urls
                sources.Add(New NetSource With {.FailCount = 0, .Url = SecretCdnSign(source.Replace(vbCr, "").Replace(vbLf, "").Trim), .Id = count, .IsFailed = False, .Ex = Nothing})
                count += 1
            Next
            Me.Sources = New SafeList(Of NetSource)(sources)
            Me.LocalPath = localPath
            Me.Check = checker
            Me.UseBrowserUserAgent = useBrowserUserAgent
            Me.CustomUserAgent = customUserAgent
            Me.LocalName = GetFileNameFromPath(localPath)
        End Sub

        ''' <summary>
        ''' 尝试开始一个新的下载线程。
        ''' 如果失败，返回 Nothing。
        ''' </summary>
        Public Function TryBeginThread() As NetThread
            Try

                '条件检测
                If NetTaskThreadCount >= NetTaskThreadLimit OrElse Not HasAvailableSource() OrElse
                    (IsNoSplit AndAlso Threads IsNot Nothing AndAlso Threads.State <> NetState.Interrupted AndAlso
                     Threads.State <> NetState.WaitingToDownload AndAlso TimeUtils.GetTimeTick() - Threads.InitTime < 30000) Then Return Nothing
                '小文件线程卡住检测：如果线程启动超过30秒仍处于Connect或Get状态，允许重试
                If State >= NetState.Merging OrElse State = NetState.WaitingToCheck Then Return Nothing
                SyncLock LockState
                    If State < NetState.Connecting Then State = NetState.Connecting
                End SyncLock
                '初始化参数
                Dim StartPosition As Long, StartSource As NetSource = Nothing
                Dim Th As Thread, ThreadInfo As NetThread
                SyncLock LockChain
                    '获取线程起点与下载源
                    '不分割
                    If IsNoSplit Then GoTo Capture
                    '单线程
                    If Not HasAvailableSource(False) Then
                        '确认没有其他线程正使用此点
                        If SourcesOnce(0).SingleThread IsNot Nothing AndAlso SourcesOnce(0).SingleThread.State <> NetState.Interrupted Then Return Nothing
                        '占用此点
Capture:
                        '小文件缓存保护：只有在确认需要重新开始时才清空缓存
                        '如果已有缓存且线程正在运行，不清空缓存
                        If IsNoSplit AndAlso SmallFileCache IsNot Nothing AndAlso Threads IsNot Nothing AndAlso
                           Threads.State <> NetState.Interrupted AndAlso Threads.State <> NetState.Finished Then
                            '已有缓存且线程未完成，不清空，直接返回
                            Return Nothing
                        End If
                        SmallFileCache?.Dispose()
                        SmallFileCache = Nothing
                        Threads = Nothing
                        NetManager.DownloadDone -= DownloadDone
                        SyncLock LockDone
                            DownloadDone = 0
                        End SyncLock
                        SpeedLastDone = 0
                        State = NetState.Reading
                    End If
                    '首个开始点
                    If Threads Is Nothing Then
                        StartPosition = 0
                        StartSource = GetSource(FirstThreadSource)
                        FirstThreadSource = StartSource.Id + 1
                        GoTo StartThread
                    End If
                    '寻找失败点
                    For Each Thread As NetThread In Threads
                        If Thread.State = NetState.Interrupted AndAlso Thread.DownloadUndone > 0 Then
                            StartPosition = Thread.DownloadStart + Thread.DownloadDone
                            StartSource = GetSource(Thread.Source.Id + 1)
                            GoTo StartThread
                        End If
                    Next
                    '是否禁用多线程，以及规定碎片大小
                    Dim TargetUrl As String = GetSource().Url
                    If Not AllowMuiltThread OrElse TargetUrl.Contains("pcl2-server") OrElse TargetUrl.Contains("bmclapi") OrElse TargetUrl.Contains("github.com") OrElse
                       TargetUrl.Contains("optifine.net") OrElse TargetUrl.Contains("modrinth") OrElse TargetUrl.Contains("gitcode") OrElse
                       TargetUrl.Contains("pysio.online") OrElse TargetUrl.Contains("mirrorchyan.com") OrElse TargetUrl.Contains("naids.com") Then Return Nothing
                    '寻找最大碎片
                    'FUTURE: 下载引擎重做，计算下载源平均链接时间和线程下载速度，按最高时间节省来开启多线程
                    Dim FilePieceMax As NetThread = Threads
                    For Each Thread As NetThread In Threads
                        If Thread.DownloadUndone > FilePieceMax.DownloadUndone Then FilePieceMax = Thread
                    Next
                    If FilePieceMax Is Nothing OrElse FilePieceMax.DownloadUndone < FilePieceLimit Then Return Nothing
                    StartPosition = FilePieceMax.DownloadEnd - FilePieceMax.DownloadUndone * 0.4
                    StartSource = GetSource()

                    '开始线程
StartThread:
                    If (StartPosition > FileSize AndAlso FileSize >= 0 AndAlso Not IsUnknownSize) OrElse StartPosition < 0 OrElse IsNothing(StartSource) Then Return Nothing
                    '构建线程
                    Dim ThreadUuid As Integer = GetUuid()
                    If Not Tasks.Any() Then Return Nothing '由于中断，已没有可用任务
                    Th = New Thread(AddressOf Thread) With {.Name = $"NetTask {Tasks(0).Uuid}/{Uuid} Download {ThreadUuid}#", .Priority = ThreadPriority.BelowNormal}
                    ThreadInfo = New NetThread With {.Uuid = ThreadUuid, .DownloadStart = StartPosition, .Thread = Th, .Source = StartSource, .Task = Me, .State = NetState.WaitingToDownload}
                    '链表处理
                    If ThreadInfo.IsFirstThread OrElse Threads Is Nothing Then
                        Threads = ThreadInfo
                    Else
                        Dim CurrentChain As NetThread = Threads
                        While CurrentChain.DownloadEnd <= StartPosition
                            CurrentChain = CurrentChain.NextThread
                        End While
                        ThreadInfo.NextThread = CurrentChain.NextThread
                        CurrentChain.NextThread = ThreadInfo
                    End If

                End SyncLock
                '开始线程
                SyncLock NetTaskThreadCountLock
                    NetTaskThreadCount += 1
                End SyncLock
                SyncLock LockSource
                    If Not HasAvailableSource(False) Then SourcesOnce(0).SingleThread = ThreadInfo
                End SyncLock
                Th.Start(ThreadInfo)
                Return ThreadInfo

            Catch ex As Exception
                Log(ex, "尝试开始下载线程失败（" & If(LocalName, "Nothing") & "）", LogLevel.Hint)
                Return Nothing
            End Try
        End Function
        ''' <summary>
        ''' 每个下载线程执行的代码。
        ''' </summary>
        Private Sub Thread(th As NetThread)
            If ModeDebug OrElse th.DownloadStart = 0 Then Log("[Download] " & LocalName & " " & th.Uuid & "#：开始，起始点 " & th.DownloadStart & "，" & th.Source.Url)
            Dim resultStream As Stream = Nothing
            '部分下载源真的特别慢，并且只需要一个请求，例如 Ping 为 20s，如果增长太慢，就会造成类似 2.5s 5s 7.5s 10s 12.5s... 的极大延迟
            '延迟过长会导致某些特别慢的链接迟迟不被掐死
            Dim Timeout As Integer = Math.Min(Math.Max(ConnectAverage, 6000) * (1 + th.Source.FailCount), 25000)
            Dim ContentLength As Long = 0
            th.State = NetState.Connecting
            '记录连接开始时间，用于检测连接阶段卡住
            Dim ConnectStartTime As Long = TimeUtils.GetTimeTick()
            Try
                Dim httpDataCount As Integer = 0
                If SourcesOnce.Contains(th.Source) AndAlso Not th.Equals(th.Source.SingleThread) Then GoTo SourceBreak
                Dim request As New HttpRequestMessage(HttpMethod.Get, th.Source.Url)
                SecretHeadersSign(th.Source.Url, request, UseBrowserUserAgent, Me.CustomUserAgent)
                If Not th.IsFirstThread OrElse th.DownloadStart <> 0 Then request.Headers.Range = New Headers.RangeHeaderValue(th.DownloadStart, Nothing)
                Using cts As New CancellationTokenSource
                    cts.CancelAfter(Timeout)
                    '连接阶段超时检测：如果连接耗时过长，提前抛出异常
                    Using response = NetworkService.
                        GetClient().
                        SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).
                        GetAwaiter().
                        GetResult()
                        EnsureSuccessStatusCode(response)
                        If State = NetState.Interrupted Then GoTo SourceBreak '快速中断
                        Dim redirected = response.RequestMessage.RequestUri
                        If redirected.OriginalString <> th.Source.Url Then
                            Log($"[Download] {LocalName} {th.Uuid}#：重定向至 {redirected.OriginalString}")
                            th.Source.Url = redirected.OriginalString
                            If redirected.Scheme = "http" Then
                                Log($"[Download] {LocalName} {th.Uuid}#：检测到下载源重定向到 HTTP 协议，下载流量可能存在安全问题")
                            End If
                        End If
                        '文件大小校验
                        ContentLength = response.Content.Headers.ContentLength.GetValueOrDefault(-1)
                        If ContentLength = -1 Then
                            If FileSize > 1 Then
                                If th.DownloadStart = 0 Then
                                    Log($"[Download] {LocalName} {th.Uuid}#：文件大小未知，但已从其他下载源获取，不作处理")
                                Else
                                    Log($"[Download] {LocalName} {th.Uuid}#：ContentLength 返回了 -1，无法确定是否支持分段下载，视作不支持")
                                    GoTo NotSupportRange
                                End If
                            Else
                                FileSize = -1 : IsUnknownSize = True
                                Log($"[Download] {LocalName} {th.Uuid}#：文件大小未知")
                            End If
                        ElseIf ContentLength < 0 Then
                            Throw New Exception("获取片大小失败，结果为 " & ContentLength & "。")
                        ElseIf th.IsFirstThread Then
                            If Check IsNot Nothing Then
                                If ContentLength < Check.MinSize AndAlso Check.MinSize > 0 Then
                                    Throw New Exception($"文件大小不足，获取结果为 {ContentLength}，要求至少为 {Check.MinSize}。")
                                End If
                                If ContentLength <> Check.ActualSize AndAlso Check.ActualSize > 0 Then
                                    Throw New Exception($"文件大小不一致，获取结果为 {ContentLength}，要求必须为 {Check.ActualSize}。")
                                End If
                            End If
                            FileSize = ContentLength : IsUnknownSize = False
                            Log($"[Download] {LocalName} {th.Uuid}#：文件大小 {ContentLength}（{GetString(ContentLength)}）")

                            '若文件大小大于 50 M，进行剩余磁盘空间校验
                            If ContentLength > 50 * 1024 * 1024 Then
                                ' 获取缓存目录和目标文件所在盘符（仅限本地固定磁盘）
                                Dim tempRoot = TryGetLocalDriveRoot(PathTemp)
                                Dim localRoot = TryGetLocalDriveRoot(LocalPath)
                                If tempRoot IsNot Nothing AndAlso localRoot IsNot Nothing Then
                                    For Each drive As DriveInfo In DriveInfo.GetDrives()
                                        ' 跳过特殊存储位置（如 CD-ROM、RAM 盘、未就绪设备）
                                        If Not drive.DriveType.Equals(DriveType.Fixed) OrElse
                                            Not drive.DriveType.Equals(DriveType.Removable) OrElse
                                            Not drive.IsReady Then Continue For
                                        Dim requiredSpace As Long = 0

                                        ' 如果缓存目录在此盘，预留 110% 空间（防碎片/写入膨胀）
                                        If Not String.IsNullOrEmpty(tempRoot) AndAlso String.Equals(drive.Name, tempRoot, StringComparison.OrdinalIgnoreCase) Then
                                            requiredSpace += CLng(ContentLength * 1.1)
                                        End If

                                        ' 如果目标文件在此盘，预留文件大小 + 5MB 安全余量
                                        If Not String.IsNullOrEmpty(localRoot) AndAlso String.Equals(drive.Name, localRoot, StringComparison.OrdinalIgnoreCase) Then
                                            requiredSpace += ContentLength + 5 * 1024 * 1024
                                        End If

                                        If requiredSpace > 0 AndAlso drive.TotalFreeSpace < requiredSpace Then
                                            Dim msg = $"{drive.Name.TrimEnd("\"c)} 盘空间不足，无法进行下载。{vbCrLf}" &
                                                $"需要至少 {GetString(requiredSpace)} 空间，但当前仅剩余 {GetString(drive.TotalFreeSpace)}。"
                                            If String.Equals(drive.Name, tempRoot, StringComparison.OrdinalIgnoreCase) Then
                                                msg &= vbCrLf & vbCrLf & "下载时需要与文件同等大小的空间存放缓存，你可以在设置中调整缓存文件夹的位置。"
                                            End If
                                            Throw New IOException(msg)
                                        End If
                                    Next
                                End If
                            End If
                        ElseIf FileSize < 0 Then
                            Throw New Exception("非首线程运行时，尚未获取文件大小")
                        ElseIf th.DownloadStart > 0 AndAlso ContentLength = FileSize Then
NotSupportRange:
                            SyncLock LockSource
                                If SourcesOnce.Contains(th.Source) Then
                                    GoTo SourceBreak
                                Else
                                    SourcesOnce.Add(th.Source)
                                End If
                            End SyncLock
                            Throw New WebException($"该下载源不支持分段下载：Range 起始于 {th.DownloadStart}，预期 ContentLength 为 {FileSize - th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                        ElseIf Not FileSize - th.DownloadStart = ContentLength Then
                            Throw New WebException($"获取到的分段大小不一致：Range 起始于 {th.DownloadStart}，预期 ContentLength 为 {FileSize - th.DownloadStart}，返回 ContentLength 为 {ContentLength}，总文件大小 {FileSize}")
                        End If
                        'Log($"[Download] {LocalName} {Info.Uuid}#：通过大小检查，文件大小 {FileSize}，起始点 {Info.DownloadStart}，ContentLength {ContentLength}")
                        th.State = NetState.Reading
                        SyncLock LockState
                            If State < NetState.Reading Then State = NetState.Reading
                        End SyncLock
                        '创建缓存文件
                        If IsNoSplit Then
                            th.Temp = Nothing
                            SmallFileCache = New MemoryStream()
                        Else
                            th.Temp = $"{PathTemp}Download\{Uuid}_{th.Uuid}_{RandomUtils.NextInt(0, 999999)}.tmp"
                            resultStream = New FileStream(th.Temp, FileMode.Create, FileAccess.Write, FileShare.Read)
                        End If
                        '开始下载
                        Using httpStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                            If Config.Debug.AddRandomDelay Then Threading.Thread.Sleep(RandomUtils.NextInt(50, 3000))
                            Const bufferSize As Integer = 16384
                            Using httpDataBufferOwner = MemoryPool(Of Byte).Shared.Rent(bufferSize)
                                Dim dataBuffer = httpDataBufferOwner.Memory.Span
                                '首次读取前记录时间，用于检测首次读取卡住的情况
                                Dim FirstReadStartTime As Long = TimeUtils.GetTimeTick()
                                httpDataCount = httpStream.Read(dataBuffer)
                                '首次读取后立即更新 LastReceiveTime，避免首次读取卡住无法检测超时
                                If httpDataCount > 0 Then
                                    th.LastReceiveTime = TimeUtils.GetTimeTick()
                                Else
                                    '首次读取返回0，记录时间用于后续超时检测
                                    th.LastReceiveTime = FirstReadStartTime
                                End If
                                While (IsUnknownSize OrElse th.DownloadUndone > 0) AndAlso '判断是否下载完成
                                httpDataCount > 0 AndAlso Not IsProgramEnded AndAlso State < NetState.Merging AndAlso Not th.Source.IsFailed
                                    '限速
                                    While NetTaskSpeedLimitHigh > 0 AndAlso NetTaskSpeedLimitLeft <= 0
                                        Threading.Thread.Sleep(8)
                                    End While
                                    Dim realDataCount As Integer = If(IsUnknownSize, httpDataCount, Math.Min(httpDataCount, th.DownloadUndone))
                                    SyncLock NetTaskSpeedLimitLeftLock
                                        If NetTaskSpeedLimitHigh > 0 Then NetTaskSpeedLimitLeft -= realDataCount
                                    End SyncLock
                                    Dim deltaTime = TimeUtils.GetTimeTick() - th.LastReceiveTime
                                    If deltaTime > 1000000 Then deltaTime = 1 '时间刻反转导致出现极大值
                                    If realDataCount > 0 Then
                                        '有数据
                                        If th.DownloadDone = 0 Then
                                            '第一次接受到数据
                                            th.State = NetState.Downloading
                                            SyncLock LockState
                                                If State < NetState.Downloading Then State = NetState.Downloading
                                            End SyncLock
                                            SyncLock LockCount
                                                ConnectCount += 1
                                                ConnectTime += TimeUtils.GetTimeTick() - th.InitTime
                                            End SyncLock
                                        End If
                                        SyncLock LockCount
                                            th.Source.FailCount = 0
                                            For Each Task In Tasks
                                                Task.FailCount = 0
                                            Next
                                        End SyncLock
                                        NetManager.DownloadDone += realDataCount
                                        SyncLock LockDone
                                            DownloadDone += realDataCount
                                        End SyncLock
                                        th.DownloadDone += realDataCount
                                        Dim pendingBuffer = dataBuffer.Slice(0, realDataCount)
                                        If IsNoSplit Then
                                            SmallFileCache.Write(pendingBuffer)
                                        Else
                                            resultStream.Write(pendingBuffer)
                                        End If
                                        '检查速度是否过慢
                                        If deltaTime > 1500 AndAlso deltaTime > realDataCount Then '数据包间隔大于 1.5s，且速度小于 1.5K/s
                                            Throw New TimeoutException("由于速度过慢断开链接，下载 " & realDataCount & " B，消耗 " & deltaTime & " ms。")
                                        End If
                                        th.LastReceiveTime = TimeUtils.GetTimeTick()
                                        '已完成
                                        If th.DownloadUndone = 0 AndAlso Not IsUnknownSize Then Exit While
                                    ElseIf th.LastReceiveTime > 0 AndAlso deltaTime > Timeout Then
                                        '无数据，且已超时（包括首次读取后长时间无数据的情况）
                                        Throw New TimeoutException("操作超时，无数据。")
                                    End If
                                    '记录读取开始时间，用于检测读取操作本身卡住
                                    Dim ReadStartTime As Long = TimeUtils.GetTimeTick()
                                    httpDataCount = httpStream.Read(dataBuffer)
                                    '如果读取操作耗时过长，可能是网络卡住
                                    Dim ReadElapsed = TimeUtils.GetTimeTick() - ReadStartTime
                                    If ReadElapsed > Timeout * 0.5 AndAlso httpDataCount = 0 Then
                                        '读取操作本身耗时过长且返回0，可能网络已断开
                                        Throw New TimeoutException($"读取操作超时，耗时 {ReadElapsed}ms，返回数据量 {httpDataCount}。")
                                    End If
                                End While
                            End Using
                        End Using
                    End Using
                End Using
SourceBreak:
                If State = NetState.Interrupted OrElse th.Source.IsFailed OrElse (th.DownloadUndone > 0 AndAlso Not IsUnknownSize) Then
                    '被外部中断
                    th.State = NetState.Interrupted
                    Log($"[Download] {LocalName} {th.Uuid}#：中断")
                ElseIf httpDataCount = 0 AndAlso th.DownloadUndone > 0 AndAlso Not IsUnknownSize Then
                    '服务器无返回数据
                    Throw New Exception($"返回的 ContentLength 过多：ContentLength 为 {ContentLength}，但获取到的总数据量仅为 {th.DownloadDone}（全文件总数据量 {DownloadDone}）")
                Else
                    '本线程完成
                    th.State = NetState.Finished
                    If ModeDebug Then Log($"[Download] {LocalName} {th.Uuid}#：完成，已下载 {th.DownloadDone}")
                End If
            Catch exc As Exception
                Log($"[Download] {LocalName}：出错，{If(TypeOf exc Is OperationCanceledException OrElse TypeOf exc Is TimeoutException,
                                                    $"已超时（{Timeout}ms）", exc.Message())}")
                SourceFail(th, exc, False)
            Finally
                If resultStream IsNot Nothing Then resultStream.Dispose()
                SyncLock NetTaskThreadCountLock
                    NetTaskThreadCount -= 1
                End SyncLock
                '可能在没有下载完的时候开始合并文件了，这造成了大多数合并失败
                If ((FileSize >= 0 AndAlso DownloadDone >= FileSize) OrElse (FileSize = -1 AndAlso DownloadDone > 0)) AndAlso State < NetState.Merging Then Merge()
            End Try
        End Sub
        Private Sub SourceFail(th As NetThread, ex As Exception, isMergeFailure As Boolean)
            '状态变更
            SyncLock LockCount
                th.Source.FailCount += 1
                For Each Task In Tasks
                    Task.FailCount += 1
                Next
            End SyncLock
            Dim isTimeoutString As String = ex.ToString().ToLower.Replace(" ", "")
            Dim isTimeout As Boolean = isTimeoutString.Contains("由于连接方在一段时间后没有正确答复或连接的主机没有反应") OrElse
                                           isTimeoutString.Contains("超时") OrElse isTimeoutString.Contains("timeout") OrElse isTimeoutString.Contains("timedout") OrElse
                                           ex.GetType() = GetType(TimeoutException) OrElse ex.GetType() = GetType(TaskCanceledException) OrElse (ex.GetType() = GetType(AggregateException) AndAlso CType(ex, AggregateException).InnerExceptions.Any(Function(x) x.GetType() = GetType(TaskCanceledException) OrElse x.GetType() = GetType(TimeoutException)))
            'Log("[Download] " & LocalName & " " & th.Uuid & If(isTimeout, "#：超时（" & (th. * 0.001) & "s）", "#：出错，" & ex.ToString()))
            th.State = NetState.Interrupted
            th.Source.Ex = ex
            '根据情况判断，是否在多线程下禁用下载源（连续错误过多，或不支持断点续传）
            Dim IsRangeNotSupported As Boolean = TypeOf ex Is RangeNotSupportedException OrElse ex.Message.Contains("(416)")
            If isMergeFailure OrElse IsRangeNotSupported OrElse
                    ex.Message.Contains("(502)") OrElse ex.Message.Contains("(404)") OrElse
                    ex.Message.Contains("未能解析") OrElse ex.Message.Contains("无返回数据") OrElse ex.Message.Contains("空间不足") OrElse
                    ((ex.Message.Contains("(403)") OrElse ex.Message.Contains("(429)")) AndAlso Not th.Source.Url.ContainsF("bmclapi")) OrElse 'BMCLAPI 的部分源在高频率请求下会返回 403/429，所以不应因此禁用下载源
                    (th.Source.FailCount >= MathClamp(NetTaskThreadLimit, 5, 30) AndAlso DownloadDone < 1) OrElse th.Source.FailCount > NetTaskThreadLimit + 2 Then
                '当一个下载源有多个线程在下载时，只选择其中一个线程进行后续处理
                Dim IsThisFail As Boolean = False
                SyncLock LockSource
                    If Not th.Source.IsFailed OrElse th.Source.SingleThread = th Then
                        IsThisFail = True
                        th.Source.IsFailed = True
                    End If
                End SyncLock
                '……后续处理
                If IsThisFail Then
                    Log($"[Download] {LocalName}：下载源被禁用（{th.Source.Id}，Range 问题：{IsRangeNotSupported}）：{th.Source.Url}")
                    Log(ex, $"{If(SourcesOnce.FirstOrDefault?.SingleThread Is Nothing, "", "单线程")}下载源 {th.Source.Id} 已被禁用",
                            If(IsRangeNotSupported OrElse ex.Message.Contains("(404)"), LogLevel.Developer, LogLevel.Debug))
                    SyncLock LockSource
                        SourcesOnce.Remove(th.Source)
                    End SyncLock
                    If ex.Message.Contains("空间不足") Then
                        '硬盘空间不足：强制失败
                        Fail(ex)
                    ElseIf HasAvailableSource() AndAlso Not isMergeFailure Then
                        '当前源失败，但还有下载源：正常地继续执行
                    ElseIf Not Retried Then
                        '合并失败或首次下载失败，未重试：将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载
                        '若所有源均不支持 Range，也会走到这里重试
                        If Not IsRangeNotSupported Then Log($"[Download] {LocalName}：文件下载失败，正在自动重试……", LogLevel.Debug)
                        Retried = True
                        SyncLock LockSource
                            SourcesOnce.Clear()
                            For Each Source In Sources
                                SourcesOnce.Add(Source)
                                Source.IsFailed = True
                            Next
                        End SyncLock
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    ElseIf HasAvailableSource() AndAlso isMergeFailure Then
                        '合并失败且单个源失败：继续下一个源
                        Reset()
                        SyncLock LockState
                            State = NetState.WaitingToDownload
                        End SyncLock
                    Else
                        '失败
                        Log($"[Download] {LocalName}：已无可用下载源，下载失败")
                        Dim ExampleEx As Exception = Nothing
                        SyncLock LockSource
                            For Each Source As NetSource In Sources
                                Log("[Download] 已禁用的下载源：" & Source.Url)
                                If Source.Ex IsNot Nothing Then
                                    ExampleEx = Source.Ex
                                    Log(Source.Ex, "下载源禁用原因", LogLevel.Developer)
                                End If
                            Next
                        End SyncLock
                        Fail(ExampleEx)
                    End If
                End If
            End If
            '清理当前已下载的内容
            If FileSize = -2 Then Reset()
        End Sub
        ''' <summary>
        ''' 从 HTTP 响应头中获取文件名。
        ''' 如果没有，返回 Nothing。
        ''' </summary>
        Private Function GetFileNameFromResponse(response As HttpResponseMessage) As String
            Return response.Content.Headers.ContentDisposition.FileName
        End Function

        '下载文件的最终收束事件
        ''' <summary>
        ''' 下载完成。合并文件。
        ''' </summary>
        Private Sub Merge()
            '状态判断
            SyncLock LockState
                If State < NetState.Merging Then
                    State = NetState.Merging
                Else
                    Return
                End If
            End SyncLock
            Dim RetryCount As Integer = 0
            Dim MergeFile As Stream = Nothing
            Try
Retry:
                SyncLock LockChain
                    '创建文件夹
                    If File.Exists(LocalPath) Then File.Delete(LocalPath)
                    Directory.CreateDirectory(GetPathFromFullPath(LocalPath))
                    '合并文件
                    If IsNoSplit Then
                        '仅有一个线程，从缓存中输出
                        '检查缓存是否存在
                        If SmallFileCache Is Nothing Then
                            Throw New Exception($"小文件缓存为空，无法合并文件（{LocalName}）。可能原因：缓存被意外清空或下载未完成。")
                        End If
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，从缓存输出文件，长度：" & SmallFileCache.Length)
                        '大小可能真的是 0，需要后续校验的支持
                        'If SmallFileCache.Length = 0 Then
                        '    Throw New Exception($"小文件缓存长度为0，无法合并文件（{LocalName}）。")
                        'End If
                        SmallFileCache.Seek(0, SeekOrigin.Begin)
                        MergeFile = New FileStream(LocalPath, FileMode.Create)
                        SmallFileCache.CopyTo(MergeFile)
                        MergeFile.Dispose() : MergeFile = Nothing
                    ElseIf Threads.DownloadDone = DownloadDone AndAlso Threads.Temp IsNot Nothing Then
                        '仅有一个文件，直接复制
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，仅有一个文件，无需合并")
                        CopyFile(Threads.Temp, LocalPath)
                    Else
                        '有多个线程，合并
                        If ModeDebug Then Log($"[Download] {LocalName}：下载结束，开始合并文件")
                        MergeFile = New FileStream(LocalPath, FileMode.Create)
                        For Each Thread As NetThread In Threads
                            If Thread.DownloadDone = 0 OrElse Thread.Temp Is Nothing Then Continue For
                            Using fs As New FileStream(Thread.Temp, FileMode.Open, FileAccess.Read, FileShare.Read)
                                fs.CopyTo(MergeFile)
                            End Using
                        Next
                        MergeFile.Dispose() : MergeFile = Nothing
                    End If
                    '写入大小要求
                    If Not IsUnknownSize AndAlso Check IsNot Nothing Then
                        If Check.ActualSize = -1 Then
                            Check.ActualSize = FileSize
                        ElseIf Check.ActualSize <> FileSize Then
                            Throw New Exception($"文件大小不一致：任务要求为 {Check.ActualSize} B，网络获取结果为 {FileSize}B")
                        End If
                    End If
                    '检查文件
                    Dim CheckResult As String = Check?.Check(LocalPath)
                    If CheckResult IsNot Nothing Then
                        Log($"[Download] {LocalName} 文件校验失败，下载线程细节：")
                        For Each Th As NetThread In Threads
                            Log($"[Download]     {Th.Uuid}#，状态 {GetStringFromEnum(Th.State)}，范围 {Th.DownloadStart}~{Th.DownloadStart + Th.DownloadDone}，完成 {Th.DownloadDone}，剩余 {Th.DownloadUndone}")
                        Next
                        Throw New Exception(CheckResult)
                    End If
                    '后处理
                    If IsNoSplit Then
                        SmallFileCache?.Dispose()
                        SmallFileCache = Nothing
                    Else
                        For Each Thread As NetThread In Threads
                            If Thread.Temp IsNot Nothing Then File.Delete(Thread.Temp)
                        Next
                    End If
                    Finish()
                End SyncLock
            Catch ex As Exception
                Log(ex, "合并文件出错（" & LocalName & "）")
                MergeFile?.Dispose() : MergeFile = Nothing
                '重试
                If RetryCount <= 3 Then
                    Threading.Thread.Sleep(RandomUtils.NextInt(500, 1000))
                    RetryCount += 1
                    GoTo Retry
                End If
                Fail(ex)
            End Try
        End Sub
        ''' <summary>
        ''' 下载失败。
        ''' </summary>
        Private Sub Fail(Optional RaiseEx As Exception = Nothing)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                If RaiseEx IsNot Nothing Then Ex.Add(RaiseEx)
                '凉凉
                State = NetState.Interrupted
            End SyncLock
            InterruptAndDelete()
            For Each Task In Tasks
                Task.OnFileFail(Me)
            Next
        End Sub
        ''' <summary>
        ''' 下载中断。
        ''' </summary>
        Public Sub Abort(CausedByTask As LoaderDownload)
            '从特定任务中移除，如果它还属于其他任务，则继续下载
            Tasks.Remove(CausedByTask)
            If Tasks.Any Then Return
            '确认中断
            SyncLock LockState
                If State >= NetState.Finished Then Return
                State = NetState.Interrupted
            End SyncLock

            InterruptAndDelete()
        End Sub
        Private Sub InterruptAndDelete()
            'On Error Resume Next
            Try
                If File.Exists(LocalPath) Then File.Delete(LocalPath)
            Catch ex As Exception
                Log(ex, $"[Download] 尝试删除文件 {LocalPath} 失败，忽略错误", LogLevel.Normal)
            End Try

            SyncLock NetManager.LockRemain
                NetManager.FileRemain -= 1
                Log($"[Download] {LocalName}：状态 {State}，剩余文件 {NetManager.FileRemain}")
            End SyncLock
        End Sub

        '状态改变接口
        ''' <summary>
        ''' 将该文件设置为已下载完成。
        ''' </summary>
        Public Sub Finish(Optional PrintLog As Boolean = True)
            SyncLock LockState
                If State >= NetState.Finished Then Return
                State = NetState.Finished
            End SyncLock
            SyncLock NetManager.LockRemain
                NetManager.FileRemain -= 1
                If PrintLog Then Log("[Download] " & LocalName & "：已完成，剩余文件 " & NetManager.FileRemain)
            End SyncLock
            For Each Task In Tasks
                Task.OnFileFinish(Me)
            Next
        End Sub

    End Class
    Private Class RangeNotSupportedException
        Inherits WebException
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class
    ''' <summary>
    ''' 下载一系列文件的加载器。
    ''' </summary>
    Public Class LoaderDownload
        Inherits LoaderBase

#Region "属性"

        ''' <summary>
        ''' 需要下载的文件。
        ''' </summary>
        Public Files As SafeList(Of NetFile)
        ''' <summary>
        ''' 剩余未完成的文件数。（用于减轻 FilesLock 的占用）
        ''' </summary>
        Private FileRemain As Integer
        Private ReadOnly FileRemainLock As New Object

        ''' <summary>
        ''' 用于显示的百分比进度。
        ''' </summary>
        Public Overrides Property Progress As Double
            Get
                If State >= LoadState.Finished Then Return 1
                If Not Files.Any() Then Return 0 '必须返回 0，否则在获取列表的时候会错觉已经下载完了
                Return _Progress
            End Get
            Set(value As Double)
                Throw New Exception("文件下载不允许指定进度")
            End Set
        End Property
        Private _Progress As Double = 0

        ''' <summary>
        ''' 任务中的文件的连续失败计数。
        ''' </summary>
        Public Property FailCount As Integer
            Get
                Return _FailCount
            End Get
            Set(value As Integer)
                _FailCount = value
                If State = LoadState.Loading AndAlso value >= Math.Min(10000, Math.Max(FileRemain * 5.5, NetTaskThreadLimit * 5.5 + 3)) Then
                    Log("[Download] 由于同加载器中失败次数过多引发强制失败：连续失败了 " & value & " 次", LogLevel.Debug)
                    'On Error Resume Next
                    Dim ExList As New List(Of Exception)
                    For Each File In Files
                        For Each Source In File.Sources
                            If Source.Ex IsNot Nothing Then
                                ExList.Add(Source.Ex)
                                If ExList.Count > 10 Then GoTo FinishExCatch
                            End If
                        Next
                    Next
FinishExCatch:
                    OnFail(ExList)
                End If
            End Set
        End Property
        Private _FailCount As Integer = 0

#End Region

        ''' <summary>
        ''' 刷新公开属性。由 NetManager 每 0.1 秒调用一次。
        ''' </summary>
        Public Sub RefreshStat()
            '计算进度
            Dim NewProgress As Double = 0
            Dim TotalProgress As Double = 0
            For Each File In Files
                If File.IsCopy Then
                    NewProgress += File.Progress * 0.2
                    TotalProgress += 0.2
                Else
                    NewProgress += File.Progress
                    TotalProgress += 1
                End If
            Next
            If TotalProgress > 0 AndAlso Not Double.IsNaN(TotalProgress) Then NewProgress /= TotalProgress
            '刷新进度
            _Progress = NewProgress
        End Sub

        Public Sub New(Name As String, FileTasks As List(Of NetFile))
            Me.Name = Name
            Files = New SafeList(Of NetFile)(FileTasks)
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            If Input IsNot Nothing Then Files = New SafeList(Of NetFile)(Input)
            '去重
            Files = New SafeList(Of NetFile)(Files.Distinct(Function(a, b) a.LocalPath = b.LocalPath))
            '设置剩余文件数
            SyncLock FileRemainLock
                FileRemain += Files.Where(Function(f) f.State <> NetState.Finished).Count
            End SyncLock
            State = LoadState.Loading
            '开始执行
            RunInNewThread(
            Sub()
                Try
                    '输入检测
                    If Not Files.Any() Then
                        OnFinish()
                        Return
                    End If
                    For Each File As NetFile In Files
                        If File Is Nothing Then Throw New ArgumentException("存在空文件请求！")
                        For Each Source As NetSource In File.Sources
                            If Not (Source.Url.StartsWithF("https://", True) OrElse Source.Url.StartsWithF("http://", True)) Then
                                Source.Ex = New ArgumentException("输入的下载链接不正确！")
                                Source.IsFailed = True
                            End If
                        Next
                        If Not File.HasAvailableSource() Then Throw New ArgumentException("输入的下载链接不正确！")
                        File.LocalPath = File.LocalPath.Replace("/", "\")
                        If Not File.LocalPath.ToLower.Contains(":\") Then Throw New ArgumentException("输入的本地文件地址不正确: " & File.LocalPath)
                        If File.LocalPath.EndsWithF("\") Then Throw New ArgumentException("请输入含文件名的完整文件路径: " & File.LocalPath)
                        Directory.CreateDirectory(GetPathFromFullPath(File.LocalPath)) '创建目标文件夹
                    Next
                    '接入任务管理器
                    NetManager.Start(Me)
                    '====================================
                    ' 已存在文件查找
                    '====================================

                    '整理允许进行查找的文件
                    Dim FilesToCheck As New List(Of NetFile)
                    Dim DisabledCopy As Boolean = Setup.Get("SystemDebugSkipCopy") '在设置中禁用了复制
                    For Each File In Files
                        If Not DisabledCopy AndAlso File.Check?.CanUseExistsFile Then
                            FilesToCheck.Add(File)
                        Else '不允许，直接开始下载
                            SyncLock LockState
                                File.State = NetState.WaitingToDownload
                                File.IsCopy = False
                            End SyncLock
                        End If
                    Next
                    If Not FilesToCheck.Any Then Return
                    '获取 MC 文件夹列表
                    Dim Folders As New List(Of String)
                    Folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) & "\.minecraft\") '总是添加官启文件夹，因为 HMCL 会把所有文件存在这里
                    Folders.AddRange(McFolderList.Select(Function(f) f.Location))
                    Folders = Folders.Distinct.Where(Function(f) Directory.Exists(f)).ToList
                    '平均分配到多个检查线程
                    Dim ThreadCount As Integer = MathClamp(FilesToCheck.Count \ 40, 1, 8) '每个线程至少 40 个文件，最多 8 线程
                    If ThreadCount = 1 Then '只有一个线程，直接执行
                        CheckExistingFiles(FilesToCheck, Folders)
                    Else
                        Dim BaseSize = FilesToCheck.Count \ ThreadCount
                        Dim Remainder = FilesToCheck.Count Mod ThreadCount
                        Dim Index = 0
                        For i = 0 To ThreadCount - 1
                            Dim Size = BaseSize + If(i < Remainder, 1, 0)
                            Dim ThreadFiles = FilesToCheck.GetRange(Index, Size)
                            Index += Size
                            RunInNewThread(Sub() CheckExistingFiles(ThreadFiles, Folders), $"下载 文件复制 {Uuid}/{GetUuid()}")
                        Next
                    End If
                Catch ex As Exception
                    OnFail(New List(Of Exception) From {New Exception("下载初始化失败", ex)})
                End Try
            End Sub, "L/下载 " & Uuid)
        End Sub
        Private Sub CheckExistingFiles(Files As List(Of NetFile), FolderList As List(Of String))
            Try
                If ModeDebug Then Log($"[Download] 文件检查线程已启动，分配的文件数：{Files.Count}")
                '列出 MC 文件夹中的各个版本文件夹
                Dim VersionFolders As New List(Of String)
                For Each McFolder In FolderList
                    Dim VersionsFolder As New DirectoryInfo(McFolder & "versions\")
                    If VersionsFolder.Exists() Then
                        For Each VersionFolder In VersionsFolder.GetDirectories
                            VersionFolders.Add(VersionFolder.FullName & "\")
                        Next
                    End If
                Next
                '处理每个文件
                For Each File As NetFile In Files
                    Dim Target As String = CheckExistingFile(FolderList, VersionFolders, File)
                    If File.State >= NetState.WaitingToDownload Then Return '中断
                    If Target Is Nothing Then
                        '未找到相同文件
                        SyncLock LockState
                            File.State = NetState.WaitingToDownload
                            File.IsCopy = False
                        End SyncLock
                    Else
                        '已找到相同文件
                        File.IsCopy = True
                        Dim RetryCount As Integer = 0
Retry:
                        Try
                            If Target <> File.LocalPath Then
                                Log($"[Download] 复制已存在的文件：{Target} → {File.LocalPath}")
                                CopyFile(Target, File.LocalPath)
                            End If
                            File.Finish(False)
                        Catch ex As Exception
                            RetryCount += 1
                            Log(ex, $"复制已存在的文件失败，第 {RetryCount} 次重试（{Target} → {File.LocalPath}）")
                            If RetryCount < 3 Then
                                Thread.Sleep(200)
                                GoTo Retry
                            End If
                            '失败，回退到下载
                            SyncLock LockState
                                File.State = NetState.WaitingToDownload
                                File.IsCopy = False
                            End SyncLock
                        End Try
                    End If
                Next
            Catch ex As Exception
                OnFail(New List(Of Exception) From {New Exception("下载已存在文件查找失败", ex)})
            End Try
        End Sub
        Private Function CheckExistingFile(FolderList As List(Of String), VersionFolders As List(Of String), File As NetFile) As String
            '目标文件已存在
            If File.Check.Check(File.LocalPath) Is Nothing Then Return File.LocalPath
            '没有可用的检查规则，只能开始下载
            If File.Check.Hash Is Nothing AndAlso File.Check.ActualSize < 0 Then Return Nothing
            '大致判断文件类别
            Dim TypeIndexes =
                {"\assets\", "\libraries\", "\versions\", "\mods\", "\coremods\", "\lib\", "\resourcepacks\", "\texturepacks\", "\shaderpacks\"}.
                Select(Function(FolderName) (FolderName, File.LocalPath.IndexOfF(FolderName, True))).
                Where(Function(kv) kv.Item2 >= 0).ToList
            If Not TypeIndexes.Any Then
                If File.LocalName.EndsWithF(".jar") Then
                    TypeIndexes.Add(("\versions\", 1)) '总是对 jar 进行版本文件检查，以包括另存为 jar 的情况
                Else
                    Return Nothing
                End If
            End If
            Dim Type = TypeIndexes.MaxOrDefault(Function(kv) kv.Item2).FolderName.TrimStart("\"c)
            '根据类别进行查找
            Select Case Type
                Case "assets\", "libraries\"
                    'assets/libraries：查找 MC 文件夹下的相同路径
                    For Each Folder In FolderList
                        Dim Candidate = Folder & Type & File.LocalPath.AfterFirst(Type)
                        If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                    Next
                Case "versions\"
                    '版本 jar 或 json：查找 MC 文件夹下的各个版本文件夹
                    For Each VersionFolder In VersionFolders
                        For Each Candidate In Directory.GetFiles(VersionFolder,
                            "*." & GetFileNameFromPath(File.LocalPath).AfterLast(".").ToLower, SearchOption.TopDirectoryOnly)
                            If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                        Next
                    Next
                Case Else
                    '社区资源
                    If File.Check.ActualSize < 0 OrElse File.Check.Hash Is Nothing Then Return Nothing '必须要求指定了文件大小和 Hash
                    For Each Folder In FolderList.Concat(VersionFolders)
                        Dim TargetFolder = Folder & Type
                        If Not Directory.Exists(TargetFolder) Then Continue For
                        For Each Candidate In Directory.GetFiles(TargetFolder)
                            '快速进行大小校验
                            Static Sizes As New SafeDictionary(Of String, Long)
                            If Not Sizes.ContainsKey(Candidate) Then Sizes(Candidate) = New FileInfo(Candidate).Length
                            If File.Check.ActualSize <> Sizes(Candidate) Then Continue For
                            'Hash 校验
                            If File.Check.Check(Candidate) Is Nothing Then Return Candidate
                        Next
                    Next
            End Select
            Return Nothing
        End Function

        Public Sub OnFileFinish(File As NetFile)
            '要求全部文件完成
            SyncLock FileRemainLock
                FileRemain -= 1
                If FileRemain > 0 Then Return
            End SyncLock
            OnFinish()
        End Sub
        Public Sub OnFinish()
            RaisePreviewFinish()
            SyncLock LockState
                If State > LoadState.Loading Then Return
                State = LoadState.Finished
            End SyncLock
        End Sub
        Public Sub OnFileFail(File As NetFile)
            '将下载源的错误加入主错误列表
            For Each Source In File.Sources
                If Not IsNothing(Source.Ex) Then File.Ex.Add(Source.Ex)
            Next
            OnFail(File.Ex)
        End Sub
        Public Sub OnFail(ExList As List(Of Exception))
            SyncLock LockState
                If State > LoadState.Loading Then Return
                If ExList Is Nothing OrElse Not ExList.Any() Then ExList = New List(Of Exception) From {New Exception("未知错误！")}
                '寻找第一个不是 404 的下载源
                Dim UsefulExs = ExList.Where(Function(e) Not e.Message.Contains("404 (")).ToList
                [Error] = If(UsefulExs.Any, UsefulExs(0), ExList(0))
                '获取实际失败的文件
                For Each File In Files
                    If File.State = NetState.Interrupted Then
                        [Error] = New Exception("文件下载失败：" & File.LocalPath & vbCrLf & Join(
                            File.Sources.Select(Function(s) If(s.Ex Is Nothing, s.Url, s.Ex.Message & "（" & s.Url & "）")), vbCrLf), [Error])
                        Exit For
                    End If
                Next
                '在设置 Error 对象后再更改为失败，避免 WaitForExit 无法捕获错误
                State = LoadState.Failed
            End SyncLock
            '中断所有文件
            For Each TaskFile In Files
                If TaskFile.State < NetState.Merging Then TaskFile.State = NetState.Interrupted
            Next
            '在退出同步锁后再进行日志输出
            Dim ErrOutput As New List(Of String)
            For Each Ex As Exception In ExList
                ErrOutput.Add(Ex.Message)
            Next
            Log("[Download] " & Join(ErrOutput.Distinct.ToArray, vbCrLf))
        End Sub
        Public Overrides Sub Abort()
            SyncLock LockState
                If State >= LoadState.Finished Then Return
                State = LoadState.Aborted
            End SyncLock
            Log("[Download] " & Name & " 已取消！")
            '中断所有文件
            For Each TaskFile In Files
                TaskFile.Abort(Me)
            Next
        End Sub

    End Class

    ''' <summary>
    ''' 下载单个 UNC 文件的加载器。
    ''' </summary>
    Public Class LoaderDownloadUnc
        Inherits LoaderBase
        ''' <summary>
        ''' UNC 路径。
        ''' </summary>
        Public Unc As String
        ''' <summary>
        ''' 保存路径。
        ''' </summary>
        Public SavePath As String
        ''' <summary>
        ''' 下载线程。
        ''' </summary>
        Private DlThread As Thread
        Public Sub New(Name As String, File As Tuple(Of String, String))
            Me.Name = Name
            Unc = File.Item1
            SavePath = File.Item2
        End Sub
        Public Overrides Sub Start(Optional Input As Object = Nothing, Optional IsForceRestart As Boolean = False)
            If Input IsNot Nothing Then
                Unc = Input.Item1
                SavePath = Input.Item2
            End If
            State = LoadState.Loading
            Directory.CreateDirectory(GetPathFromFullPath(SavePath))
            DlThread = RunInNewThread(AddressOf DownloadThread, "Download UNC File")
        End Sub
        Private Sub DownloadThread()
            Try
                Dim fileInfo As New FileInfo(Unc)
                Dim totalBytes As Long = fileInfo.Length
                Dim bytesRead As Long = 0

                Dim tempFile As String = PathTemp & Uuid & "\" & GetFileNameFromPath(SavePath)
                Directory.CreateDirectory(GetPathFromFullPath(tempFile))
                If File.Exists(tempFile) Then File.Delete(tempFile)
                Using sourceStream As New FileStream(Unc, FileMode.Open, FileAccess.Read)
                    Using destStream As New FileStream(tempFile, FileMode.Create, FileAccess.Write)
                        Dim buffer(81920) As Byte '80KB 缓冲区
                        Dim currentBytesRead As Integer

                        Do
                            currentBytesRead = sourceStream.Read(buffer, 0, buffer.Length)
                            destStream.Write(buffer, 0, currentBytesRead)
                            bytesRead += currentBytesRead

                            Progress = bytesRead / totalBytes
                        Loop While currentBytesRead > 0 AndAlso State = LoadState.Loading
                    End Using
                End Using
                If State > LoadState.Loading Then Return
                CopyFile(tempFile, SavePath)
                If State = LoadState.Loading Then State = LoadState.Finished
            Catch ex As ThreadAbortException
            End Try
        End Sub

        Public Overrides Sub Abort()
            If State >= LoadState.Finished Then Return
            State = LoadState.Aborted
            Log("[Download] " & Name & " 已取消！")
        End Sub
    End Class
    Public NetManager As New NetManagerClass
    ''' <summary>
    ''' 下载文件管理。
    ''' </summary>
    Public Class NetManagerClass

#Region "属性"

        ''' <summary>
        ''' 需要下载的文件。为“本地地址 - 文件对象”键值对。
        ''' </summary>
        Public Files As New Dictionary(Of String, NetFile)
        Public ReadOnly LockFiles As New Object

        ''' <summary>
        ''' 当前的所有下载任务。
        ''' </summary>
        Public Tasks As New SafeList(Of LoaderDownload)

        ''' <summary>
        ''' 已下载完成的大小。
        ''' </summary>
        Public Property DownloadDone As Long
            Get
                Return _DownloadDone
            End Get
            Set(value As Long)
                SyncLock LockDone
                    _DownloadDone = value
                End SyncLock
            End Set
        End Property
        Private _DownloadDone As Long = 0
        Private ReadOnly LockDone As New Object


        ''' <summary>
        ''' 尚未完成下载的文件数。
        ''' </summary>
        Public FileRemain As Integer = 0
        Public ReadOnly LockRemain As New Object
        
        '这些属性由 RefreshStat 刷新
        ''' <summary>
        ''' 当前的全局下载速度，单位为 Byte / 秒。
        ''' </summary>
        Public Speed As Long = 0

        Public ReadOnly Uuid As Integer = GetUuid()

#End Region

        ''' <summary>
        ''' 进度与下载速度由任务管理线程每隔约 0.1 秒刷新一次。
        ''' </summary>
        Private Sub RefreshStat()
            Try
                Dim DeltaTime As Long = TimeUtils.GetTimeTick() - RefreshStatLast
                If DeltaTime = 0 Then Return
                RefreshStatLast += DeltaTime
#Region "刷新整体速度"
                '计算瞬时速度
                Static SpeedLast As New List(Of Long) '记录至多最近 30 次下载速度的记录，较新的在前面
                Static SpeedLastDone As Long = 0 '上次记速时的已下载大小
                Dim ActualSpeed As Double = Math.Max(0, (DownloadDone - SpeedLastDone) / (DeltaTime / 1000))
                SpeedLast.Insert(0, ActualSpeed)
                If SpeedLast.Count >= 31 Then SpeedLast.RemoveAt(30)
                SpeedLastDone = DownloadDone
                '计算用于显示的速度
                Dim SpeedSum As Long = 0, SpeedDiv As Long = 0, Weight = SpeedLast.Count
                For Each SpeedRecord In SpeedLast
                    SpeedSum += SpeedRecord * Weight
                    SpeedDiv += Weight
                    Weight -= 1
                Next
                Speed = If(SpeedDiv > 0, SpeedSum / SpeedDiv, 0)
                '计算新的速度下限
                Dim Limit As Long = 0
                If SpeedLast.Count >= 10 Then Limit = SpeedLast.Take(10).Average * 0.85 '取近 1 秒的平均速度的 85%
                If Limit > NetTaskSpeedLimitLow Then
                    NetTaskSpeedLimitLow = Limit
                    Log("[Download] " & "速度下限已提升到 " & GetString(Limit))
                End If
#End Region
#Region "刷新下载任务属性"
                For Each Task In Tasks
                    Task.RefreshStat()
                Next
#End Region
            Catch ex As Exception
                Log(ex, "刷新下载公开属性失败")
            End Try
        End Sub
        Private RefreshStatLast As Long

        ''' <summary>
        ''' 启动监控线程，用于新增下载线程。
        ''' </summary>
        Private Sub StartManager()
            Static IsStarted As Boolean = False
            If IsStarted Then Return
            IsStarted = True
            Dim ThreadStarter =
            Sub(Id As Integer) '0 或 1
                Try
                    While True
                        Thread.Sleep(20)
                        '获取文件列表
                        Dim AllFiles As List(Of NetFile)
                        SyncLock LockFiles
                            If Id = 0 AndAlso FileRemain = 0 AndAlso Files.Any() Then Files.Clear() '若已完成，则清空
                            AllFiles = Files.Values.ToList()
                        End SyncLock
                        Dim WaitingFiles As New List(Of NetFile)
                        Dim OngoingFiles As New List(Of NetFile)
                        For Each File As NetFile In AllFiles
                            If File.Uuid Mod 2 = Id Then Continue For
                            If File.State = NetState.WaitingToDownload Then
                                WaitingFiles.Add(File)
                            ElseIf File.State < NetState.Merging Then
                                OngoingFiles.Add(File)
                            End If
                        Next
                        '为等待中的文件开始线程
                        For Each File As NetFile In WaitingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While '最大线程数检查
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) '减少 BMCLAPI 请求频率
                        Next
                        '为进行中的文件追加线程
                        If Speed >= NetTaskSpeedLimitLow Then Continue While '下载速度足够，无需新增
                        For Each File As NetFile In OngoingFiles
                            If NetTaskThreadCount >= NetTaskThreadLimit Then Continue While '最大线程数检查
                            '线程种类计数
                            Dim PreparingCount = 0, DownloadingCount = 0
                            If File.Threads IsNot Nothing Then
                                For Each Thread As NetThread In File.Threads.ToList
                                    If Thread.State < NetState.Downloading Then
                                        PreparingCount += 1
                                    ElseIf Thread.State = NetState.Downloading Then
                                        DownloadingCount += 1
                                    End If
                                Next
                            End If
                            '新增线程
                            If PreparingCount > DownloadingCount Then Continue For '准备中的线程已多于下载中的线程，不再新增
                            Dim NewThread = File.TryBeginThread()
                            If NewThread IsNot Nothing AndAlso NewThread.Source.Url.Contains("bmclapi") Then Thread.Sleep(100) '减少 BMCLAPI 请求频率
                        Next
                    End While
                Catch ex As Exception
                    Log(ex, $"任务管理启动线程 {Id} 出错", LogLevel.Critical)
                End Try
            End Sub
            RunInNewThread(Sub() ThreadStarter(0), "NetManager ThreadStarter 0")
            RunInNewThread(Sub() ThreadStarter(1), "NetManager ThreadStarter 1")
            RunInNewThread(
            Sub()
                Try
                    Dim NextTick As Long = GetTimeTick()
                    While True
                        '增加限速余量
                        If NetTaskSpeedLimitHigh > 0 Then NetTaskSpeedLimitLeft = NetTaskSpeedLimitHigh / 10
                        '刷新公开属性
                        RefreshStat()
                        '等待 100 ms
                        NextTick += 100
                        Dim SleepTime = NextTick - GetTimeTick()
                        If SleepTime > 0 Then
                            Thread.Sleep(SleepTime)
                        Else
                            NextTick = GetTimeTick() '超时，直接追帧，不等待
                        End If
                    End While
                Catch ex As Exception
                    Log(ex, "任务管理刷新线程出错", LogLevel.Critical)
                End Try
            End Sub, "NetManager StatRefresher")
        End Sub

        'Public FileRemainList As New List(Of String)
        Private IsDownloadCacheCleared As Boolean = False
        ''' <summary>
        ''' 开始一个下载任务。
        ''' </summary>
        Public Sub Start(Task As LoaderDownload)
            StartManager()
            '清理缓存
            If Not IsDownloadCacheCleared Then
                Try
                    DeleteDirectory(PathTemp & "Download")
                Catch ex As Exception
                    Log(ex, "清理下载缓存失败")
                End Try
                IsDownloadCacheCleared = True
            End If
            Directory.CreateDirectory(PathTemp & "Download")
            '文件处理
            SyncLock LockFiles
                '添加每个文件
                For i = 0 To Task.Files.Count - 1
                    Dim File = Task.Files(i)
                    If Files.ContainsKey(File.LocalPath) Then
                        '已有该文件
                        If Files(File.LocalPath).State >= NetState.Finished Then
                            '该文件已经下载过一次，且下载完成
                            '将已下载的文件替换成当前文件，重新下载
                            File.Tasks.Add(Task)
                            Files(File.LocalPath) = File
                            SyncLock LockRemain
                                FileRemain += 1
                                If ModeDebug Then Log("[Download] " & File.LocalName & "：已替换列表，剩余文件 " & FileRemain)
                                'FileRemainList.Add(File.LocalPath)
                            End SyncLock
                        Else
                            '该文件正在下载中
                            '将当前文件替换成下载中的文件，即两个任务指向同一个文件
                            File = Files(File.LocalPath)
                            File.Tasks.Add(Task)
                        End If
                    Else
                        '没有该文件
                        File.Tasks.Add(Task)
                        Files.Add(File.LocalPath, File)
                        SyncLock LockRemain
                            FileRemain += 1
                            If ModeDebug Then Log("[Download] " & File.LocalName & "：已加入列表，剩余文件 " & FileRemain)
                            'FileRemainList.Add(File.LocalPath)
                        End SyncLock
                    End If
                    Task.Files(i) = File '回设
                Next
            End SyncLock
            Tasks.Add(Task)
        End Sub

    End Class

    ''' <summary>
    ''' 是否有正在进行中、需要在任务管理页面显示的下载任务？
    ''' </summary>
    Public Function HasDownloadingTask(Optional IgnoreCustomDownload As Boolean = False) As Boolean
        For Each Task In LoaderTaskbar.ToList()
            If (Task.Show AndAlso Task.State = LoadState.Loading) AndAlso
               (Not IgnoreCustomDownload OrElse Not Task.Name.ToString.Contains("自定义下载")) Then
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>
    ''' 安全获取路径所在的根盘符（如 "C:\"），仅支持本地绝对路径。
    ''' 若路径无效或非本地盘，返回 Nothing。
    ''' </summary>
    Private Function TryGetLocalDriveRoot(path As String) As String
        If String.IsNullOrEmpty(path) Then Return Nothing
        Try
            Dim root = IO.Path.GetPathRoot(path)
            ' 仅接受 X:\ 格式（长度为3，第二个字符是冒号）
            If root?.Length = 3 AndAlso root(1) = ":"c AndAlso root(2) = "\"c Then
                Return root.ToUpperInvariant()
            End If
        Catch
            ' 路径非法（如包含通配符、相对路径、UNC 等）
        End Try
        Return Nothing
    End Function
End Module
