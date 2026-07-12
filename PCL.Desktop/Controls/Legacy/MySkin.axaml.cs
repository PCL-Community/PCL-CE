// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public partial class MySkin : Grid
{
    private static readonly HttpClient SkinClient = CreateSkinClient();
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MySkin, string>(nameof(Address), string.Empty);

    public static readonly StyledProperty<bool> HasCapeProperty =
        AvaloniaProperty.Register<MySkin, bool>(nameof(HasCape));

    private readonly Image? _backImage;
    private readonly Image? _frontImage;
    private readonly Border? _shadow;
    private Bitmap? _skinBitmap;
    private bool _isSkinMouseDown;
    private int _loadVersion;

    public MySkin()
    {
        AvaloniaXamlLoader.Load(this);
        _backImage = this.FindControl<Image>("ImgBack");
        _frontImage = this.FindControl<Image>("ImgFore");
        _shadow = this.FindControl<Border>("ShadowSkin");
        if (this.FindControl<MyMenuItem>("BtnSkinSave") is { } save)
        {
            save.Click += BtnSkinSaveClick;
            save.Checked += BtnSkinSaveChecked;
        }
        if (this.FindControl<MyMenuItem>("BtnSkinRefresh") is { } refresh)
            refresh.Click += RefreshClick;
        if (this.FindControl<MyMenuItem>("BtnSkinCape") is { } cape)
            cape.Click += BtnSkinCapeClick;

        PointerEntered += PanSkin_PointerEntered;
        PointerExited += PanSkin_PointerExited;
        PointerPressed += PanSkin_PointerPressed;
        PointerReleased += PanSkin_PointerReleased;
        this.GetObservable(AddressProperty).Subscribe(address =>
        {
            _ = LoadAsync();
        });
        this.GetObservable(HasCapeProperty).Subscribe(value =>
        {
            if (this.FindControl<MyMenuItem>("BtnSkinCape") is { } cape)
                cape.IsVisible = value;
        });
    }

    public event EventHandler<PointerReleasedEventArgs>? Click;

    public event EventHandler? SaveRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? CapeRequested;

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public bool HasCape
    {
        get => GetValue(HasCapeProperty);
        set => SetValue(HasCapeProperty, value);
    }

    public void Load() => _ = LoadAsync();

    private async Task LoadAsync()
    {
        string address = Address.Trim();
        int loadVersion = Interlocked.Increment(ref _loadVersion);
        try
        {
            byte[] bytes;
            if (File.Exists(address))
            {
                bytes = await File.ReadAllBytesAsync(address).ConfigureAwait(false);
            }
            else if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) &&
                     (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                uri = NormalizeSkinUri(uri);
                using HttpResponseMessage response = await SkinClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            else
            {
                await ClearIfCurrentAsync(loadVersion).ConfigureAwait(false);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using MemoryStream stream = new(bytes, writable: false);
                Bitmap bitmap = new(stream);
                PixelSize size = bitmap.PixelSize;
                if (loadVersion != _loadVersion || size.Width < 32 || size.Height < 32)
                {
                    bitmap.Dispose();
                    if (loadVersion == _loadVersion)
                        ClearImages();
                    return;
                }

                int scale = Math.Max(1, (int)Math.Round(size.Width / 64d));
                ClearImages();
                _skinBitmap = bitmap;
                _backImage!.Source = Crop(bitmap, scale * 8, scale * 8, scale * 8, scale * 8);
                _frontImage!.Source = size.Width >= 64 && size.Height >= 32
                    ? Crop(bitmap, scale * 40, scale * 8, scale * 8, scale * 8)
                    : null;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            await ClearIfCurrentAsync(loadVersion).ConfigureAwait(false);
        }
    }

    private Task ClearIfCurrentAsync(int loadVersion)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadVersion == _loadVersion)
                ClearImages();
        }).GetTask();
    }

    public void Clear()
    {
        Interlocked.Increment(ref _loadVersion);
        ClearImages();
    }

    private void ClearImages()
    {
        if (_frontImage is not null)
            _frontImage.Source = null;
        if (_backImage is not null)
            _backImage.Source = null;
        _skinBitmap?.Dispose();
        _skinBitmap = null;
    }

    private static HttpClient CreateSkinClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/png,image/*;q=0.8");
        return client;
    }

    private static Uri NormalizeSkinUri(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttp &&
            string.Equals(uri.Host, "textures.minecraft.net", StringComparison.OrdinalIgnoreCase))
        {
            return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        return uri;
    }

    public void BtnSkinSaveClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

    public void RefreshClick(object? sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);

    public void BtnSkinCapeClick(object? sender, RoutedEventArgs e) => CapeRequested?.Invoke(this, EventArgs.Empty);

    private void BtnSkinSaveChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is MyMenuItem item)
            item.IsEnabled = !string.IsNullOrWhiteSpace(Address);
    }

    private void PanSkin_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (_shadow is not null)
            _shadow.Opacity = 0.8d;
    }

    private void PanSkin_PointerExited(object? sender, PointerEventArgs e)
    {
        if (_shadow is not null)
            _shadow.Opacity = 0.2d;
        _isSkinMouseDown = false;
        ControlVisualHelpers.SetCenterScale(this, 1d);
    }

    private void PanSkin_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isSkinMouseDown = true;
        ControlVisualHelpers.SetCenterScale(this, 0.9d);
        e.Handled = true;
    }

    private void PanSkin_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ControlVisualHelpers.SetCenterScale(this, 1d);
        if (!_isSkinMouseDown)
            return;

        _isSkinMouseDown = false;
        Click?.Invoke(this, e);
        e.Handled = true;
    }

    private static CroppedBitmap Crop(Bitmap source, int x, int y, int width, int height) =>
        new()
        {
            Source = source,
            SourceRect = new PixelRect(x, y, width, height)
        };
}
