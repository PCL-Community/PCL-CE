using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace PCL.UI.Controls;

/// <summary>
///     列表项的本地位图 Logo。
///     图片拉伸填满整个单元格（不保持宽高比），缩放方式按 <see cref="UseNearestNeighbor" /> 选择：
///     皮肤头像与披风缓存使用最近邻，保留像素画边缘；其余位图使用高质量插值。
///     圆角通过 <see cref="Visual.Clip" /> 裁剪，四角按 <see cref="MyImage.CornerRadius" /> 中最大的半径取圆角。
/// </summary>
internal sealed class ListItemLogoImage : MyImage
{
    private bool _useNearestNeighbor;

    public ListItemLogoImage()
    {
        Stretch = Stretch.Fill;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
    }

    /// <summary>
    ///     是否使用最近邻缩放。用于像素画尺寸较小的皮肤头像与披风图标。
    /// </summary>
    public bool UseNearestNeighbor
    {
        get => _useNearestNeighbor;
        set
        {
            if (_useNearestNeighbor == value)
                return;
            _useNearestNeighbor = value;
            ApplyInterpolationMode();
        }
    }

    /// <summary>
    ///     位图不参与尺寸计算，始终由所在单元格决定大小，避免大图把列表项的自动行撑高。
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        return default;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == BoundsProperty || change.Property == CornerRadiusProperty)
            UpdateRoundedClip();
    }

    private void ApplyInterpolationMode()
    {
        RenderOptions.SetBitmapInterpolationMode(
            this,
            _useNearestNeighbor
                ? BitmapInterpolationMode.None
                : BitmapInterpolationMode.HighQuality);
    }

    private void UpdateRoundedClip()
    {
        ApplyInterpolationMode();

        var width = Bounds.Width;
        var height = Bounds.Height;
        var cornerRadius = CornerRadius;
        if (width <= 0d
            || height <= 0d
            || cornerRadius is not
            {
                TopLeft: >= 0d,
                TopRight: >= 0d,
                BottomLeft: >= 0d,
                BottomRight: >= 0d
            })
        {
            Clip = null;
            return;
        }

        var radius = Math.Max(
            Math.Max(cornerRadius.TopLeft, cornerRadius.TopRight),
            Math.Max(cornerRadius.BottomLeft, cornerRadius.BottomRight));
        Clip = new RectangleGeometry(new Rect(0d, 0d, width, height), radius, radius);
    }
}