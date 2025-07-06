Imports System.IO.Compression
Imports PCL.Core.Helper
Imports PCL.Core.Helper.Diff
Public Class UpdatesMinioModel '社区自己的更新系统格式
    Implements IUpdateSource

    Property SourceName As String Implements IUpdateSource.SourceName
    Private _baseUrl As String
    Public Sub New(BaseUrl As String, Optional Name As String = "Minio")
        _baseUrl = BaseUrl
        SourceName = Name
    End Sub
    Public Function IsAvailable() As Boolean Implements IUpdateSource.IsAvailable
        Return Not String.IsNullOrWhiteSpace(_baseUrl)
    End Function

    Private _remoteCache As Dictionary(Of String, String)

    Public Function RefreshCache() As Boolean Implements IUpdateSource.RefreshCache
        '先检查缓存
        Dim remoteCache = JToken.Parse(NetGetCodeByRequestRetry($"{_baseUrl}apiv2/cache.json"))
        _remoteCache = remoteCache.ToObject(Of Dictionary(Of String, String))
        Return True
    End Function

    Public Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel Implements IUpdateSource.GetLatestVersion
        If _remoteCache Is Nothing Then RefreshCache()
        '确定版本通道名称
        Return GetChannelInfo(channel, arch)
    End Function

    Public Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean Implements IUpdateSource.IsLatest
        If _remoteCache Is Nothing Then RefreshCache()
        Dim latestVersion = GetChannelInfo(channel, arch)
        Return currentVersionCode >= latestVersion.VersionCode
    End Function

    Public Function GetAnnouncementList() As VersionAnnouncementDataModel Implements IUpdateSource.GetAnnouncementList
        If _remoteCache Is Nothing Then RefreshCache()
        Dim deJsonData = GetRemoteInfoByName("announcement")?.ToObject(Of VersionAnnouncementDataModel)
        If deJsonData Is Nothing Then Throw New NullReferenceException("Can not get remote announcement info!")
        Return deJsonData
    End Function

    Private Function GetChannelInfo(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel
        Dim channelName = GetChannelName(channel, arch)
        Dim deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")?.ToObject(Of MinioUpdateModel).assets.FirstOrDefault()
        If deJsonData Is Nothing Then Throw New NullReferenceException("Can not get remote update info!")
        Return New VersionDataModel With {
            .VersionName = deJsonData.version.name,
            .VersionCode = deJsonData.version.code,
            .SHA256 = deJsonData.sha256,
            .Source = SourceName,
            .Changelog = deJsonData.changelog
        }
    End Function

    Private Function GetRemoteInfoByName(name As String, Optional path As String = "") As JToken
        Dim localInfoFile = IO.Path.Combine(PathTemp, "Cache", "Update", $"{name}.json")
        Dim jsonData As JToken
        If IsCacheValid($"{name}.json", _remoteCache(name)) Then
            jsonData = JToken.Parse(ReadFile(localInfoFile))
        Else
            Dim httpRet = NetGetCodeByRequestRetry($"{_baseUrl}apiv2/{path}{name}.json")
            jsonData = JToken.Parse(httpRet)
            WriteFile(localInfoFile, httpRet)
        End If
        Return jsonData
    End Function

    ''' <summary>
    ''' 缓存是否有效
    ''' </summary>
    ''' <param name="path"></param>
    ''' <param name="hash"></param>
    ''' <returns></returns>
    Private Function IsCacheValid(path As String, hash As String) As Boolean
        Dim cacheFile = IO.Path.Combine(PathTemp, "Cache", "Update", path)
        Dim fileInfo As New FileInfo(cacheFile)
        Return fileInfo.Exists AndAlso (DateTime.Now - fileInfo.LastWriteTime).Hours < 1 AndAlso GetFileMD5(cacheFile) = hash
    End Function

    Private Function GetChannelName(channel As UpdateChannel, arch As UpdateArch) As String
        Dim ChannelName As String = String.Empty
        Select Case channel
            Case UpdateChannel.stable
                ChannelName += "sr"
            Case UpdateChannel.beta
                ChannelName += "fr"
            Case Else
                ChannelName += "sr"
        End Select
        Select Case arch
            Case UpdateArch.x64
                ChannelName += "x64"
            Case UpdateArch.arm64
                ChannelName += "arm64"
            Case Else
                ChannelName += "x64"
        End Select
        Return ChannelName
    End Function

    Public Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase) Implements IUpdateSource.GetDownloadLoader
        If _remoteCache Is Nothing Then RefreshCache()
        Dim loaders As New List(Of LoaderBase)
        Dim patchUpdate As Boolean = True
        Dim tempPath = $"{PathTemp}Cache\Update\Download\"
        loaders.Add(New LoaderTask(Of Integer, List(Of NetFile))("获取版本信息", Sub(load As LoaderTask(Of Integer, List(Of NetFile)))
                                                                               Dim channelName = GetChannelName(channel, arch)
                                                                               Dim deJsonData = GetRemoteInfoByName($"updates-{channelName}", "updates/")?.ToObject(Of MinioUpdateModel).assets.FirstOrDefault()
                                                                               If deJsonData Is Nothing Then Throw New Exception("No assets can download!")
                                                                               Dim selfSha256 = GetFileSHA256(PathWithName)
                                                                               Dim remoteUpdSha256 = deJsonData.sha256
                                                                               Dim patchFileName = $"{selfSha256}_{remoteUpdSha256}.patch"
                                                                               If deJsonData.patches.Contains(patchFileName) Then
                                                                                   patchUpdate = True
                                                                                   tempPath += patchFileName
                                                                                   load.Output = New List(Of NetFile) From {
                                                                                   New NetFile(
                                                                                   {$"{_baseUrl}static/patch/{patchFileName}"},
                                                                                   tempPath)
                                                                                   }
                                                                               Else
                                                                                   patchUpdate = False

                                                                                   tempPath += $"{deJsonData.sha256}.bin"
                                                                                   load.Output = New List(Of NetFile) From {
                                                                                   New NetFile(
                                                                                   Shuffle(deJsonData.downloads),
                                                                                   tempPath)
                                                                                   }
                                                                               End If
                                                                           End Sub))
        loaders.Add(New LoaderDownload("下载文件", New List(Of NetFile)))
        loaders.Add(New LoaderTask(Of String, Integer)("应用文件", Sub()
                                                                   If patchUpdate Then
                                                                       Dim diff As New BsDiff()
                                                                       Dim newFile = diff.Apply(ReadFileBytes(PathWithName), ReadFileBytes(tempPath)).GetAwaiter().GetResult()
                                                                       WriteFile(output, newFile)
                                                                   Else
                                                                       Using fs As New FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                                                                           Using zip As New ZipArchive(fs)
                                                                               Dim entry = zip.Entries.Where(Function(x) x.Name.Contains("Plain Craft Launcher Community Edition.exe")).FirstOrDefault()
                                                                               If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains("Plain Craft Launcher")).FirstOrDefault()
                                                                               If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains("Launcher")).FirstOrDefault()
                                                                               If entry Is Nothing Then entry = zip.Entries.Where(Function(x) x.Name.Contains(".exe")).FirstOrDefault()
                                                                               If entry Is Nothing Then Throw New Exception("找不到更新文件")
                                                                               entry.ExtractToFile(output, True)
                                                                           End Using
                                                                       End Using
                                                                   End If
                                                               End Sub))
        Return loaders
    End Function

    Private Class MinioUpdateModel
        Public Property assets As List(Of MinioUpdateAsset)
    End Class

    Private Class MinioUpdateAsset
        Public Property file_name As String
        Public Property version As MinioUpdateAssetVersionInfo
        Public Property upd_time As String
        Public Property downloads As List(Of String)
        Public Property patches As List(Of String)
        Public Property sha256 As String
        Public Property changelog As String
    End Class

    Private Class MinioUpdateAssetVersionInfo
        Public Property channel As String
        Public Property name As String
        Public Property code As Integer
    End Class
End Class
