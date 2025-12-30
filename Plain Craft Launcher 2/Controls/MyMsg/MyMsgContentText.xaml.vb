Imports PCL.Core.UI.Controls

Partial Public Class MyMsgContentText
    Inherits MyMsgContent

    Private _Text As String

    Public Sub New(text As String)
        InitializeComponent()
        _Text = text
    End Sub

    Public Overrides Sub Initialize()
        LabCaption.Text = _Text
    End Sub

    Public Overrides Function GetResult() As Object
        ' Text 类型返回按钮编号（在 MyMsgCustom 中处理）
        Return Nothing
    End Function

End Class

