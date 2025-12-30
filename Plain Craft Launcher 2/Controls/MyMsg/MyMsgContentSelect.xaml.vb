Imports PCL.Core.UI.Controls

Public Class MyMsgContentSelect
    Inherits UserControl

    Public Property Converter As MyMsgBoxConverter
    Private SelectedIndex As Integer = -1

    Public Sub New(conv As MyMsgBoxConverter)
        InitializeComponent()
        Converter = conv
        '添加选择控件
        For Each Selection As IMyRadio In conv.Content
            PanSelection.Children.Add(Selection)
            AddHandler Selection.Check, AddressOf OnChecked
            If TypeOf Selection Is MyListItem Then
                CType(Selection, MyListItem).Type = MyListItem.CheckType.RadioBox
                CType(Selection, MyListItem).MinHeight = 24
            Else
                CType(Selection, MyRadioBox).MinHeight = 24
            End If
        Next
    End Sub

    Private Sub OnChecked(sender As IMyRadio, e As EventArgs)
        SelectedIndex = PanSelection.Children.IndexOf(sender)
        RaiseEvent SelectionChanged(Me, EventArgs.Empty)
    End Sub

    Public ReadOnly Property HasSelection As Boolean
        Get
            Return SelectedIndex >= 0
        End Get
    End Property

    Public ReadOnly Property GetSelectedIndex As Integer?
        Get
            If SelectedIndex >= 0 Then
                Return SelectedIndex
            End If
            Return Nothing
        End Get
    End Property

    Public Event SelectionChanged As EventHandler

End Class

