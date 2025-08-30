Imports PCL.Core.IO
Imports PCL.Core.Minecraft
Imports PCL.Core.Utils

Public Module ModWorld

#Region "压缩包处理"
    ''' <summary>
    ''' 尝试处理存档。
    ''' </summary>
    ''' <exception cref="CancelledException">确定这是一个存档文件（夹），但存档文件损坏时抛出的异常。</exception>
    ''' <exception cref="Exception"></exception>
    Public Async Function ReadWorld(SavePath As String) As Task
        If File.Exists(SavePath) Then
            Dim ExtractPath As String = $"{PathTemp}Cache\{RandomUtils.NextInt(0, 1000_0000)}\"
            If Directory.Exists(ExtractPath) Then DeleteDirectory(ExtractPath)
            Await Files.ExtractFileAsync(SavePath, ExtractPath)
            SavePath = ExtractPath
        End If
        Dim world As New McWorld(SavePath)
        If Not File.Exists(world.LevelDatPath) Then Throw New Exception("无效的 Minecraft 存档")
        If Not Await world.ReadAsync() Then
            Hint("存档文件可能已损坏，无法读取！", HintType.Critical)
            Throw New CancelledException()
        End If
        Dim sb As New StringBuilder
        If world.VersionName IsNot Nothing Then sb.AppendLine($"存档版本：{world.VersionName}")
        If world.VersionId IsNot Nothing Then sb.AppendLine($"存档数据版本：{world.VersionId}")
        If sb.Length = 0 Then sb.AppendLine("无法获取存档的版本信息，存档版本可能低于 15w32a（对应正式版 1.9）！")
        MyMsgBox(sb.ToString, "存档版本信息")
    End Function
#End Region

End Module
