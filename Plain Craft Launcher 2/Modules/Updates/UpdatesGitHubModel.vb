Imports System.IO.Compression
Imports Newtonsoft.Json
Imports PCL.Core.Utils

Public Class UpdatesGitHubModel
    Implements IUpdateSource

    Private Const NightlyJsonUrl As String = "https://github.com/PCL-Community/PCL2-CE/releases/download/nightly/nightly.json"
    ' Private Const NightlyJsonUrl As String = "https://r2.230225.xyz/nightly.json"
    Private _nightlyInfo As NightlyJsonModel

    Public Property SourceName As String Implements IUpdateSource.SourceName

    Public Function IsAvailable() As Boolean Implements IUpdateSource.IsAvailable
        Return True ' Assume network is available
    End Function

    Public Function RefreshCache() As Boolean Implements IUpdateSource.RefreshCache
        Try
            Dim jsonContent = NetGetCodeByRequestRetry(NightlyJsonUrl)
            _nightlyInfo = JsonConvert.DeserializeObject(Of NightlyJsonModel)(jsonContent)
            Return _nightlyInfo IsNot Nothing
        Catch ex As Exception
            Log(ex, "[Update] Failed to fetch nightly.json from GitHub")
            Return False
        End Try
    End Function

    Public Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel Implements IUpdateSource.GetLatestVersion
        If channel <> UpdateChannel.nightly Then Throw New NotSupportedException("UpdatesGitHubModel only supports the Nightly channel.")
        If _nightlyInfo Is Nothing AndAlso Not RefreshCache() Then Throw New InvalidOperationException("Failed to get nightly update info.")

        Dim asset = GetAssetForArch(arch)

        Return New VersionDataModel With {
            .VersionName = _nightlyInfo.version,
            .VersionCode = _nightlyInfo.version_code,
            .SHA256 = asset.sha256,
            .Source = SourceName,
            .Changelog = _nightlyInfo.changelog
        }
    End Function

    Public Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean Implements IUpdateSource.IsLatest
        If channel <> UpdateChannel.nightly Then Throw New NotSupportedException("UpdatesGitHubModel only supports the Nightly channel.")
        If _nightlyInfo Is Nothing AndAlso Not RefreshCache() Then Return True ' If we can't get info, assume we're latest to avoid errors

        Return currentVersionCode >= _nightlyInfo.version_code
    End Function

    Public Function GetAnnouncementList() As VersionAnnouncementDataModel Implements IUpdateSource.GetAnnouncementList
        Throw New Exception("GitHub Nightly 无公告系统")
    End Function

    Public Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase) Implements IUpdateSource.GetDownloadLoader
        If channel <> UpdateChannel.nightly Then Throw New NotSupportedException("UpdatesGitHubModel only supports the Nightly channel.")
        If _nightlyInfo Is Nothing AndAlso Not RefreshCache() Then Throw New InvalidOperationException("Failed to get nightly update info for download.")

        Dim asset = GetAssetForArch(arch)
        Dim downloadUrl = asset.download_url
        Dim tempPath = IO.Path.Combine(PathTemp, "Cache", "Update", "Download", $"{asset.sha256}.zip")

        Dim loaders As New List(Of LoaderBase)
        ' 1. 下载更新包
        loaders.Add(New LoaderDownload("下载更新", New List(Of NetFile) From {New NetFile({downloadUrl}, tempPath)}))

        ' 2. 解压更新包，将 .exe 文件提取到 ModSecret 指定的 output 路径
        loaders.Add(New LoaderTask(Of String, Integer)("提取文件", Sub()
            Using fs As New IO.FileStream(tempPath, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.Read)
                Using zip As New ZipArchive(fs)
                    Dim entry = zip.Entries.FirstOrDefault(Function(x) x.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    If entry Is Nothing Then Throw New InvalidOperationException("在下载的更新包中找不到可执行文件。")
                    entry.ExtractToFile(output, True)
                End Using
            End Using
        End Sub))
        Return loaders
    End Function

    Private Function GetAssetForArch(arch As UpdateArch) As NightlyAsset
        Dim archName = If(arch = UpdateArch.arm64, "arm64", "x64")
        Dim asset = _nightlyInfo.assets.FirstOrDefault(Function(a) a.arch.Equals(archName, StringComparison.OrdinalIgnoreCase))
        If asset Is Nothing Then Throw New InvalidOperationException($"Nightly build for architecture {archName} not found.")
        Return asset
    End Function

End Class

Public Class NightlyJsonModel
    Public Property version As String
    Public Property version_code As Integer
    Public Property changelog As String
    Public Property assets As List(Of NightlyAsset)
End Class

Public Class NightlyAsset
    Public Property arch As String
    Public Property download_url As String
    Public Property sha256 As String
End Class
