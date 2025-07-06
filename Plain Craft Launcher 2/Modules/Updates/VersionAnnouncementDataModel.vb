Public Class VersionAnnouncementDataModel
    Public Property content As List(Of VersionAnnouncementContentModel)
End Class

Public Class VersionAnnouncementContentModel
    Public Property title As String
    Public Property detail As String
    Public Property id As String
    Public Property [date] As String
    Public Property btn1 As AnnouncementBtnInfoModel
    Public Property btn2 As AnnouncementBtnInfoModel
End Class
Public Class AnnouncementBtnInfoModel
    Public Property text As String
    Public Property command As String
    Public Property command_paramter As String
End Class