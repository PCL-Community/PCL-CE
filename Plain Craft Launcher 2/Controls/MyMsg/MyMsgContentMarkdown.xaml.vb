Imports PCL.Core.UI.Controls

Public Class MyMsgContentMarkdown
    Inherits UserControl

    Public Property Converter As MyMsgBoxConverter

    Public Sub New(conv As MyMsgBoxConverter)
        InitializeComponent()
        Converter = conv
        LabCaption.Markdown = conv.Text
        DataContext = Me
    End Sub

End Class

