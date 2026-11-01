using Avalonia.Controls;

namespace PCL.Avalonia.Views.Pages;

/// <summary>测速页右侧容器：内容由测速流程动态填充，进入页面时回到顶部。</summary>
public partial class PageSpeedRight : PageBase
{
    private readonly ScrollViewer? _panBack;

    public PageSpeedRight()
    {
        InitializeComponent();
        _panBack = this.FindControl<ScrollViewer>("PanBack");
    }

    public override void OnNavigatedTo(object? parameter)
    {
        _panBack?.Offset = default;
    }
}