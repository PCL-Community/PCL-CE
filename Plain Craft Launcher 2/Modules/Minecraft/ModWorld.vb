Imports System.Xml.XPath
Imports PlainNamedBinaryTag

Public Module ModWorld

#Region "压缩包处理"
    ''' <summary>
    ''' 尝试处理存档。
    ''' </summary>
    ''' <exception cref="CancelledException">确定这是一个存档文件（夹），但存档文件损坏时抛出的异常。</exception>
    ''' <exception cref="Exception"></exception>
    Public Sub ReadWorld(SavePath As String)
        If File.Exists(SavePath) Then
            Dim ExtractPath As String = $"{PathTemp}Cache\{RandomInteger(0, 1000_0000)}\"
            If Directory.Exists(ExtractPath) Then DeleteDirectory(ExtractPath)
            ExtractFile(SavePath, ExtractPath)
            SavePath = ExtractPath
        End If
        Dim world As New McWorld(SavePath)
        If Not File.Exists(world.LevelDatPath) Then Throw New Exception("无效的 Minecraft 存档")
        If Not world.Read Then
            Hint("存档文件可能已损坏，无法读取！", HintType.Critical)
            Throw New CancelledException()
        End If
        Dim sb As New StringBuilder
        If world.VersionName IsNot Nothing Then sb.AppendLine($"存档版本：{world.VersionName}")
        If world.VersionId IsNot Nothing Then sb.AppendLine($"存档数据版本：{world.VersionId}")
        If sb.Length = 0 Then sb.AppendLine("无法获取存档的版本信息，存档版本可能低于 15w32a（对应正式版 1.9）！")
        MyMsgBox(sb.ToString, "存档版本信息")
    End Sub
#End Region

#Region "存档"
    ''' <summary>
    ''' 存档。
    ''' </summary>
    Public Class McWorld
        ''' <summary>
        ''' 存档路径。文件夹，以 “\” 结尾。
        ''' </summary>
        Public SavePath As String
        ''' <summary>
        ''' 版本名。
        ''' </summary>
        Public VersionName As String
        ''' <summary>
        ''' 版本 ID。
        ''' </summary>
        Public VersionId As String
        ''' <summary>
        ''' 存档。
        ''' </summary>
        ''' <param name="SavePath">存档路径。文件夹，以 “\” 结尾。</param>
        Public Sub New(SavePath As String)
            If Not SavePath.EndsWithF("\") Then SavePath = SavePath & "\"
            Me.SavePath = SavePath
        End Sub
        Public ReadOnly Property LevelDatPath As String
            Get
                Return If(File.Exists(SavePath & "level.dat"), SavePath & "level.dat", SavePath & "level.dat_old")
            End Get
        End Property
        ''' <summary>
        ''' 读取存档。返回是否成功。
        ''' </summary>
        Public Function Read() As Boolean
            Try
                Log($"[World] 读取存档：{SavePath}")
                If Not File.Exists(LevelDatPath) Then
                    Log("[World] 存档没有 level.dat 文件，读取失败")
                    Return False
                End If
                '读取 NBT 数据
                Dim rootTag As XElement
                Using reader As NbtReader = VbNbtReaderCreator.FromPath(LevelDatPath, True)
                    rootTag = reader.ReadNbtAsXml(NbtType.TCompound)
                End Using

                Dim versionTag As XElement = rootTag.XPathSelectElement("//TCompound[@Name='Version']")
                If versionTag Is Nothing Then
                    Log("[World] Version 标签存在问题，读取失败")
                    Return False
                End If
                VersionName = rootTag.XPathSelectElement("//TCompound[@Name='Version']/TString[@Name='Name']")
                VersionId = rootTag.XPathSelectElement("//TCompound[@Name='Version']/TInt32[@Name='Id']")

                Return True
            Catch ex As Exception
                Log(ex, "读取存档时出错")
                Return False
            End Try
        End Function
    End Class
#End Region

End Module
