Imports System.Text.RegularExpressions
Imports System.Windows.Controls.Primitives
Imports System.Windows.Threading
Imports fNbt
Imports PCL.Core.IO
Imports PCL.Core.Minecraft
Imports PCL.Core.UI

Public Class ServerCard
    Dim _server As MinecraftServerInfo
    Dim ReadOnly _manager As IconManager
    
    ' #808080 Default Color for originalColorMap
    ' Minecraft color code mapping
    Private ReadOnly originalColorMap As New Dictionary(Of String, Brush) From {
        {"0", Brushes.Black},        ' Black
        {"1", New SolidColorBrush(Color.FromRgb(0, 0, 170))}, ' Dark Blue
        {"2", New SolidColorBrush(Color.FromRgb(0, 170, 0))}, ' Dark Green
        {"3", New SolidColorBrush(Color.FromRgb(0, 170, 170))}, ' Cyan
        {"4", New SolidColorBrush(Color.FromRgb(170, 0, 0))}, ' Dark Red
        {"5", New SolidColorBrush(Color.FromRgb(170, 0, 170))}, ' Purple
        {"6", New SolidColorBrush(Color.FromRgb(255, 170, 0))}, ' Gold
        {"7", Brushes.LightGray},    ' Gray
        {"8", Brushes.DarkGray},     ' Dark Gray
        {"9", Brushes.Blue},         ' Blue
        {"a", Brushes.Lime},         ' Green
        {"b", Brushes.Cyan},         ' Cyan
        {"c", Brushes.Red},          ' Red
        {"d", Brushes.Magenta},      ' Magenta
        {"e", Brushes.Yellow},       ' Yellow
        {"f", Brushes.White}         ' White
    }
    '        {"0", New SolidColorBrush(Color.FromRgb(51, 51, 51))},    ' 深灰 #333333
'        {"1", New SolidColorBrush(Color.FromRgb(0, 48, 135))},   ' 海军蓝 #003087
'        {"2", New SolidColorBrush(Color.FromRgb(0, 128, 0))},    ' 森林绿 #008000
'        {"3", New SolidColorBrush(Color.FromRgb(0, 122, 122))},  ' 青色 #007A7A
'        {"4", New SolidColorBrush(Color.FromRgb(161, 0, 0))},    ' 深红 #A10000
'        {"5", New SolidColorBrush(Color.FromRgb(128, 0, 128))},  ' 深紫 #800080
'        {"6", New SolidColorBrush(Color.FromRgb(204, 112, 0))},  ' 深橙 #CC7000
'        {"7", New SolidColorBrush(Color.FromRgb(102, 102, 102))}, ' 中灰 #666666
'        {"8", New SolidColorBrush(Color.FromRgb(68, 68, 68))},   ' 炭灰 #444444
'        {"9", New SolidColorBrush(Color.FromRgb(0, 68, 204))},   ' 皇家蓝 #0044CC
'        {"a", New SolidColorBrush(Color.FromRgb(0, 153, 0))},    ' 绿色 #009900
'        {"b", New SolidColorBrush(Color.FromRgb(0, 161, 161))},  ' 青色 #00A1A1
'        {"c", New SolidColorBrush(Color.FromRgb(204, 0, 0))},    ' 红色 #CC0000
'        {"d", New SolidColorBrush(Color.FromRgb(194, 0, 194))},  ' 品红 #C200C2
'        {"e", New SolidColorBrush(Color.FromRgb(179, 160, 0))},  ' 深黄 #B3A000
'        {"f", New SolidColorBrush(Color.FromRgb(85, 85, 85))}    ' 暗灰 #555555
    
    
    ' 针对白色背景 (#f3f6fa) 优化的颜色代码映射
    Private ReadOnly colorMap As New Dictionary(Of String, Brush) From {
        {"0", New SolidColorBrush(Color.FromRgb(51, 51, 51))},    ' 深灰 #333333
        {"1", New SolidColorBrush(Color.FromRgb(0, 48, 135))},   ' 海军蓝 #003087
        {"2", New SolidColorBrush(Color.FromRgb(0, 128, 0))},    ' 森林绿 #008000
        {"3", New SolidColorBrush(Color.FromRgb(0, 122, 122))},  ' 青色 #007A7A
        {"4", New SolidColorBrush(Color.FromRgb(161, 0, 0))},    ' 深红 #A10000
        {"5", New SolidColorBrush(Color.FromRgb(128, 0, 128))},  ' 深紫 #800080
        {"6", New SolidColorBrush(Color.FromRgb(204, 112, 0))},  ' 深橙 #CC7000
        {"7", New SolidColorBrush(Color.FromRgb(102, 102, 102))}, ' 中灰 #666666
        {"8", New SolidColorBrush(Color.FromRgb(68, 68, 68))},   ' 炭灰 #444444
        {"9", New SolidColorBrush(Color.FromRgb(0, 68, 204))},   ' 皇家蓝 #0044CC
        {"a", New SolidColorBrush(Color.FromRgb(0, 153, 0))},    ' 绿色 #009900
        {"b", New SolidColorBrush(Color.FromRgb(0, 161, 161))},  ' 青色 #00A1A1
        {"c", New SolidColorBrush(Color.FromRgb(204, 0, 0))},    ' 红色 #CC0000
        {"d", New SolidColorBrush(Color.FromRgb(194, 0, 194))},  ' 品红 #C200C2
        {"e", New SolidColorBrush(Color.FromRgb(179, 160, 0))},  ' 深黄 #B3A000
        {"f", New SolidColorBrush(Color.FromRgb(136, 136, 136))}         ' White
    }

    ' Format code mapping
    Private ReadOnly formatMap As New Dictionary(Of String, Boolean) From {
        {"l", True},  ' Bold
        {"o", True},  ' Italic
        {"n", True},  ' Underline
        {"m", True},  ' Strikethrough
        {"k", True}, ' Obfuscated (not supported)
        {"r", False}  ' Reset
    }
    ' 存储 §k 文本的 TextBlock 和原始文本
    Private obfuscatedTextBlocks As New List(Of Tuple(Of TextBlock, String))
    Private random As New Random()
    Private ReadOnly randomChars As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()"

    
    Public Sub New()
        InitializeComponent()
        ' AddHandler Loaded, AddressOf MainWindow_Loaded
        ' 启动定时器以更新 §k 文本
        Dim timer As New DispatcherTimer()
        timer.Interval = TimeSpan.FromMilliseconds(20)
        AddHandler timer.Tick, AddressOf UpdateObfuscatedText
        timer.Start()
        
        DataContext = New IconManager()
        
        ' 示例：可在代码中切换图标
        _manager = TryCast(DataContext, IconManager)
        _manager.AddIconFromXaml("signal_1", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_2", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_3", "<Viewbox Width=""20"" Height=""20"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff888888""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_4", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff00ff21""/></Canvas><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff888888""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_5", "<Viewbox Width=""20"" Height=""20"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Canvas UseLayoutRounding=""False""><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""234.666667"" Canvas.Top=""610.56"" Width=""80.853333"" Height=""127.04"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""353.066667"" Canvas.Top=""541.226667"" Width=""80.853333"" Height=""196.373333"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""471.445333"" Canvas.Top=""460.373333"" Width=""80.896"" Height=""277.226667"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""589.866667"" Canvas.Top=""379.52"" Width=""80.853333"" Height=""358.08"" Fill=""#ff00ff21""/><Rectangle RadiusX=""0.0"" RadiusY=""0.0"" Canvas.Left=""708.266667"" Canvas.Top=""298.666667"" Width=""80.853333"" Height=""438.933333"" Fill=""#ff00ff21""/></Canvas></Canvas></Viewbox>")
        _manager.AddIconFromXaml("signal_offline", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""14"" Height=""14"" Margin=""3""><Canvas UseLayoutRounding=""False"" Width=""1280.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1280.0,1024.0""/></Canvas.Clip><Path Fill=""#ff000000""><Path.Data><PathGeometry Figures=""M 317.63 349.235 l -67.951 -67.951 l -67.95 67.95 c -18.964 18.964 -48.988 18.964 -67.951 0 c -18.963 -18.962 -18.963 -48.987 0 -67.95 l 67.95 -67.95 l -66.37 -67.951 c -18.963 -18.963 -18.963 -48.988 0 -67.95 c 18.963 -18.964 48.988 -18.964 67.95 0 l 67.951 67.95 l 67.95 -67.95 c 18.964 -18.964 48.989 -18.964 67.951 0 c 18.963 18.962 18.963 48.987 0 67.95 l -67.95 67.95 l 67.95 67.951 c 18.963 18.963 18.963 48.988 0 67.95 c -9.481 9.482 -20.543 14.223 -33.185 14.223 c -14.222 0 -26.864 -6.321 -36.345 -14.222 z M 216.494 752.198 h -48.988 c -26.864 0 -48.987 26.864 -48.987 60.049 v 120.099 c 0 33.185 22.123 60.05 48.987 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 v -120.1 c 0 -33.184 -22.123 -60.048 -48.987 -60.048 z M 516.74 512 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 360.296 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 572.049 c 0 -33.185 -22.123 -60.049 -48.987 -60.049 z m 300.247 -240.198 H 768 c -26.864 0 -48.988 26.865 -48.988 60.05 v 600.494 c 0 33.185 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.865 48.987 -60.05 V 331.852 c 0 -33.185 -22.123 -60.05 -48.987 -60.05 z m 300.247 -240.197 h -48.988 c -26.864 0 -48.988 26.864 -48.988 60.05 v 840.69 c 0 33.186 22.124 60.05 48.988 60.05 h 48.988 c 26.864 0 48.987 -26.864 48.987 -60.05 V 91.656 c -1.58 -33.186 -22.123 -60.05 -48.987 -60.05 z"" FillRule=""Nonzero""/></Path.Data></Path></Canvas></Viewbox>")
        _manager.AddIconFromXaml("loading", "<Viewbox xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Width=""20"" Height=""20""><Canvas UseLayoutRounding=""False"" Width=""1024.0"" Height=""1024.0""><Canvas.Clip><RectangleGeometry Rect=""0.0,0.0,1024.0,1024.0""/></Canvas.Clip><Path Fill=""#ff000000""><Path.Data><PathGeometry Figures=""M 256 490.667 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z m 384 0 a 106.667 106.667 0 1 1 -213.376 -0.042667 A 106.667 106.667 0 0 1 597.333 490.667 z m -42.6667 0 a 64 64 0 1 0 -128.043 0.042666 A 64 64 0 0 0 554.667 490.667 z m 298.667 0 a 64 64 0 1 1 -128 0 a 64 64 0 0 1 128 0 z m -42.6667 0 a 21.3333 21.3333 0 1 0 -42.6667 0 a 21.3333 21.3333 0 0 0 42.6667 0 z"" FillRule=""Nonzero""/></Path.Data></Path></Canvas></Viewbox>")
    End Sub
    
    Private Sub UpdateObfuscatedText(sender As Object, e As EventArgs)
        For Each item In obfuscatedTextBlocks
            Dim textBlock = item.Item1
            Dim originalText = item.Item2
            ' 生成与原始文本等长的随机字符
            Dim obfuscated As String = String.Join("", Enumerable.Range(0, originalText.Length).Select(Function(i) randomChars(random.Next(randomChars.Length))))
            textBlock.Text = obfuscated
        Next
    End Sub
    Private ReadOnly backgroundColor As Color = Color.FromRgb(243, 246, 250) ' #f3f6fa
    
    
    Private Function GetRelativeLuminance(color As Color) As Double
        Dim r As Double = color.R/255.0
        Dim g As Double = color.G/255.0
        Dim b As Double = color.B/255.0
        Dim rL As Double = If(r <= 0.03928, r/12.92, ((r + 0.055)/1.055)^2.4)
        Dim gL As Double = If(g <= 0.03928, g/12.92, ((g + 0.055)/1.055)^2.4)
        Dim bL As Double = If(b <= 0.03928, b/12.92, ((b + 0.055)/1.055)^2.4)
        Return 0.2126*rL + 0.7152*gL + 0.0722*bL
    End Function

    Private Function GetContrastRatio(foreground As Color, background As Color) As Double
        Dim l1 As Double = GetRelativeLuminance(foreground)
        Dim l2 As Double = GetRelativeLuminance(background)
        Return (Math.Max(l1, l2) + 0.05)/(Math.Min(l1, l2) + 0.05)
    End Function

    Private Function AdjustColorForContrast(inputColor As Color) As Color
        Dim contrastRatio As Double = GetContrastRatio(inputColor, backgroundColor)
        If contrastRatio >= 4.5 Then Return inputColor ' 对比度已足够
' 将 RGB 转换为 HSL
        Dim r As Double = inputColor.R/255.0
        Dim g As Double = inputColor.G/255.0
        Dim b As Double = inputColor.B/255.0
        Dim max As Double = Math.Max(Math.Max(r, g), b)
        Dim min As Double = Math.Min(Math.Min(r, g), b)
        Dim l As Double = (max + min)/2.0
        Dim s As Double
        Dim h As Double
        If max = min Then
            h = 0.0
            s = 0.0
        Else
            Dim d As Double = max - min
            s = If(l > 0.5, d/(2.0 - max - min), d/(max + min))
            Select Case max
                Case r
                    h = (g - b)/d + If(g < b, 6.0, 0.0)
                Case g
                    h = (b - r)/d + 2.0
                Case b
                    h = (r - g)/d + 4.0
            End Select
            h /= 6.0
        End If
' 降低亮度直到对比度 ≥ 4.5:1
        Dim newL As Double = l
        Dim adjustedColor As Color = inputColor
        While newL > 0.1 AndAlso GetContrastRatio(adjustedColor, backgroundColor) < 4.5
            newL -= 0.05 ' 逐步降低亮度
            Dim newR As Double
            Dim newG As Double
            Dim newB As Double
            If s = 0 Then
                newR = newL
                newG = newL
                newB = newL
            Else
                Dim q As Double = If(newL < 0.5, newL*(1.0 + s), newL + s - newL*s)
                Dim p As Double = 2.0*newL - q
                newR = HueToRgb(p, q, h + 1.0/3.0)
                newG = HueToRgb(p, q, h)
                newB = HueToRgb(p, q, h - 1.0/3.0)
            End If
            adjustedColor = Color.FromRgb(CByte(newR*255), CByte(newG*255), CByte(newB*255))
        End While
' 若仍无法满足对比度，使用默认颜色 #555555
        If GetContrastRatio(adjustedColor, backgroundColor) < 4.5 Then
            Return Color.FromRgb(85, 85, 85) ' colorMap("f")
        End If
        Return adjustedColor
    End Function
    
    Private Function HueToRgb(p As Double, q As Double, t As Double) As Double
        If t < 0 Then t += 1.0
        If t > 1 Then t -= 1.0
        If t < 1.0 / 6.0 Then Return p + (q - p) * 6.0 * t
        If t < 0.5 Then Return q
        If t < 2.0 / 3.0 Then Return p + (q - p) * (2.0 / 3.0 - t) * 6.0
        Return p
    End Function
    
    Private Sub RenderMotd(motd As String)
        MotdCanvas.Children.Clear()
        obfuscatedTextBlocks.Clear()
        Dim font As String = Setup.Get("UiFont")
        Dim fontFamily As New FontFamily(If(String.IsNullOrWhiteSpace(font), "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI", font))
        Dim fontSize As Double = 12
        Dim canvasWidth As Double = If(MotdCanvas.ActualWidth > 0, MotdCanvas.ActualWidth, 300) ' 防止宽度为0
        Dim canvasHeight As Double = If(MotdCanvas.ActualHeight > 0, MotdCanvas.ActualHeight, 34) ' 防止宽度为0
        Dim y As Double = 10

        ' 正则表达式匹配 § 代码和 RGB 颜色
        Dim regex As New Regex("(§[0-9a-fk-oAr]|#[0-9A-Fa-f]{6})")

        ' 分割多行 MOTD
        motd = Replace(motd, vbLf, vbCrLf)
        Dim lines As String() = motd.Split(vbCrLf)
        Dim currentColor As Brush = colorMap("f")
        Dim isBold As Boolean = False
        Dim isItalic As Boolean = False
        Dim isUnderline As Boolean = False
        Dim isStrikethrough As Boolean = False
        Dim isObfuscated As Boolean = False

        For lineIndex As Integer = 0 To lines.Length - 1
            Dim line As String = lines(lineIndex).Trim()
            Dim parts As String() = regex.Split(line)

            ' 计算整行宽度
            Dim lineWidth As Double = 0
            Dim lineHeight As Double = 0
            Dim tempX As Double = 0 ' 临时x坐标用于宽度计算
            Dim textBlocks As New List(Of TextBlock) ' 存储每行的TextBlock
            Dim positions As New List(Of Double) ' 存储每个TextBlock的x坐标
            Dim partTexts As New List(Of String) ' 存储每段文本内容

            For Each part As String In parts
                If String.IsNullOrEmpty(part) Then Continue For

                ' 处理 § 颜色代码
                If part.StartsWith("§") AndAlso part.Length = 2 Then
                    Dim code As String = part.Substring(1).ToLower()
                    If colorMap.ContainsKey(code) Then
                        currentColor = colorMap(code)
                        isBold = False
                        isItalic = False
                        isUnderline = False
                        isStrikethrough = False
                        isObfuscated = False
                    ElseIf formatMap.ContainsKey(code) Then
                        If code = "l" Then isBold = True
                        If code = "o" Then isItalic = True
                        If code = "n" Then isUnderline = True
                        If code = "m" Then isStrikethrough = True
                        If code = "k" Then isObfuscated = True
                        If code = "r" Then
                            currentColor = colorMap("f")
                            isBold = False
                            isItalic = False
                            isUnderline = False
                            isStrikethrough = False
                            isObfuscated = False
                        End If
                    End If
                    Continue For
                End If

                ' 处理 RGB 颜色代码
                If Regex.IsMatch(part, "^#[0-9A-Fa-f]{6}$") Then
                    Try
                        Dim hex As String = part.Substring(1)
                        Dim r As Byte = Convert.ToByte(hex.Substring(0, 2), 16)
                        Dim g As Byte = Convert.ToByte(hex.Substring(2, 2), 16)
                        Dim b As Byte = Convert.ToByte(hex.Substring(4, 2), 16)
                        Dim inputColor As Color = Color.FromRgb(r, g, b)
                        currentColor = New SolidColorBrush(AdjustColorForContrast(inputColor))
                        isBold = False
                        isItalic = False
                        isUnderline = False
                        isStrikethrough = False
                        isObfuscated = False
                    Catch
                        ' 无效 RGB 颜色，保持当前颜色
                    End Try
                    Continue For
                End If

                ' 渲染文本，始终使用原始文本计算宽度
                Dim displayText As String = part
                If isObfuscated Then
                    ' 为 §k 文本生成初始随机字符
                    displayText = String.Join("", Enumerable.Range(0, part.Length).Select(Function(i) randomChars(random.Next(randomChars.Length))))
                End If
                Dim textBlock = RenderText(displayText, fontFamily, fontSize, currentColor, isBold, isItalic, isUnderline, isStrikethrough, tempX, y)
                textBlocks.Add(textBlock)
                positions.Add(tempX)
                partTexts.Add(part) ' 存储原始文本用于混淆

                ' 使用原始文本宽度更新 tempX 坐标
                If isObfuscated Then
                    Dim textHeight = MeasureTextHeight(part, fontFamily, fontSize, isBold, isItalic)
                    lineHeight = If(textHeight > lineHeight, textHeight, lineHeight)
                    If IsMonospacedFont(fontFamily.Source) Then
                        Log("使用等宽字体：" & fontFamily.Source)
                        tempX += MeasureTextWidth(part, fontFamily, fontSize, isBold, isItalic)
                    Else
                        Log("使用非等宽字体：" & fontFamily.Source)
                        tempX += GetMaxCharacterWidth(fontFamily, fontSize, isBold, isItalic) * part.Length
                    End If
                Else
                    tempX += MeasureTextWidth(part, fontFamily, fontSize, isBold, isItalic)
                    Dim textHeight = MeasureTextHeight(part, fontFamily, fontSize, isBold, isItalic)
                    lineHeight = If(textHeight > lineHeight, textHeight, lineHeight)
                End If
                lineWidth = tempX ' 更新行宽度
            Next

            ' 居中对齐：调整每行TextBlock的x坐标
            Dim offsetX As Double = (canvasWidth - lineWidth) / 2
            For i As Integer = 0 To textBlocks.Count - 1
                Log(positions(i))
                Canvas.SetLeft(textBlocks(i), positions(i) + offsetX)
                If isObfuscated Then
                    obfuscatedTextBlocks.Add(New Tuple(Of TextBlock, String)(textBlocks(i), partTexts(i)))
                End If
            Next
            
            If lines.Length = 1 Then
                Dim offsetY As Double = (canvasHeight - lineHeight) / 2
                For i As Integer = 0 To textBlocks.Count - 1
                    Canvas.SetTop(textBlocks(i), offsetY)
                    If isObfuscated Then
                        obfuscatedTextBlocks.Add(New Tuple(Of TextBlock, String)(textBlocks(i), partTexts(i)))
                    End If
                Next
            Else If lines.Length = 2 AndAlso lineIndex = 0 Then
                Dim offsetY As Double = (canvasHeight - lineHeight * 2) / 2
                For i As Integer = 0 To textBlocks.Count - 1
                    Canvas.SetTop(textBlocks(i), offsetY)
                    If isObfuscated Then
                        obfuscatedTextBlocks.Add(New Tuple(Of TextBlock, String)(textBlocks(i), partTexts(i)))
                    End If
                Next
                y = lineHeight + offsetY
            End If
        Next
    End Sub
    
    Private Function GetMaxCharacterWidth(fontFamily As FontFamily, fontSize As Double, isBold As Boolean, isItalic As Boolean) As Single
        ' 遍历字符串中的每个字符
        Dim maxWidth As Double = 0
        For Each c As Char In randomChars
            ' 测量单个字符的宽度
            Dim size As Double = MeasureTextWidth(c, fontFamily, fontSize, isBold, isItalic)
            ' 更新最大宽度
            If size > maxWidth Then
                maxWidth = size
            End If
        Next
        Return maxWidth
    End Function
    
    Function IsMonospacedFont(fontName As String) As Boolean
        Try
            Dim typeface As New Typeface(New FontFamily(fontName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal)
            Dim glyphTypeface As GlyphTypeface
            If typeface.TryGetGlyphTypeface(glyphTypeface) Then
                ' 检查字符的宽度（AdvanceWidths）
                Dim widthI As Double = glyphTypeface.AdvanceWidths(glyphTypeface.CharacterToGlyphMap(AscW("i"c)))
                Dim widthW As Double = glyphTypeface.AdvanceWidths(glyphTypeface.CharacterToGlyphMap(AscW("W"c)))

                ' 如果宽度相等，则为等宽字体
                Return Math.Abs(widthI - widthW) < 0.01
            End If
            Return False
        Catch ex As Exception
            Return False
        End Try
    End Function

    Private Function RenderText(text As String, fontFamily As FontFamily, fontSize As Double, color As Brush,
                              isBold As Boolean, isItalic As Boolean, isUnderline As Boolean, isStrikethrough As Boolean,
                              x As Double, y As Double) As TextBlock
        Dim textBlock As New TextBlock With {
            .Text = text,
            .FontFamily = fontFamily,
            .FontSize = fontSize,
            .Foreground = color,
            .FontWeight = If(isBold, FontWeights.Bold, FontWeights.Normal),
            .FontStyle = If(isItalic, FontStyles.Italic, FontStyles.Normal)
        }

        If isUnderline OrElse isStrikethrough Then
            textBlock.TextDecorations = New TextDecorationCollection()
            If isUnderline Then textBlock.TextDecorations.Add(TextDecorations.Underline)
            If isStrikethrough Then textBlock.TextDecorations.Add(TextDecorations.Strikethrough)
        End If

        Canvas.SetLeft(textBlock, x)
        Canvas.SetTop(textBlock, y)
        MotdCanvas.Children.Add(textBlock)
        Return textBlock
    End Function

    Private Function MeasureTextWidth(text As String, fontFamily As FontFamily, fontSize As Double,
                                    isBold As Boolean, isItalic As Boolean) As Double
        Dim formattedText As New FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            New Typeface(fontFamily, If(isItalic, FontStyles.Italic, FontStyles.Normal),
                         If(isBold, FontWeights.Bold, FontWeights.Normal), FontStretches.Normal),
            fontSize,
            Brushes.White,
            96)
        Return formattedText.WidthIncludingTrailingWhitespace
    End Function
    
    Private Function MeasureTextHeight(text As String, fontFamily As FontFamily, fontSize As Double,
                                      isBold As Boolean, isItalic As Boolean) As Double
        Dim formattedText As New FormattedText(
            text,
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            New Typeface(fontFamily, If(isItalic, FontStyles.Italic, FontStyles.Normal),
                         If(isBold, FontWeights.Bold, FontWeights.Normal), FontStretches.Normal),
            fontSize,
            Brushes.White,
            96)
        Return formattedText.Height
    End Function
    
    Private Sub SaveButton_Click()
        ' 确保 Canvas 已渲染
        MotdCanvas.UpdateLayout()

        ' 为 §k 文本生成静态随机字符
        For Each item In obfuscatedTextBlocks
            Dim textBlock = item.Item1
            Dim originalText = item.Item2
            textBlock.Text = String.Join("", Enumerable.Range(0, originalText.Length).Select(Function(i) randomChars(random.Next(randomChars.Length))))
        Next

        ' 使用 RenderTargetBitmap 捕获 Canvas
        Dim rtb As New RenderTargetBitmap(
            CInt(MotdCanvas.Width), CInt(MotdCanvas.Height), 96, 96, PixelFormats.Pbgra32)
        rtb.Render(MotdCanvas)
    End Sub
    
    Private Sub BtnSkin_Click(sender As Object, e As RoutedEventArgs) Handles BtnSetting.Click
        BtnSetting.ContextMenu.IsOpen = True
    End Sub

    ''' <summary>
    ''' 初始化服务器卡片
    ''' </summary>
    Public Sub UpdateServerInfo(server As MinecraftServerInfo)
        _server = server
        RunInUi(Sub() UpdateServerUi())
    End Sub
    
    ''' <summary>
    ''' 更新服务器UI
    ''' </summary>
    Private Async Sub UpdateServerUi()
        If _server Is Nothing Then Return
        
        ' 更新服务器名称
        ServerName.Text = _server.Name
        Await ImageLoaderHelper.SetServerLogoAsync(_server.Icon, ServerIcon)
        If _server.Status = ServerStatus.Online
            _manager.SetSelectedIconByName(GetSignalIcon(_server.Ping))
            Signal.ToolTip = _server.Ping.ToString() & "ms"
            ToolTipService.SetInitialShowDelay(Signal, 0)
            ToolTipService.SetBetweenShowDelay(Signal, 50)
            ToolTipService.SetPlacement(Signal, PlacementMode.Top)
            
            If _server.PlayerCount <> Nothing AndAlso _server.MaxPlayers <> Nothing Then
                ServerPlayer.Text = $"{_server.PlayerCount} / {_server.MaxPlayers}"
            Else
                ServerPlayer.Text = "???"
            End If
            
            ServerMotD.Visibility = Visibility.Collapsed
            RenderMotd(_server.Description)
            SaveButton_Click()
        Else If _server.Status = ServerStatus.Pinging
            _manager.SetSelectedIconByName("loading")
            MotdCanvas.Children.Clear()
            ServerPlayer.Text = "正在连接"
            ServerMotD.Text = "正在连接..."
            ServerMotD.Visibility = Visibility.Visible
        Else If _server.Status = ServerStatus.Offline
            _manager.SetSelectedIconByName("signal_offline")
            MotdCanvas.Children.Clear()
            ServerPlayer.Text = "离线"
            ServerMotD.Text = "服务器离线"
            ServerMotD.Visibility = Visibility.Visible
        End If
    End Sub
    
    Private Function GetSignalIcon(ping As Integer) As String
        Select Case ping
            Case 0 To 99
                Return "signal_5" ' 5 条信号
            Case 100 To 299
                Return "signal_4" ' 4 条信号
            Case 300 To 599
                Return "signal_3" ' 3 条信号
            Case 600 To 999
                Return "signal_2" ' 2 条信号
            Case Else
                Return "signal_1" ' 1 条信号
        End Select
    End Function
    
    ''' <summary>
    ''' 刷新服务器状态
    ''' </summary>
    Public Async Function RefreshServerStatus(withHint As Boolean) As Task
        If withHint Then
            Hint($"正在刷新服务器 {_server.Name} 的状态...", HintType.Info)
        End If
        _server.Status = ServerStatus.Pinging
        RunInUi(Sub() UpdateServerUi())
        Dim server = Await PageInstanceServer.PingServer(_server)
        UpdateServerInfo(server)
    End Function
    
    ''' <summary>
    ''' 连接到服务器
    ''' </summary>
    Private Sub BtnConnect_Click(sender As Object, e As EventArgs)
        Try
            Dim launchOptions As New McLaunchOptions With {.ServerIp = _server.Address}
            McLaunchStart(LaunchOptions)
            FrmMain.PageChange(New FormMain.PageStackData With {.Page = FormMain.PageType.Launch})
            Hint($"正在连接到服务器 {_server.Name}...", HintType.Info)
        Catch ex As Exception
            Log(ex, "启动服务器失败", LogLevel.Feedback)
            Hint("启动服务器失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
    
    ''' <summary>
    ''' 复制服务器地址
    ''' </summary>
    Private Sub BtnCopy_Click(sender As Object, e As RoutedEventArgs)
        Try
            Clipboard.SetText(_server.Address)
            Hint($"已复制服务器地址：{_server.Address}", HintType.Finish)
        Catch ex As Exception
            Log(ex, "复制服务器地址失败", LogLevel.Debug)
            Hint("复制服务器地址失败", HintType.Critical)
        End Try
    End Sub
    
    ''' <summary>
    ''' 刷新服务器状态
    ''' </summary>
    Private Async Sub BtnRefresh_Click(sender As Object, e As RoutedEventArgs)
        Await RefreshServerStatus(True)
    End Sub
    
    ''' <summary>
    ''' 编辑服务器信息
    ''' </summary>
    Private Sub BtnEdit_Click(sender As Object, e As RoutedEventArgs)
        Try
            Dim result = PageInstanceServer.GetServerInfo(_server)
            If result.Success Then
                Dim nbtData = NbtFileHandler.ReadNbTFile(PageInstanceLeft.Instance.PathIndie + "servers.dat", "servers")
                If nbtData IsNot Nothing Then
                    Dim index = PageInstanceServer.GetServerIndex(Me)
                    Dim server = TryCast(nbtData(index), NbtCompound)
                    If server.Get(Of NbtString)("name").Value = _server.Name AndAlso
                       server.Get(Of NbtString)("ip").Value = _server.Address Then
                        server("name") = New NbtString("name", result.Name)
                        server("ip") = New NbtString("ip", result.Address)
                        Dim clonedNbtData As NbtList = CType(nbtData.Clone(), NbtList)
                        NbtFileHandler.WriteNbtFile(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat")
                        ' 更改地址和端口
                        _server.Name = result.Name
                        _server.Address = result.Address
                
                        ' 刷新UI
                        RunInUi(Sub() UpdateServerUi())
                
                        Hint("服务器信息已更新", HintType.Finish)
                    End If
                End If
            End If
        Catch ex As Exception
            Log(ex, "编辑服务器信息失败", LogLevel.Feedback)
            Hint("编辑服务器信息失败：" & ex.Message, HintType.Critical)
        End Try
    End Sub
    
    Private Sub BtnRemove_Click(sender As Object, e As RoutedEventArgs)
        If MyMsgBox("你确定要移除服务器 " & _server.Name & " 吗？" & vbCrLf & "'" & _server.Address & "' 将从您的列表中移除，包括游戏内列表，且无法恢复。", "移除服务器确认", "确认", "取消") = 1 Then
            Dim index = PageInstanceServer.GetServerIndex(Me)
            If index >= 0 Then
                PageInstanceServer.RemoveServer(Me)
                
                ' 更新NBT文件
                Dim nbtData = NbtFileHandler.ReadNbTFile(PageInstanceLeft.Instance.PathIndie + "servers.dat", "servers")
                If nbtData IsNot Nothing Then
                    Dim server = TryCast(nbtData(index), NbtCompound)
                    If server.Get(Of NbtString)("name").Value = _server.Name AndAlso
                       server.Get(Of NbtString)("ip").Value = _server.Address Then
                        nbtData.RemoveAt(index)
                        Dim clonedNbtData = CType(nbtData.Clone(), NbtList)
                        NbtFileHandler.WriteNbtFile(clonedNbtData, PageInstanceLeft.Instance.PathIndie + "servers.dat")
                    End If
                End If
                
                Hint("服务器已移除", HintType.Finish)
                Dim parent = TryCast(Me.Parent, Panel)
                If parent IsNot Nothing Then
                    parent.Children.Remove(Me)
                End If
            Else
                Hint("无法找到服务器在列表中的索引", HintType.Critical)
            End If
        End If
    End Sub
End Class