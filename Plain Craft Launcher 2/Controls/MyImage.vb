Imports System.Net.Http
Imports PCL.Core.IO.Net.Http.Client.Request
Imports PCL.Core.Utils
Imports PCL.Core.Utils.Exts

Public Class MyImage
    Inherits Image

#Region "公开属性"

    ''' <summary>
    ''' 网络图片的缓存有效期。
    ''' 在这个时间后，才会重新尝试下载图片。
    ''' </summary>
    Public FileCacheExpiredTime As TimeSpan = TimeSpan.FromDays(14)

    ''' <summary>
    ''' 是否允许将网络图片存储到本地用作缓存。
    ''' </summary>
    Public Property EnableCache As Boolean
        Get
            Return GetValue(EnableCacheProperty)
        End Get
        Set(value As Boolean)
            SetValue(EnableCacheProperty, value)
        End Set
    End Property
    Public Shared Shadows ReadOnly EnableCacheProperty As DependencyProperty = DependencyProperty.Register(
        "EnableCache", GetType(Boolean), GetType(MyImage), New PropertyMetadata(True))

    ''' <summary>
    ''' 与 Image 的 Source 类似。
    ''' 若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    ''' 支持 WebP 格式的图片。
    ''' </summary>
    Public Shadows Property Source As String '覆写 Image 的 Source 属性
        Get
            Return _Source
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _Source = value Then Return
            _Source = value
            If Not IsInitialized Then Return '属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
            Load()
        End Set
    End Property
    Private _Source As String = ""
    Public Shared Shadows ReadOnly SourceProperty As DependencyProperty = DependencyProperty.Register(
        "Source", GetType(String), GetType(MyImage), New PropertyMetadata(New PropertyChangedCallback(
    Sub(sender, e) If sender IsNot Nothing Then CType(sender, MyImage).Source = e.NewValue.ToString())))

    ''' <summary>
    ''' 当 Source 首次下载失败时，会从该备用地址加载图片。
    ''' </summary>
    Public Property FallbackSource As String
        Get
            Return _FallbackSource
        End Get
        Set(value As String)
            _FallbackSource = value
        End Set
    End Property
    Private _FallbackSource As String = Nothing

    ''' <summary>
    ''' 正在下载网络图片时显示的本地图片。
    ''' </summary>
    Public Property LoadingSource As String
        Get
            Return _LoadingSource
        End Get
        Set(value As String)
            _LoadingSource = value
        End Set
    End Property
    Private _LoadingSource As String = "pack://application:,,,/images/Icons/NoIcon.png"

    Public Property CornerRadius As CornerRadius
        Get
            Return GetValue(CornerRadiusProperty)
        End Get
        Set(value As CornerRadius)
            SetValue(CornerRadiusProperty, value)
        End Set
    End Property
    Private Shared ReadOnly CornerRadiusProperty As DependencyProperty = DependencyProperty.Register(
        "CornerRadius",
        GetType(CornerRadius),
        GetType(MyImage),
        New FrameworkPropertyMetadata(
            New CornerRadius(-1),
            AddressOf OnCornerRadiusChanged)
        )

    Private Shared Sub OnCornerRadiusChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        DirectCast(d, MyImage).UpdateClip()
    End Sub

    Private Sub UpdateClip() Handles Me.SizeChanged
        If (ActualWidth > 0 AndAlso ActualHeight > 0) AndAlso
            (CornerRadius.TopLeft >= 0 AndAlso CornerRadius.TopRight >= 0) Then
            Clip = New RectangleGeometry(
                New Rect(0, 0, ActualWidth, ActualHeight),
                CornerRadius.TopLeft,
                CornerRadius.TopRight)
        End If
    End Sub

#End Region

    ''' <summary>
    ''' 实际被呈现的图片地址。
    ''' </summary>
    Public Property ActualSource As String
        Get
            Return _ActualSource
        End Get
        Set(value As String)
            If value = "" Then value = Nothing
            If _ActualSource = value Then Return
            _ActualSource = value
            Dispatcher.BeginInvoke(Async Function() As Task
                Try
                    Dim bitmap As ImageSource = If(value Is Nothing, Nothing, Await Task.Run(Function() New MyBitmap(value))) '在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
                    MyBase.Source = bitmap
                Catch ex As Exception
                    Log(ex, $"加载图片失败（{value}）")
                    Try
                        If value.StartsWithF(PathTemp) AndAlso File.Exists(value) Then File.Delete(value)
                    Catch 'ignored
                    End Try
                End Try
            End Function)
        End Set
    End Property
    Private _ActualSource As String = Nothing

    Private Sub Load() Handles Me.Initialized '属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
        '空
        If Source Is Nothing Then
            ActualSource = Nothing
            Return
        End If
        '本地图片
        If Not Source.StartsWithF("http") Then
            ActualSource = Source
            Return
        End If
        '从缓存加载网络图片
        Dim Url As String = Source
        Dim TempPath As String = GetTempPath(Url)
        Dim TempFile As New FileInfo(TempPath)
        Dim EnableCache As Boolean = Me.EnableCache
        If EnableCache AndAlso TempFile.Exists Then
            ActualSource = TempPath
            If (Date.Now - TempFile.LastWriteTime) < FileCacheExpiredTime Then Return '无需刷新缓存
        End If

        Dispatcher.BeginInvoke(
            Async Function() As Task
                Try
                    '下载
                    ActualSource = LoadingSource '显示加载中图片

                    Dim resp = Await DownloadImageAsync(Url)
                    If Not String.IsNullOrEmpty(resp) Then
                        ActualSource = resp
                        Return
                    End If

                    resp = Await DownloadImageAsync(FallbackSource)
                    If Not String.IsNullOrEmpty(resp) Then
                        ActualSource = resp
                        Return
                    End If

                Catch ex As Exception
                    '更换备用地址
                    Log(ex, $"Online image get fail（source = {Url}, fallback = {FallbackSource}）", LogLevel.Developer)
                    '从缓存加载网络图片
                    TempPath = GetTempPath(Url)
                    TempFile = New FileInfo(TempPath)
                    If EnableCache AndAlso TempFile.Exists() Then
                        ActualSource = TempPath
                        If (Date.Now - TempFile.LastWriteTime) < FileCacheExpiredTime Then Return '无需刷新缓存
                    End If
                End Try
            End Function)
    End Sub
    Public Shared Function DownloadImageAsync(url As String) As Task(Of String)
        Return _downloadTasks.GetOrAdd(
            url,
            Function(key)
                Dim t = DownloadImageInternelAsync(key)
                t.ContinueWith(
                        Sub()
                            _downloadTasks.Remove(url, Nothing)
                        End Sub)
                Return t
            End Function)
    End Function
    Public Shared Function GetTempPath(Url As String) As String
        Return IO.Path.Combine(PathTemp, "Cache", "Images", $"{GetStringMD5(Url)}.png")
    End Function

    Private Shared ReadOnly _downloadTasks As New Concurrent.ConcurrentDictionary(Of String, Task(Of String))
    Private Shared Async Function DownloadImageInternelAsync(url As String) As Task(Of String)
        Dim tempPath = GetTempPath(url)
        Dim TempDownloadingPath = tempPath & RandomUtils.NextInt(0, 1000000)

        Try
            Directory.CreateDirectory(GetPathFromFullPath(tempPath)) '重新实现下载，以避免携带 Header（#5072）
            Using fs As New FileStream(TempDownloadingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read)
                Using response = Await HttpRequest.Create(url).
                        WithHttpVersionOption(HttpVersion.Version30).
                        SendAsync(addMetedata:=False)
                    response.EnsureSuccessStatusCode()

                    Using nfs = Await response.AsStreamAsync()
                        fs.SetLength(0)
                        Await nfs.CopyToAsync(fs)
                    End Using
                End Using
            End Using

            File.Move(TempDownloadingPath, tempPath, True)
            Return tempPath
        Catch ex As Exception
            If File.Exists(tempPath) Then File.Delete(tempPath)
            If File.Exists(TempDownloadingPath) Then File.Delete(TempDownloadingPath)

            Log(ex, $"Try to get online image fail (url = {url}, dest = {tempPath})")
            Return String.Empty
        End Try
    End Function


End Class