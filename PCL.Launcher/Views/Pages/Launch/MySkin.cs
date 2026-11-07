using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     皮肤头像控件：按皮肤文件裁出脸部（底层）与帽子层（顶层）并叠放显示，带随悬停加深的阴影。
///     纹理地址、加载状态与菜单命令由 <see cref="MySkinViewModel" /> 负责；控件保留指针动画、
///     右键菜单构建与图像源更新。
/// </summary>
public sealed class MySkin : Grid
{
    private const double ShadowIdleOpacity = 0.2d;

    private const double ShadowHoverOpacity = 0.8d;

    private readonly Image _imgBack = new()
    {
        Width = 48d,
        Height = 48d,
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly Image _imgFore = new()
    {
        Width = 56d,
        Height = 56d,
        Stretch = Stretch.Fill,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly DropShadowEffect _shadow = new()
    {
        BlurRadius = 10d,
        OffsetX = 0d,
        OffsetY = 0d,
        Opacity = ShadowIdleOpacity
    };

    private bool _isMouseDown;

    private MyMenuItem? _menuCape;
    private MyMenuItem? _menuSave;

    public MySkin()
    {
        Width = 64d;
        Height = 64d;
        RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        Bind(BackgroundProperty, new DynamicResourceExtension("ColorBrushSemiTransparent"));
        ToolTip.SetTip(this, ViewModel.ToolTipText ?? string.Empty);

        Effect = _shadow;
        ApplyShadowColor();
        AttachedToVisualTree += (_, _) => ApplyShadowColor();
        ActualThemeVariantChanged += (_, _) => ApplyShadowColor();

        RenderOptions.SetBitmapInterpolationMode(_imgBack, BitmapInterpolationMode.None);
        RenderOptions.SetBitmapInterpolationMode(_imgFore, BitmapInterpolationMode.None);
        Children.Add(_imgBack);
        Children.Add(_imgFore);

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.CapeRequested += () => CapeRequested?.Invoke(this, EventArgs.Empty);

        BuildContextMenu();
        ContextMenuHelper.Attach(this, () => ContextMenu);

        PointerEntered += (_, _) =>
        {
            AniStart(
                AaDouble(
                    delta => _shadow.Opacity += delta,
                    ShadowHoverOpacity - _shadow.Opacity,
                    200,
                    100),
                "Skin Shadow");
        };
        PointerExited += (_, _) =>
        {
            AniStart(
                AaDouble(
                    delta => _shadow.Opacity += delta,
                    ShadowIdleOpacity - _shadow.Opacity,
                    200),
                "Skin Shadow");
            _isMouseDown = false;
            AniStart(
                AaScaleTransform(this, 1d - GetScale().ScaleX, 60, ease: new AniEaseOutFluent()),
                "Skin Scale");
        };
        PointerPressed += (_, _) =>
        {
            _isMouseDown = true;
            AniStart(
                AaScaleTransform(this, 0.9d - GetScale().ScaleX, 60, ease: new AniEaseOutFluent()),
                "Skin Scale");
        };
        PointerReleased += (_, _) =>
        {
            AniStart(
                AaScaleTransform(this, 1d - GetScale().ScaleX, 60, ease: new AniEaseOutFluent()),
                "Skin Scale");
            if (!_isMouseDown) return;
            _isMouseDown = false;
            Click?.Invoke(this, EventArgs.Empty);
        };
    }

    public MySkinViewModel ViewModel { get; } = new();

    public bool HasCape
    {
        get => ViewModel.HasCape;
        set => ViewModel.HasCape = value;
    }

    public bool CanSave => ViewModel.CanSave;

    public string Address
    {
        get => ViewModel.Address;
        set => ViewModel.Address = value ?? "";
    }

    public event EventHandler? Click;

    public event EventHandler? CapeRequested;

    public void Clear()
    {
        ViewModel.Clear();
    }

    public void RefreshSource()
    {
        ViewModel.RefreshSource();
    }

    public async Task SaveSkinAsync()
    {
        await ViewModel.SaveSkinAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MySkinViewModel.ToolTipText):
                ToolTip.SetTip(this, ViewModel.ToolTipText ?? string.Empty);
                break;
            case nameof(MySkinViewModel.FaceImage):
                _imgBack.Source = ViewModel.FaceImage;
                break;
            case nameof(MySkinViewModel.HatImage):
                _imgFore.Source = ViewModel.HatImage;
                break;
            case nameof(MySkinViewModel.HasCape):
                _menuCape?.IsVisible = ViewModel.HasCape;
                break;
            case nameof(MySkinViewModel.CanSave):
                _menuSave?.IsEnabled = ViewModel.CanSave;
                break;
        }
    }

    private void BuildContextMenu()
    {
        _menuSave = new MyMenuItem
        {
            Header = Lang.Text("Launch.Skin.SaveAs"),
            SvgIcon = "lucide/download",
            IsEnabled = ViewModel.CanSave
        };
        _menuSave.Click += (_, _) => _ = SaveSkinAsync();

        var refreshItem = new MyMenuItem
        {
            Header = Lang.Text("Common.Action.Refresh"),
            SvgIcon = "lucide/refresh-cw"
        };
        refreshItem.Click += (_, _) => RefreshSource();

        _menuCape = new MyMenuItem
        {
            Header = Lang.Text("Launch.Skin.ChangeCape"),
            SvgIcon = "lucide/shirt",
            IsVisible = ViewModel.HasCape
        };
        _menuCape.Click += (_, _) => ViewModel.ChangeCapeCommand.Execute(null);

        ContextMenu = new ContextMenu
        {
            ItemsSource = new List<MyMenuItem>
            {
                _menuSave,
                refreshItem,
                _menuCape
            }
        };
    }

    private void ApplyShadowColor()
    {
        if (this.TryFindResource("ColorObject2", out var value) && value is Color color)
            _shadow.Color = color;
    }

    private ScaleTransform GetScale()
    {
        if (RenderTransform is ScaleTransform scale)
            return scale;
        scale = new ScaleTransform(1d, 1d);
        RenderTransform = scale;
        return scale;
    }
}