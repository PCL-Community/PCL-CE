Imports PCL.Core.UI.Controls

Public Class MyMsgContentText
    Inherits UserControl

    Public Property Converter As MyMsgBoxConverter

    Public Sub New(conv As MyMsgBoxConverter)
        InitializeComponent()
        Converter = conv
        LabCaption.Text = conv.Text
    End Sub

End Class

