Imports PCL.Core.Helper

Public Class UpdatesRandomModel '社区自己的更新系统格式
    Implements IUpdateSource

    Private _sources As IEnumerable(Of IUpdateSource)
    Private _randIndex As Integer
    Public Sub New(Sources As IEnumerable(Of IUpdateSource))
        _sources = Sources
        Dim rand As New Random(DateTime.Now.Millisecond)
        _randIndex = rand.Next(0, _sources.Count() - 1)
    End Sub

    Property SourceName As String Implements IUpdateSource.SourceName
        Get
            Return _sources.ElementAt(_randIndex).SourceName
        End Get
        Set(value As String)
            _sources.ElementAt(_randIndex).SourceName = value
        End Set
    End Property

    Public Function IsAvailable() As Boolean Implements IUpdateSource.IsAvailable
        Return _sources.ElementAt(_randIndex).IsAvailable()
    End Function

    Public Function RefreshCache() As Boolean Implements IUpdateSource.RefreshCache
        Return _sources.ElementAt(_randIndex).RefreshCache()
    End Function

    Public Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel Implements IUpdateSource.GetLatestVersion
        Return _sources.ElementAt(_randIndex).GetLatestVersion(channel, arch)
    End Function

    Public Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean Implements IUpdateSource.IsLatest
        Return _sources.ElementAt(_randIndex).IsLatest(channel, arch, currentVersion, currentVersionCode)
    End Function

    Public Function GetAnnouncementList() As VersionAnnouncementDataModel Implements IUpdateSource.GetAnnouncementList
        Return _sources.ElementAt(_randIndex).GetAnnouncementList()
    End Function

    Public Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase) Implements IUpdateSource.GetDownloadLoader
        Return _sources.ElementAt(_randIndex).GetDownloadLoader(channel, arch, output)
    End Function
End Class
