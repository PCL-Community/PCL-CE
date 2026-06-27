// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class SvgIcon : Control
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<string> DefaultPackProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(DefaultPack), SvgIconLoader.DefaultIconPack);

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<SvgIcon, IBrush?>(nameof(IconBrush), Brushes.Black);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SvgIcon, double>(nameof(StrokeThickness), 2d, validate: value => !double.IsNaN(value) && value >= 0d);

    public static readonly StyledProperty<bool> UseOriginalColorProperty =
        AvaloniaProperty.Register<SvgIcon, bool>(nameof(UseOriginalColor));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<SvgIcon, Stretch>(nameof(Stretch), Stretch.Uniform);

    private SvgIconModel? _model;
    private bool _modelLoaded;

    public SvgIcon()
    {
        this.GetObservable(IconProperty).Subscribe(_ => ResetModel());
        this.GetObservable(DefaultPackProperty).Subscribe(_ => ResetModel());
        this.GetObservable(IconBrushProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(StrokeThicknessProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(UseOriginalColorProperty).Subscribe(_ => InvalidateVisual());
        this.GetObservable(StretchProperty).Subscribe(_ =>
        {
            InvalidateMeasure();
            InvalidateVisual();
        });
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public string DefaultPack
    {
        get => GetValue(DefaultPackProperty);
        set => SetValue(DefaultPackProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public bool UseOriginalColor
    {
        get => GetValue(UseOriginalColorProperty);
        set => SetValue(UseOriginalColorProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        SvgIconModel? model = GetModel();
        Size naturalSize = model is null
            ? new Size(24d, 24d)
            : new Size(model.Width, model.Height);

        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
            return naturalSize;

        if (double.IsInfinity(availableSize.Width))
            return new Size(naturalSize.Width * availableSize.Height / naturalSize.Height, availableSize.Height);

        if (double.IsInfinity(availableSize.Height))
            return new Size(availableSize.Width, naturalSize.Height * availableSize.Width / naturalSize.Width);

        return availableSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        SvgIconModel? model = GetModel();
        if (model is null || model.Elements.Count == 0 || Bounds.Width <= 0d || Bounds.Height <= 0d)
            return;

        Rect target = CalculateTargetRect(new Size(model.Width, model.Height), Bounds.Size, Stretch);
        if (target.Width <= 0d || target.Height <= 0d)
            return;

        double scaleX = target.Width / model.Width;
        double scaleY = target.Height / model.Height;

        using (context.PushTransform(Matrix.CreateTranslation(target.X, target.Y)))
        using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY)))
        using (context.PushTransform(Matrix.CreateTranslation(-model.MinX, -model.MinY)))
        {
            SvgIconPaintOptions options = new(IconBrush ?? Brushes.Black, StrokeThickness, UseOriginalColor);
            foreach (SvgIconElement element in model.Elements)
                element.Draw(context, options);
        }
    }

    private SvgIconModel? GetModel()
    {
        if (_modelLoaded)
            return _model;

        _model = SvgIconLoader.Load(Icon, DefaultPack);
        _modelLoaded = true;
        return _model;
    }

    private void ResetModel()
    {
        _model = null;
        _modelLoaded = false;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Rect CalculateTargetRect(Size sourceSize, Size renderSize, Stretch stretch)
    {
        if (sourceSize.Width <= 0d || sourceSize.Height <= 0d)
            sourceSize = new Size(24d, 24d);

        if (stretch == Stretch.None)
        {
            double x = (renderSize.Width - sourceSize.Width) / 2d;
            double y = (renderSize.Height - sourceSize.Height) / 2d;
            return new Rect(x, y, sourceSize.Width, sourceSize.Height);
        }

        double scaleX = renderSize.Width / sourceSize.Width;
        double scaleY = renderSize.Height / sourceSize.Height;

        double scale = stretch switch
        {
            Stretch.Fill => double.NaN,
            Stretch.UniformToFill => Math.Max(scaleX, scaleY),
            _ => Math.Min(scaleX, scaleY)
        };

        double width = stretch == Stretch.Fill ? renderSize.Width : sourceSize.Width * scale;
        double height = stretch == Stretch.Fill ? renderSize.Height : sourceSize.Height * scale;
        double left = (renderSize.Width - width) / 2d;
        double top = (renderSize.Height - height) / 2d;

        return new Rect(left, top, width, height);
    }
}
