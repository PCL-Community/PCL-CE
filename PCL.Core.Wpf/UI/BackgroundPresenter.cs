using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

// 该部分源码来自或修改于 https://github.com/OrgEleCho/EleCho.WpfSuite
// 项目: EleCho.WpfSuite
// 作者: EleCho
// 协议: MIT License

namespace PCL.Core.UI;

/// <summary>
///     Used for rendering the background content of a UIElement. By adding a <see cref="BlurEffect" />
///     to this element, you can achieve a blurred background effect.
/// </summary>
public class BackgroundPresenter : FrameworkElement
{
    private const double EffectPadding = 32;

    private static readonly FieldInfo DrawingContentOfUiElement = typeof(UIElement)
        .GetField("_drawingContent", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo ContentOfDrawingVisual = typeof(DrawingVisual)
        .GetField("_content", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly FieldInfo OffsetOfVisual = typeof(Visual)
        .GetField("_offset", BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly Func<UIElement, DrawingContext> RenderOpenMethod =
        (Func<UIElement, DrawingContext>)typeof(UIElement)
            .GetMethod("RenderOpen", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Func<UIElement, DrawingContext>));

    private static readonly Action<UIElement, DrawingContext> OnRenderMethod =
        (Action<UIElement, DrawingContext>)typeof(UIElement)
            .GetMethod("OnRender", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(Action<UIElement, DrawingContext>));

    private static readonly GetContentBoundsDelegate MethodGetContentBounds =
        (GetContentBoundsDelegate)typeof(VisualBrush)
            .GetMethod("GetContentBounds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate(typeof(GetContentBoundsDelegate));

    /// <summary>
    ///     Dependency property for <see cref="MaxDepth" />.
    /// </summary>
    public static readonly DependencyProperty MaxDepthProperty =
        DependencyProperty.Register(
            nameof(MaxDepth),
            typeof(int),
            typeof(BackgroundPresenter),
            new FrameworkPropertyMetadata(16));

    private readonly Stack<UIElement> _parentStack = new();

    /// <summary>
    ///     Gets or sets the maximum depth of the visual tree to render.
    /// </summary>
    public int MaxDepth
    {
        get => (int)GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    public static void ForceRender(UIElement target)
    {
        using var drawingContext = RenderOpenMethod(target);

        OnRenderMethod.Invoke(target, drawingContext);
    }

    internal static void DrawVisual(DrawingContext drawingContext, Visual visual, Point relatedXy)
    {
        var visualBrush = new VisualBrush(visual);
        var visualOffset = (Vector)OffsetOfVisual.GetValue(visual)!;

        MethodGetContentBounds.Invoke(visualBrush, out var contentBounds);
        relatedXy -= visualOffset;
        if (contentBounds.IsEmpty) return;

        var bounds = contentBounds;
        bounds.Inflate(EffectPadding, EffectPadding);
        visualBrush.ViewboxUnits = BrushMappingMode.Absolute;
        visualBrush.Viewbox = bounds;

        drawingContext.DrawRectangle(
            visualBrush,
            null,
            new Rect(relatedXy.X + bounds.X, relatedXy.Y + bounds.Y, bounds.Width, bounds.Height));
    }

    /// <inheritdoc />
    protected override Geometry GetLayoutClip(Size layoutSlotSize)
    {
        return new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight));
    }

    /// <inheritdoc />
    protected override void OnVisualParentChanged(DependencyObject oldParentObject)
    {
        if (oldParentObject is UIElement oldParent) oldParent.LayoutUpdated -= ParentLayoutUpdated;

        if (Parent is UIElement newParent) newParent.LayoutUpdated += ParentLayoutUpdated;
    }

    private void ParentLayoutUpdated(object? sender, EventArgs e)
    {
        // cannot use 'InvalidateVisual' here, because it will cause infinite loop

        ForceRender(this);

        // Debug.WriteLine("Parent layout updated, forcing render of BackgroundPresenter.");
    }

    internal static void DrawBackground(
        DrawingContext drawingContext,
        UIElement self,
        Stack<UIElement> parentStackStorage,
        int maxDepth,
        bool throwExceptionIfParentArranging)
    {
        var selfInDesignMode = DesignerProperties.GetIsInDesignMode(self);

        var parent = VisualTreeHelper.GetParent(self) as UIElement;
        while (
            parent is not null && parentStackStorage.Count < maxDepth)
        {
            // parent not visible, no need to render
            if (!parent.IsVisible)
            {
                parentStackStorage.Clear();
                return;
            }

            if (selfInDesignMode && parent.GetType().ToString().Contains("VisualStudio"))
                // 遍历到 VS 自身的设计器元素, 中断!
                break;

            // is parent arranging
            // we cannot render it
            if (parent.RenderSize.Width == 0 || parent.RenderSize.Height == 0)
            {
                parentStackStorage.Clear();

                if (throwExceptionIfParentArranging)
                    throw new InvalidOperationException("Arranging");

                // render after parent arranging finished
                self.InvalidateArrange();
                return;
            }

            parentStackStorage.Push(parent);
            parent = VisualTreeHelper.GetParent(parent) as UIElement;
        }

        var selfRect = new Rect(0, 0, self.RenderSize.Width, self.RenderSize.Height);
        while (parentStackStorage.Count > 0)
        {
            var currentParent = parentStackStorage.Pop();
            var breakElement = self;

            if (parentStackStorage.Count > 0) breakElement = parentStackStorage.Peek();

            var parentRelatedXy = currentParent.TranslatePoint(default, self);

            // has render data
            if (DrawingContentOfUiElement.GetValue(currentParent) is { } parentDrawingContent)
            {
                var drawingVisual = new DrawingVisual();
                ContentOfDrawingVisual.SetValue(drawingVisual, parentDrawingContent);

                DrawVisual(drawingContext, drawingVisual, parentRelatedXy);
            }

            var childCount = VisualTreeHelper.GetChildrenCount(currentParent);
            for (var i = 0; i < childCount; i++)
            {
                if (VisualTreeHelper.GetChild(currentParent, i) is not UIElement child) continue;

                if (child == breakElement) break;

                var childRelatedXy = child.TranslatePoint(default, self);
                var childRect = new Rect(childRelatedXy, child.RenderSize);

                if (!selfRect.IntersectsWith(childRect)) continue; // skip if not intersecting

                if (child.IsVisible) DrawVisual(drawingContext, child, childRelatedXy);
            }
        }
    }

    /// <summary>
    ///     Draw background of the specified UIElement.
    /// </summary>
    /// <param name="drawingContext"></param>
    /// <param name="self"></param>
    public static void DrawBackground(DrawingContext drawingContext, UIElement self)
    {
        var parentStack = new Stack<UIElement>();
        DrawBackground(drawingContext, self, parentStack, int.MaxValue, true);
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        DrawBackground(drawingContext, this, _parentStack, MaxDepth, false);
    }

    private delegate void GetContentBoundsDelegate(VisualBrush visualBrush, out Rect bounds);
}