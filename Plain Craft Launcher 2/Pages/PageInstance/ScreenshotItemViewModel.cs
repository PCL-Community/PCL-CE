using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PCL;

/// <summary>实例截图页中的单个截图条目。</summary>
public partial class ScreenshotItemViewModel : ObservableObject
{
    public ScreenshotItemViewModel(string filePath, string displayName)
    {
        FilePath = filePath;
        DisplayName = displayName;
    }

    /// <summary>截图文件的完整路径。</summary>
    public string FilePath { get; }

    /// <summary>用于提示框显示的相对路径。</summary>
    public string DisplayName { get; }

    /// <summary>解码后的缩略图，解码完成前为 <see langword="null" />。</summary>
    [ObservableProperty] private BitmapImage? _thumbnail;
}
