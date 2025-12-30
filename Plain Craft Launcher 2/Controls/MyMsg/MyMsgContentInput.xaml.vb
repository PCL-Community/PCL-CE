Imports PCL.Core.UI.Controls

Public Class MyMsgContentInput
    Inherits UserControl

    Public Property Converter As MyMsgBoxConverter

    Public Sub New(conv As MyMsgBoxConverter)
        InitializeComponent()
        Converter = conv
        LabText.Text = conv.Text
        PanText.Visibility = If(conv.Text = "", Visibility.Collapsed, Visibility.Visible)
        TextArea.Text = conv.Content
        TextArea.HintText = conv.HintText
        TextArea.ValidateRules = conv.ValidateRules
    End Sub

    Public Function GetResult() As String
        TextArea.Validate()
        If TextArea.IsValidated Then
            Return TextArea.Text
        End If
        Return Nothing
    End Function

    Public ReadOnly Property IsValidated As Boolean
        Get
            Return TextArea.IsValidated
        End Get
    End Property

    Public Sub FocusInput()
        TextArea.Focus()
        TextArea.SelectionStart = TextArea.Text.Length
    End Sub

    Private Sub TextArea_ValidateChanged(sender As Object, e As EventArgs) Handles TextArea.ValidateChanged
        ' 通知外部验证状态改变
        RaiseEvent ValidateChanged(Me, e)
    End Sub

    Public Event ValidateChanged As EventHandler

End Class

