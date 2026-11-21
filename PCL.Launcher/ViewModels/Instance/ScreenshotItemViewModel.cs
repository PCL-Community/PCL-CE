using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL;

/// <summary>实例截图页中的单个截图条目。</summary>
public partial class ScreenshotItemViewModel(
    string filePath,
    string displayName) : ObservableObject
{
    /// <summary>条目是否被选中；页面只负责把点击事件同步到这里。</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>截图文件的完整路径。</summary>
    public string FilePath { get; } = filePath;

    /// <summary>用于提示框显示的相对路径。</summary>
    public string DisplayName { get; } = displayName;
}