using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace PCL.UI.Controls;

/// <summary>
///     带 SvgIcon 的菜单项：SvgIcon 变化时重建 16×16 图标，颜色状态由样式与主题资源驱动。
/// </summary>
public class MyMenuItem : MenuItem
{
    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(SvgIcon), string.Empty);

    public int Uuid { get; } = Guid.NewGuid().GetHashCode();

    public MyMenuItem()
    {
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        IsEnabledProperty.Changed.AddClassHandler<MyMenuItem>((item, _) => item.RefreshColor());
        SvgIconProperty.Changed.AddClassHandler<MyMenuItem>((item, _) => item.UpdateIcon());
    }

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    private void UpdateIcon()
    {
        if (string.IsNullOrWhiteSpace(SvgIcon))
        {
            Icon = null;
            return;
        }

        Icon = new SvgIcon.SvgIcon
        {
            Icon = SvgIcon,
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private void RefreshColor()
    {
        InvalidateVisual();
    }
}