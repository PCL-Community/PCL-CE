// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Instances.Views;

public partial class MyLocalModItem : Grid
{
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private TextBlock? _info;
    private MyImage? _logo;
    private StackPanel? _tagsPanel;
    private ColumnDefinition? _paddingRightColumn;
    private Border? _rectBack;
    private Border? _rectCheck;
    private Image? _stateImage;
    private Control? _buttonStack;
    private bool _checked;
    private bool _isPressed;
    private bool _isLoaded;
    private bool _isSettingChecked;
    private string? _lastColorState;

    public MyLocalModItem()
    {
        AvaloniaXamlLoader.Load(this);

        _title = this.FindControl<TextBlock>("LabTitle");
        _subtitle = this.FindControl<TextBlock>("LabSubtitle");
        _info = this.FindControl<TextBlock>("LabInfo");
        _logo = this.FindControl<MyImage>("PathLogo");
        _tagsPanel = this.FindControl<StackPanel>("PanTags");
        _paddingRightColumn = ColumnDefinitions.Count > 5 ? ColumnDefinitions[5] : null;

        PointerEntered += (_, _) => RefreshColor(animate: true);
        PointerExited += (_, _) =>
        {
            _isPressed = false;
            RefreshColor(animate: true);
        };
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            SyncVisuals(animate: false);
        };
        SizeChanged += (_, _) => CompressTitleColumns();

        SyncVisuals(animate: false);
    }

    public event EventHandler<PointerReleasedEventArgs>? Click;

    public event EventHandler<RouteEventArgs>? Check;

    public event EventHandler<RouteEventArgs>? Changed;

    public int Uuid { get; } = Random.Shared.Next();

    public string Logo
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplyLogo();
        }
    } = string.Empty;

    public string Title
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplyTitle();
        }
    } = string.Empty;

    public string SubTitle
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplySubtitle();
        }
    } = string.Empty;

    public string Description
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            if (_info is not null)
                _info.Text = value;
        }
    } = string.Empty;

    public ResourceItemState State
    {
        get => field;
        set
        {
            if (field == value)
                return;

            field = value;
            ApplyTitle();
            ApplyStateIcon();
            ApplyTitleForeground(animate: false);
        }
    }

    public bool Checked
    {
        get => _checked;
        set => SetChecked(value, user: false);
    }

    public IList<MyIconButton> Buttons
    {
        get => field;
        set
        {
            field = value;
            ApplyButtons();
            if (_buttonStack is not null && !IsPointerOver)
                SetRightPaddingWidth(4d);
        }
    } = [];

    public IList<string> Tags
    {
        get => field;
        set
        {
            field = value;
            ApplyTags();
        }
    } = [];

    public void SetChecked(bool value, bool user = false)
    {
        if (_checked == value && !_isSettingChecked)
            return;

        bool oldValue = _checked;
        RouteEventArgs changedArgs = new(user);
        _isSettingChecked = true;
        try
        {
            _checked = value;
            Changed?.Invoke(this, changedArgs);
            if (changedArgs.Handled)
            {
                _checked = oldValue;
                return;
            }
        }
        finally
        {
            _isSettingChecked = false;
        }

        if (value && user)
            Check?.Invoke(this, new RouteEventArgs(user));

        RefreshCheckedVisual(_isLoaded);
        RefreshColor(_isLoaded);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEnabled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isPressed = true;
        if (_buttonStack is not null)
            _buttonStack.IsHitTestVisible = false;
        Focus();
        RefreshColor(animate: true);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPressed)
            return;

        _isPressed = false;
        if (_buttonStack is not null)
            _buttonStack.IsHitTestVisible = true;
        Click?.Invoke(this, e);
        RefreshColor(animate: true);
        e.Handled = true;
    }

    private void SyncVisuals(bool animate)
    {
        ApplyLogo();
        ApplyTitle();
        ApplySubtitle();
        if (_info is not null)
            _info.Text = Description;
        ApplyTags();
        ApplyButtons();
        ApplyStateIcon();
        RefreshCheckedVisual(animate);
        RefreshColor(animate);
    }

    private void ApplyLogo()
    {
        if (_logo is null)
            return;

        _logo.Source = LoadLogoImage(string.IsNullOrWhiteSpace(Logo)
            ? InstanceDisplayHelper.ImageAssetRoot + "Icons/NoIcon.png"
            : Logo);
    }

    private void ApplyTitle()
    {
        if (_title is null)
            return;

        string title = Title;
        _title.TextDecorations = null;
        if (State == ResourceItemState.Disabled)
        {
            _title.TextDecorations = TextDecorations.Strikethrough;
        }
        else if (State == ResourceItemState.Unavailable)
        {
            _title.TextDecorations = TextDecorations.Strikethrough;
            title += "（不可用）";
        }

        _title.Text = title;
        ApplyTitleForeground(animate: false);
        CompressTitleColumns();
    }

    private void ApplySubtitle()
    {
        if (_subtitle is null)
            return;

        _subtitle.Text = SubTitle;
        _subtitle.IsVisible = !string.IsNullOrWhiteSpace(SubTitle);
        CompressTitleColumns();
    }

    private void ApplyTags()
    {
        if (_tagsPanel is null)
            return;

        _tagsPanel.Children.Clear();
        _tagsPanel.IsVisible = Tags.Count > 0;
        if (_info is not null)
        {
            Grid.SetColumn(_info, Tags.Count > 0 ? 4 : 3);
            Grid.SetColumnSpan(_info, Tags.Count > 0 ? 1 : 2);
        }

        foreach (string tagText in Tags.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            Border tag = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(12, 0, 0, 0)),
                Padding = new Thickness(3d, 1d, 3d, 1d),
                CornerRadius = new CornerRadius(3d),
                Margin = new Thickness(0d, 0d, 3d, 0d),
                Child = new TextBlock
                {
                    Text = tagText,
                    Foreground = FindBrush("ColorBrushGray2", "#868686"),
                    FontSize = 11d
                }
            };
            _tagsPanel.Children.Add(tag);
        }
    }

    private void ApplyButtons()
    {
        // Detach previous button host first. Removing the StackPanel alone does not
        // clear its Children, so MyIconButton instances keep a visual parent and
        // re-adding them throws (Avalonia: already has a visual parent).
        if (_buttonStack is Panel oldHost)
        {
            oldHost.Children.Clear();
            Children.Remove(oldHost);
            _buttonStack = null;
        }

        if (Buttons.Count == 0)
            return;

        StackPanel stack = new()
        {
            Opacity = IsPointerOver ? 1d : 0d,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        foreach (MyIconButton button in Buttons)
        {
            // Same button instance may still be parented if ApplyButtons ran twice
            // (ctor SyncVisuals + AttachedToVisualTree) before the host was cleared.
            if (button.Parent is Panel existingParent)
                existingParent.Children.Remove(button);

            if (double.IsNaN(button.Height))
                button.Height = 25d;
            if (double.IsNaN(button.Width))
                button.Width = 25d;
            stack.Children.Add(button);
        }

        Grid.SetColumnSpan(stack, 10);
        Grid.SetRowSpan(stack, 10);
        _buttonStack = stack;
        Children.Add(stack);
    }

    private Border EnsureRectBack()
    {
        if (_rectBack is not null)
            return _rectBack;

        _rectBack = new Border
        {
            Name = "RectBack",
            CornerRadius = new CornerRadius(3d),
            RenderTransform = new ScaleTransform(0.8d, 0.8d),
            RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
            BorderThickness = new Thickness(1d),
            IsHitTestVisible = false,
            Opacity = 0d,
            Background = FindBrush("ColorBrush7", "#e0eafd"),
            BorderBrush = FindBrush("ColorBrush6", "#d5e6fd")
        };
        Grid.SetColumnSpan(_rectBack, 999);
        Grid.SetRowSpan(_rectBack, 999);
        Children.Insert(0, _rectBack);
        return _rectBack;
    }

    private Border EnsureRectCheck()
    {
        if (_rectCheck is not null)
            return _rectCheck;

        _rectCheck = new Border
        {
            Width = 5d,
            Height = Checked ? 32d : 0d,
            CornerRadius = new CornerRadius(2d),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Margin = new Thickness(-3d, 0d, 0d, 0d),
            Background = FindBrush("ColorBrush3", "#1370f3"),
            IsHitTestVisible = false,
            Opacity = Checked ? 1d : 0d
        };
        Grid.SetRowSpan(_rectCheck, 10);
        Children.Add(_rectCheck);
        return _rectCheck;
    }

    private void RefreshCheckedVisual(bool animate)
    {
        Border indicator = EnsureRectCheck();
        ModAnimation.AniStop($"MyLocalCompItem Checked {Uuid}");
        indicator.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        indicator.Margin = new Thickness(-3d, 0d, 0d, 0d);

        if (!animate)
        {
            indicator.Height = Checked ? 32d : 0d;
            indicator.Opacity = Checked ? 1d : 0d;
            ApplyTitleForeground(animate: false);
            return;
        }

        List<ModAnimation.AniData> animations = [];
        if (Checked)
        {
            double delta = 32d - indicator.Bounds.Height;
            animations.Add(ModAnimation.AaHeight(indicator, delta * 0.4d, 200, ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaHeight(indicator, delta * 0.6d, 300, ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaOpacity(indicator, 1d - indicator.Opacity, 30));
            if (_title is not null)
                animations.Add(ModAnimation.AaColor(_title, TextBlock.ForegroundProperty, CheckedTitleBrushKey, 200));
        }
        else
        {
            animations.Add(ModAnimation.AaHeight(indicator, -indicator.Bounds.Height, 120, ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)));
            animations.Add(ModAnimation.AaOpacity(indicator, -indicator.Opacity, 70, 40));
            if (_title is not null)
                animations.Add(ModAnimation.AaColor(_title, TextBlock.ForegroundProperty, NormalTitleBrushKey, 120));
        }

        ModAnimation.AniStart(animations, $"MyLocalCompItem Checked {Uuid}");
    }

    private void RefreshColor(bool animate)
    {
        string state = _isPressed ? "MouseDown" : IsPointerOver ? "MouseOver" : Checked ? "Checked" : "Idle";
        if (_lastColorState == state && animate)
            return;
        _lastColorState = state;

        if (!animate)
        {
            if (state is "MouseDown" or "MouseOver" or "Checked")
            {
                Border rect = EnsureRectBack();
                rect.Background = FindBrush(state == "MouseDown" ? "ColorBrush6" : "ColorBrushBg1", "#bee0eafd");
                rect.Opacity = 1d;
                rect.RenderTransform = new ScaleTransform(state == "MouseDown" ? 0.996d : 1d, state == "MouseDown" ? 0.996d : 1d);
                SetButtonStackOpacity(IsPointerOver ? 1d : 0d);
                SetRightPaddingWidth(IsPointerOver ? GetExpandedPaddingRight() : 4d);
            }
            else
            {
                if (_rectBack is not null)
                {
                    _rectBack.Opacity = 0d;
                    _rectBack.RenderTransform = new ScaleTransform(0.8d, 0.8d);
                }
                SetButtonStackOpacity(0d);
                SetRightPaddingWidth(4d);
            }
            ApplyTitleForeground(animate: false);
            return;
        }

        int time = IsPointerOver ? 120 : 180;
        List<ModAnimation.AniData> animations = [];
        if (_buttonStack is not null)
        {
            if (IsPointerOver)
            {
                animations.Add(ModAnimation.AaOpacity(_buttonStack, 1d - _buttonStack.Opacity, (int)Math.Round(time * 0.7d), (int)Math.Round(time * 0.3d)));
                animations.Add(ModAnimation.AaDouble(
                    value => SetRightPaddingWidth(GetRightPaddingWidth() + value),
                    GetExpandedPaddingRight() - GetRightPaddingWidth(),
                    (int)Math.Round(time * 0.3d),
                    (int)Math.Round(time * 0.7d)));
            }
            else
            {
                animations.Add(ModAnimation.AaOpacity(_buttonStack, -_buttonStack.Opacity, (int)Math.Round(time * 0.4d)));
                animations.Add(ModAnimation.AaDouble(
                    value => SetRightPaddingWidth(GetRightPaddingWidth() + value),
                    4d - GetRightPaddingWidth(),
                    (int)Math.Round(time * 0.4d)));
            }
        }

        if (IsPointerOver || Checked || _isPressed)
        {
            Border rect = EnsureRectBack();
            animations.Add(ModAnimation.AaColor(rect, Border.BackgroundProperty, _isPressed ? "ColorBrush6" : "ColorBrushBg1", time));
            animations.Add(ModAnimation.AaOpacity(rect, 1d - rect.Opacity, time, ease: new ModAnimation.AniEaseOutFluent()));
            animations.Add(ModAnimation.AaScaleTransform(rect, (_isPressed ? 0.996d : 1d) - GetScaleX(rect), (int)Math.Round(time * 1.2d), ease: new ModAnimation.AniEaseOutFluent()));
        }
        else if (_rectBack is not null)
        {
            animations.Add(ModAnimation.AaOpacity(_rectBack, -_rectBack.Opacity, time));
            animations.Add(ModAnimation.AaScaleTransform(_rectBack, 0.996d - GetScaleX(_rectBack), time, ease: new ModAnimation.AniEaseOutFluent()));
            animations.Add(ModAnimation.AaScaleTransform(_rectBack, -0.196d, 1, after: true));
        }

        ModAnimation.AniStart(animations, $"LocalModItem Color {Uuid}");
        ApplyTitleForeground(animate: true);
    }

    private void ApplyTitleForeground(bool animate)
    {
        if (_title is null)
            return;

        string brushKey = Checked ? CheckedTitleBrushKey : NormalTitleBrushKey;
        if (animate)
            ModAnimation.AniStart(ModAnimation.AaColor(_title, TextBlock.ForegroundProperty, brushKey, 120), $"LocalModItem Title {Uuid}");
        else
            _title.Foreground = FindBrush(brushKey, Checked ? "#1370f3" : "#343d4a");
    }

    private string CheckedTitleBrushKey =>
        State == ResourceItemState.Fine ? "ColorBrush2" : "ColorBrush5";

    private string NormalTitleBrushKey =>
        State == ResourceItemState.Fine ? "ColorBrush1" : "ColorBrushGray4";

    private void ApplyStateIcon()
    {
        if (State == ResourceItemState.Fine)
        {
            if (_stateImage is not null)
            {
                Children.Remove(_stateImage);
                _stateImage = null;
            }
            return;
        }

        _stateImage ??= CreateStateImage();
        string iconName = State == ResourceItemState.Disabled ? "Disabled.png" : "Unavailable.png";
        _stateImage.Source = LoadLogoImage(InstanceDisplayHelper.ImageAssetRoot + "Icons/" + iconName);
    }

    private Image CreateStateImage()
    {
        Image image = new()
        {
            Width = 20d,
            Height = 20d,
            Margin = new Thickness(0d, 0d, -5d, -3d),
            IsHitTestVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
            Stretch = Stretch.Uniform
        };
        Grid.SetColumn(image, 1);
        Grid.SetRow(image, 1);
        Grid.SetRowSpan(image, 2);
        Children.Add(image);
        return image;
    }

    private void CompressTitleColumns()
    {
        if (this.FindControl<Grid>("PanTitle") is not { } panTitle ||
            panTitle.ColumnDefinitions.Count < 4 ||
            _subtitle is null)
        {
            return;
        }

        ColumnDefinition title = panTitle.ColumnDefinitions[0];
        ColumnDefinition subtitle = panTitle.ColumnDefinitions[1];
        ColumnDefinition extend = panTitle.ColumnDefinitions[3];
        double width = Bounds.Width;
        if (width <= 0d)
            return;

        if (!_subtitle.IsVisible || width < 360d)
        {
            title.Width = new GridLength(1d, GridUnitType.Star);
            subtitle.Width = new GridLength(0d);
            extend.Width = new GridLength(0d);
        }
        else if (width < 520d)
        {
            title.Width = GridLength.Auto;
            subtitle.Width = new GridLength(1d, GridUnitType.Star);
            extend.Width = new GridLength(0d);
        }
        else
        {
            title.Width = GridLength.Auto;
            subtitle.Width = GridLength.Auto;
            extend.Width = new GridLength(1d, GridUnitType.Star);
        }
    }

    private void SetButtonStackOpacity(double opacity)
    {
        if (_buttonStack is not null)
            _buttonStack.Opacity = opacity;
    }

    private double GetRightPaddingWidth() => _paddingRightColumn?.Width.Value ?? 4d;

    private void SetRightPaddingWidth(double value)
    {
        if (_paddingRightColumn is not null)
            _paddingRightColumn.Width = new GridLength(Math.Max(0d, value));
    }

    private double GetExpandedPaddingRight() =>
        Math.Max(4d, 5d + Buttons.Count * 25d);

    private IBrush FindBrush(string key, string fallback)
    {
        if (Avalonia.Application.Current is { } application &&
            application.TryGetResource(key, null, out object? appResource))
        {
            return BrushFromResource(appResource, fallback);
        }

        return TryGetResource(key, null, out object? resource)
            ? BrushFromResource(resource, fallback)
            : new SolidColorBrush(Color.Parse(fallback));
    }

    private static IBrush BrushFromResource(object? resource, string fallback) =>
        resource switch
        {
            IBrush brush => brush,
            Color color => new SolidColorBrush(color),
            _ => new SolidColorBrush(Color.Parse(fallback))
        };

    private static Bitmap? LoadLogoImage(string logo)
    {
        try
        {
            string normalized = NormalizeLogoUri(logo);
            if (File.Exists(normalized))
            {
                using Stream fileStream = File.OpenRead(normalized);
                return new Bitmap(fileStream);
            }

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
                return null;

            using Stream stream = uri.IsFile ? File.OpenRead(uri.LocalPath) : AssetLoader.Open(uri);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string NormalizeLogoUri(string logo)
    {
        const string wpfImagePrefix = "pack://application:,,,/images/";
        return logo.StartsWith(wpfImagePrefix, StringComparison.OrdinalIgnoreCase)
            ? InstanceDisplayHelper.ImageAssetRoot + logo[wpfImagePrefix.Length..].Replace('\\', '/')
            : logo;
    }

    private static double GetScaleX(Control control) =>
        control.RenderTransform switch
        {
            ScaleTransform scale => scale.ScaleX,
            TransformGroup group => group.Children.OfType<ScaleTransform>().FirstOrDefault()?.ScaleX ?? 1d,
            _ => 1d
        };
}

public enum ResourceItemState
{
    Fine,
    Disabled,
    Unavailable
}
