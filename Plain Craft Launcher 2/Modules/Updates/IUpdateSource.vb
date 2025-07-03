Imports PCL.Core.Helper
Public Interface IUpdateSource
    ''' <summary>
    ''' 是否可用，根据本地情况判断
    ''' </summary>
    ''' <returns></returns>
    Function IsAvailable() As Boolean
    ''' <summary>
    ''' 确保最新版本
    ''' </summary>
    ''' <returns>True 表示更新成功，False 表示没有数据更新</returns>
    Function RefreshCache() As Boolean
    Function GetLatestVersion(channel As UpdateChannel, arch As UpdateArch) As VersionDataModel
    Function IsLatest(channel As UpdateChannel, arch As UpdateArch, currentVersion As SemVer, currentVersionCode As Integer) As Boolean
    Function GetAnnouncementList() As VersionAnnouncementDataModel
    Function GetDownloadLoader(channel As UpdateChannel, arch As UpdateArch, output As String) As List(Of LoaderBase)
    Property SourceName As String
End Interface
