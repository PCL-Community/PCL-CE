Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports PCL.Core.Utils.Exts

Public Class FontSelector
    Public Shared ReadOnly TooltipProperty As DependencyProperty = 
        DependencyProperty.Register("Tooltip", GetType(String), GetType(FontSelector), 
                                   New PropertyMetadata(Nothing, AddressOf OnTooltipChanged))

    Public Property Tooltip As String
        Get
            Return CStr(GetValue(TooltipProperty))
        End Get
        Set(value As String)
            SetValue(TooltipProperty, value)
        End Set
    End Property

    Private Shared Sub OnTooltipChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim control = TryCast(d, FontSelector)
        If control IsNot Nothing Then
            control.ComboFont.ToolTip = e.NewValue
        End If
    End Sub

    Public Class CustomFontProperties
        Public Property Name As String
        Public Property Font As FontFamily
        Public Property Tag As String
    End Class

    Public ReadOnly Property CustomFontCollection As New ObservableCollection(Of CustomFontProperties)

    Public Event SelectionChanged(sender As Object, e As SelectionChangedEventArgs)

    Private Sub FontSelector_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If CustomFontCollection.Count = 0 Then
            LoadFonts()
        End If
    End Sub

    Private Sub LoadFonts()
        Dispatcher.BeginInvoke(Async Function() As Task
            ComboFont.IsEnabled = False
            CustomFontCollection.Add(New CustomFontProperties() With {.Name = "加载中..."})
            ComboFont.SelectedIndex = 0
            Dim availableFonts As New List(Of KeyValuePair(Of String, FontFamily))
            Await Task.Run(Sub()
                For Each Font In Fonts.SystemFontFamilies
                    Try
                        '忽略 Global 系列字体
                        If Font.Source.StartsWith("Global ") Then Continue For
                        '尝试加载字体以检测是否可用
                        For Each Typeface In Font.GetTypefaces()
                            Dim glyph As GlyphTypeface = Nothing
                            Typeface.TryGetGlyphTypeface(glyph)
                            If glyph Is Nothing Then Throw New NullReferenceException($"字形 {Typeface.FaceNames.GetForCurrentUiCulture("(unknown)")} 无法加载")
                            'ReSharper disable once UnusedVariable
                            Dim vbSucks = New GlyphTypeface(glyph.FontUri)
                        Next
                        availableFonts.Add(New KeyValuePair(Of String, FontFamily)(Font.FamilyNames.GetForCurrentUiCulture(), Font))
                    Catch ex As Exception
                        Log(ex, "发现了一个无法加载的异常的字体：" & Font.Source, LogLevel.Debug)
                    End Try
                Next
                availableFonts.Sort(Function(l, r) String.Compare(l.Key, r.Key))
            End Sub)
            CustomFontCollection.Clear()
            CustomFontCollection.Add(New CustomFontProperties With {
                .Name = "默认",
                .Font = New FontFamily(New Uri("pack://application:,,,/"), "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI"),
                .Tag = ""
            })
            For Each font In availableFonts
                CustomFontCollection.Add(New CustomFontProperties With {
                    .Name = font.Key,
                    .Font = font.Value,
                    .Tag = font.Value.Source
                })
            Next
            ComboFont.IsEnabled = True
            
            ' 应用之前待设置的字体
            If _pendingFontTag IsNot Nothing Then
                Dim pendingTag = _pendingFontTag
                _pendingFontTag = Nothing
                SelectedFontTag = pendingTag
            End If
        End Function)
    End Sub

    Private _pendingFontTag As String = Nothing

    Public Property SelectedFontTag As String
        Get
            If ComboFont.SelectedItem Is Nothing Then Return ""
            Dim selectedFont = TryCast(ComboFont.SelectedItem, CustomFontProperties)
            If selectedFont Is Nothing Then Return ""
            Return selectedFont.Tag
        End Get
        Set(value As String)
            ' 如果字体还在加载中，延迟设置
            If CustomFontCollection.Count = 0 OrElse (CustomFontCollection.Count = 1 AndAlso CustomFontCollection(0).Name = "加载中...") Then
                _pendingFontTag = value
                Return
            End If
            
            Dim targetSelection = CustomFontCollection.FirstOrDefault(Function(i) i.Tag = value)
            If targetSelection Is Nothing Then
                ComboFont.SelectedIndex = 0
            Else
                ComboFont.SelectedItem = targetSelection
            End If
        End Set
    End Property

    Public Property SelectedIndex As Integer
        Get
            Return ComboFont.SelectedIndex
        End Get
        Set(value As Integer)
            ComboFont.SelectedIndex = value
        End Set
    End Property

    Public Property IsEnabled As Boolean
        Get
            Return ComboFont.IsEnabled
        End Get
        Set(value As Boolean)
            ComboFont.IsEnabled = value
        End Set
    End Property

    Private Sub ComboFont_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles ComboFont.SelectionChanged
        RaiseEvent SelectionChanged(sender, e)
    End Sub

End Class
