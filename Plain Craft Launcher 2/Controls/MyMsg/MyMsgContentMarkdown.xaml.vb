Imports PCL.Core.UI.Controls

Partial Public Class MyMsgContentMarkdown
    Inherits MyMsgContent

    Private _Text As String

    Public Sub New(text As String)
        InitializeComponent()
        _Text = text
    End Sub

    Public Overrides Sub Initialize()
        LabCaption.Markdown = _Text
        DataContext = Me
    End Sub

    Public Overrides Function GetResult() As Object
        ' Markdown 类型返回按钮编号（在 MyMsgCustom 中处理）
        Return Nothing
    End Function

End Class

