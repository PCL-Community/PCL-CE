Imports PCL.Core.UI.Controls

''' <summary>
''' 弹窗内容控件的基类，用于保存特有数据和提供统一接口。
''' </summary>
Public MustInherit Class MyMsgContent
    Inherits Grid

    ''' <summary>
    ''' 获取或设置弹窗项（用于访问标题、按钮等通用数据）。
    ''' </summary>
    Public Property Item As MyMsgBoxItem

    ''' <summary>
    ''' 获取结果数据。子类应重写此方法以返回特定类型的结果。
    ''' </summary>
    Public MustOverride Function GetResult() As Object

    ''' <summary>
    ''' 初始化内容控件。子类应重写此方法以设置特有数据。
    ''' </summary>
    Public MustOverride Sub Initialize()

End Class
