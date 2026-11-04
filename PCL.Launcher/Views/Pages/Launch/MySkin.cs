using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     皮肤头像控件：按皮肤文件裁出脸部（底层）与帽子层（顶层）并叠放显示，带随悬停加深的阴影。
///     支持本地文件、<c>avares://</c> 内置贴图与网络地址（下载后裁切）。
///     右键菜单提供另存为、刷新与更换披风；点击时缩放反馈，供启动页账号区打开档案操作。
/// </summary>
public sealed class MySkin : Grid
{
    private const double ShadowIdleOpacity = 0.2d;

    private const double ShadowHoverOpacity = 0.8d;

    private readonly MyDropShadow _shadow = new()
    {
        Margin = new Thickness(-3d, -3d, -3d, -4d),
        ShadowRadius = 3d,
        CornerRadius = new CornerRadius(5d),
        Opacity = ShadowIdleOpacity,
        IsHitTestVisible = false
    };

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

    private string _address = "";
    private bool _isMouseDown;
    private bool _hasCape = true;
    private int _loadRevision;

    public MySkin()
    {
        Width = 64d;
        Height = 64d;
        RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        Bind(BackgroundProperty, new DynamicResourceExtension("ColorBrushSemiTransparent"));
        ToolTip.SetTip(this, Lang.Text("Launch.Skin.Change.ToolTip"));

        ContainerTheme.BindDynamic(_shadow, MyDropShadow.ColorProperty, "ColorObject2");

        RenderOptions.SetBitmapInterpolationMode(_imgBack, BitmapInterpolationMode.None);
        RenderOptions.SetBitmapInterpolationMode(_imgFore, BitmapInterpolationMode.None);
        Children.Add(_shadow);
        Children.Add(_imgBack);
        Children.Add(_imgFore);

        BuildContextMenu();

        PointerEntered += (_, _) =>
        {
            AniStart(AaOpacity(_shadow, ShadowHoverOpacity - _shadow.Opacity, 200, 100), "Skin Shadow");
        };
        PointerExited += (_, _) =>
        {
            AniStart(AaOpacity(_shadow, ShadowIdleOpacity - _shadow.Opacity, 200), "Skin Shadow");
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

    public event EventHandler? Click;

    public event EventHandler? CapeRequested;

    public bool HasCape
    {
        get => _hasCape;
        set
        {
            _hasCape = value;
            if (_menuCape is not null)
                _menuCape.IsVisible = value;
        }
    }

    public string Address
    {
        get => _address;
        set
        {
            if (string.Equals(_address, value, StringComparison.Ordinal))
                return;
            _address = value ?? "";
            ToolTip.SetTip(
                this,
                Lang.Text(string.IsNullOrEmpty(_address)
                    ? "Common.State.Loading"
                    : "Launch.Skin.Change.ToolTip"));
            _ = LoadAsync();
        }
    }

    public void Clear()
    {
        Address = "";
        _imgBack.Source = null;
        _imgFore.Source = null;
    }

    public void RefreshSource()
    {
        _imgBack.Source = null;
        _imgFore.Source = null;
        _ = LoadAsync();
    }

    public async Task SaveSkinAsync()
    {
        var address = _address;
        if (string.IsNullOrWhiteSpace(address))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.Fetching")));
            return;
        }

        try
        {
            var localPath = address;
            if (address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                localPath = await PCL.UI.Controls.MyImage.DownloadImageAsync(address).ConfigureAwait(false);
            if (string.IsNullOrEmpty(localPath))
                throw new InvalidDataException("皮肤文件不存在");

            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Launch.Skin.SaveDialog.Title"),
                Path.GetFileName(address.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                    ? address
                    : "Skin.png"),
                Lang.Text("Launch.Skin.SaveDialog.Filter"));
            if (string.IsNullOrWhiteSpace(target))
                return;

            await Task.Run(() =>
            {
                using var source = OpenStream(localPath);
                if (source is null)
                    throw new FileNotFoundException("皮肤文件不存在", localPath);
                using var destination = File.Create(target);
                source.CopyTo(destination);
            });
            HintService.Show(Hints.Success(Lang.Text("Launch.Skin.SaveAs")));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "保存皮肤失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.File.Error")));
        }
    }

    private MyMenuItem? _menuCape;

    private void BuildContextMenu()
    {
        var saveItem = new MyMenuItem
        {
            Header = Lang.Text("Launch.Skin.SaveAs"),
            SvgIcon = "lucide/download"
        };
        saveItem.Click += (_, _) => _ = SaveSkinAsync();

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
            IsVisible = _hasCape
        };
        _menuCape.Click += (_, _) => CapeRequested?.Invoke(this, EventArgs.Empty);

        ContextMenu = new ContextMenu
        {
            ItemsSource = new List<MyMenuItem> { saveItem, refreshItem, _menuCape }
        };
    }

    private ScaleTransform GetScale()
    {
        if (RenderTransform is ScaleTransform scale)
            return scale;
        scale = new ScaleTransform(1d, 1d);
        RenderTransform = scale;
        return scale;
    }

    private async Task LoadAsync()
    {
        var revision = ++_loadRevision;
        var address = _address;
        if (string.IsNullOrWhiteSpace(address))
        {
            _imgBack.Source = null;
            _imgFore.Source = null;
            return;
        }

        try
        {
            var localPath = address;
            if (address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                localPath = await PCL.UI.Controls.MyImage.DownloadImageAsync(address).ConfigureAwait(false);
                if (string.IsNullOrEmpty(localPath))
                    throw new FileNotFoundException("皮肤下载失败", address);
            }

            using var stream = OpenStream(localPath);
            if (stream is null)
                throw new FileNotFoundException("皮肤文件不存在", localPath);
            var bitmap = new Bitmap(stream);
            if (revision != _loadRevision)
                return;

            if (bitmap.PixelSize.Width < 32 || bitmap.PixelSize.Height < 32)
                throw new InvalidDataException(
                    $"图片大小不足，宽 {bitmap.PixelSize.Width}，高 {bitmap.PixelSize.Height}");

            var scale = Math.Max(1, (int)Math.Round(bitmap.PixelSize.Width / 64d));
            var face = Crop(bitmap, scale * 8, scale * 8);
            var hat = bitmap.PixelSize.Width >= 64 && bitmap.PixelSize.Height >= 32
                ? Crop(bitmap, scale * 40, scale * 8)
                : null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (revision != _loadRevision)
                    return;
                _imgBack.Source = face;
                _imgFore.Source = hat;
            });
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Launch", $"加载皮肤失败：{address}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (revision != _loadRevision)
                    return;
                _imgBack.Source = null;
                _imgFore.Source = null;
            });
        }
    }

    private static Stream? OpenStream(string path)
    {
        if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return AssetLoader.Exists(new Uri(path)) ? AssetLoader.Open(new Uri(path)) : null;
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    private static CroppedBitmap Crop(Bitmap source, int x, int y)
    {
        var size = Math.Max(1, source.PixelSize.Width / 8);
        var rect = new PixelRect(x, y, size, size);
        return new CroppedBitmap(source, rect);
    }
}
