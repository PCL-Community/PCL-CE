Imports PCL.Core.Helper

Public Class UpdatesWrapperModel
    Implements IUpdateSource

    Private _sources As IEnumerable(Of IUpdateSource)
    Private _versionSource As IUpdateSource
    Private _announcementSource As IUpdateSource

    Sub New(sources As IEnumerable(Of IUpdateSource))
        _sources = sources
    End Sub
    Public Property SourceName As String Implements IUpdateSource.SourceName
        Get
            Return If(_versionSource?.SourceName, "")
        End Get
        Set(value As String)
            If _versionSource Is Nothing Then Return
            _versionSource.SourceName = value
        End Set
    End Property

    Public Function IsAvailable() As Boolean Implements IUpdateSource.IsAvailable
        Return _sources.Any(Function(x) x.IsAvailable())
    End Function

    Public Function RefreshCache() As Boolean Implements IUpdateSource.RefreshCache
        For Each item In _sources
            Try
                item.RefreshCache()
                _versionSource = item
                Exit For
            Catch ex As Exception
                Log(ex, $"[Update] {item.SourceName} 暂不可用")
            End Try
        Next
        Return _versionSource IsNot Nothing
    End Function

    Public Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel Implements IUpdateSource.GetLatestVersion
        For Each item In _sources
            Try
                If _versionSource IsNot Nothing Then
                    Try
                        Return _versionSource.GetLatestVersion(channel, arch)
                    Catch ex As Exception
                        Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用")
                    End Try
                End If
                Dim ret = item.GetLatestVersion(channel, arch)
                _versionSource = item
                Return ret
            Catch ex As Exception
                Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息")
            End Try
        Next
        Log("[Update] 错误！所有的版本源都无法使用！")
        Throw New Exception("获取版本信息失败")
    End Function

    Public Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean Implements IUpdateSource.IsLatest
        For Each item In _sources
            Try
                If _versionSource IsNot Nothing Then
                    Try
                        Return _versionSource.IsLatest(channel, arch, currentVersion, currentVersionCode)
                    Catch ex As Exception
                        Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用")
                    End Try
                End If
                Dim ret = item.IsLatest(channel, arch, currentVersion, currentVersionCode)
                _versionSource = item
                Return ret
            Catch ex As Exception
                Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息")
            End Try
        Next
        Log("[Update] 错误！所有的版本源都无法使用！")
        Throw New Exception("获取版本信息失败")
    End Function

    Public Function GetAnnouncementList() As VersionAnnouncementDataModel Implements IUpdateSource.GetAnnouncementList
        For Each item In _sources
            Try
                If _announcementSource IsNot Nothing Then
                    Try
                        Return _announcementSource.GetAnnouncementList()
                    Catch ex As Exception
                        Log(ex, $"[Update] 缓存的公告源 {_announcementSource.SourceName} 不可用")
                    End Try
                End If
                Dim ret = item.GetAnnouncementList()
                _announcementSource = item
                Return ret
            Catch ex As Exception
                Log(ex, $"[Update] {item.SourceName} 无法获取最新公告信息")
            End Try
        Next
        Log("[Update] 错误！所有的公告源都无法使用！")
        Throw New Exception("获取公告信息失败")
    End Function

    Public Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase) Implements IUpdateSource.GetDownloadLoader
        For Each item In _sources
            Try
                If _versionSource IsNot Nothing Then
                    Try
                        Return _versionSource.GetDownloadLoader(channel, arch, output)
                    Catch ex As Exception
                        Log(ex, $"[Update] 缓存的版本源 {_versionSource.SourceName} 不可用")
                    End Try
                End If
                Dim ret = item.GetDownloadLoader(channel, arch, output)
                _versionSource = item
                Return ret
            Catch ex As Exception
                Log(ex, $"[Update] {item.SourceName} 无法获取最新版本信息")
            End Try
        Next
        Log("[Update] 错误！所有的版本源都无法使用！")
        Throw New Exception("获取版本信息失败")
    End Function
End Class
