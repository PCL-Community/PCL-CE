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

namespace PCL.Desktop.Controls.Legacy;

public partial class MySkin : Grid
{
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MySkin, string>(nameof(Address), string.Empty);

    public static readonly StyledProperty<bool> HasCapeProperty =
        AvaloniaProperty.Register<MySkin, bool>(nameof(HasCape));

    private readonly Image? _backImage;
    private readonly Image? _frontImage;
    private readonly Border? _shadow;
    private bool _isSkinMouseDown;

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
        this.GetObservable(AddressProperty).Subscribe(_ => Load());
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

    public void Load()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Address) || !File.Exists(Address))
            {
                Clear();
                return;
            }

            using FileStream stream = File.OpenRead(Address);
            Bitmap bitmap = new(stream);
            PixelSize size = bitmap.PixelSize;
            if (size.Width < 32 || size.Height < 32)
            {
                Clear();
                return;
            }

            int scale = Math.Max(1, (int)Math.Round(size.Width / 64d));
            _backImage!.Source = Crop(bitmap, scale * 8, scale * 8, scale * 8, scale * 8);
            _frontImage!.Source = size.Width >= 64 && size.Height >= 32
                ? Crop(bitmap, scale * 40, scale * 8, scale * 8, scale * 8)
                : null;
        }
        catch (IOException)
        {
            Clear();
        }
        catch (UnauthorizedAccessException)
        {
            Clear();
        }
    }

    public void Clear()
    {
        if (_frontImage is not null)
            _frontImage.Source = null;
        if (_backImage is not null)
            _backImage.Source = null;
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
        RenderTransform = new ScaleTransform(1d, 1d);
    }

    private void PanSkin_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isSkinMouseDown = true;
        RenderTransform = new ScaleTransform(0.9d, 0.9d);
        e.Handled = true;
    }

    private void PanSkin_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        RenderTransform = new ScaleTransform(1d, 1d);
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
