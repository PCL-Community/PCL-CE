Imports System.IO

''' <summary>
''' 表示原理图文件夹的类
''' </summary>
Public Class LocalCompFolder

#Region "基础"

    ''' <summary>
    ''' 文件夹的路径。
    ''' </summary>
    Public ReadOnly Path As String
    
    ''' <summary>
    ''' 文件夹名称。
    ''' </summary>
    Public ReadOnly Property Name As String
        Get
            Return New DirectoryInfo(Path).Name
        End Get
    End Property
    
    ''' <summary>
    ''' 文件夹中的文件数量。
    ''' </summary>
    Public ReadOnly Property FileCount As Integer
        Get
            If Not Directory.Exists(Path) Then Return 0
            Return New DirectoryInfo(Path).EnumerateFiles("*", SearchOption.AllDirectories).Where(Function(f) LocalCompFile.IsCompFile(f.FullName, CompType.Schematic)).Count()
        End Get
    End Property
     
    ''' <summary>
    ''' 是否为文件夹类型。
    ''' </summary>
    Public ReadOnly Property IsFolder As Boolean = True
    
    Public Sub New(FolderPath As String)
        Me.Path = If(FolderPath, "")
    End Sub
    
#End Region

End Class
