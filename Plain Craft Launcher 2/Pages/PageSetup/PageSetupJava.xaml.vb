Imports PCL.Core.Java

Public Class PageSetupJava

    Private IsLoad As Boolean = False

    Private Async Sub PageSetupLaunch_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        Dim ret = Await JavaHelper.ScanJava()
        MsgBox(ret.Select(Function(x) $"{x.Path} - {x.Version.ToString()} - {x.Brand.ToString()}").Join(vbCrLf))
    End Sub

End Class
