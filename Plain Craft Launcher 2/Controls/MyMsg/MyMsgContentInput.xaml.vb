Imports PCL.Core.UI.Controls

Partial Public Class MyMsgContentInput
    Inherits MyMsgContent

    Private _Text As String
    Private _DefaultInput As String
    Private _HintText As String
    Private _ValidateRules As ObjectModel.Collection(Of Validate)

    Public Sub New(text As String, defaultInput As String, hintText As String, validateRules As ObjectModel.Collection(Of Validate))
        InitializeComponent()
        _Text = text
        _DefaultInput = defaultInput
        _HintText = hintText
        _ValidateRules = validateRules
    End Sub

    Public Overrides Sub Initialize()
        LabText.Text = _Text
        PanText.Visibility = If(_Text = "", Visibility.Collapsed, Visibility.Visible)
        TextArea.Text = _DefaultInput
        TextArea.HintText = _HintText
        TextArea.ValidateRules = _ValidateRules
    End Sub

    Public Overrides Function GetResult() As Object
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

