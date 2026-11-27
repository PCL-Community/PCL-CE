using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Views;
using PlainToolkit.ObjectiveC.Runtime;

namespace PCL;

/// <summary>在融入标题栏模式下调整 macOS 原生红绿灯的位置，不改变按钮尺寸、间距和行为。</summary>
internal sealed class MacOsCaptionButtonLayout : IDisposable
{
    private static readonly ObjCSel StandardWindowButton = ObjCSel.FromString("standardWindowButton:");
    private static readonly ObjCSel ContentView = ObjCSel.FromString("contentView");
    private static readonly ObjCSel Superview = ObjCSel.FromString("superview");
    private static readonly ObjCSel Frame = ObjCSel.FromString("frame");
    private static readonly ObjCSel Bounds = ObjCSel.FromString("bounds");
    private static readonly ObjCSel IsFlipped = ObjCSel.FromString("isFlipped");
    private static readonly ObjCSel ConvertPointToView = ObjCSel.FromString("convertPoint:toView:");
    private static readonly ObjCSel SetFrameOrigin = ObjCSel.FromString("setFrameOrigin:");
    private static readonly ObjCSel LayoutSubtreeIfNeeded = ObjCSel.FromString("layoutSubtreeIfNeeded");

    private const double ContentSpacing = 12d;

    private readonly MainWindow _window;
    private readonly double _left;
    private readonly double _verticalOffset;
    private readonly double _minimumLeadingInset;
    private readonly Dictionary<nuint, OriginalPosition> _originalPositions = [];
    private bool _unified;
    private bool _updateQueued;

    public MacOsCaptionButtonLayout(
        MainWindow window,
        double left,
        double verticalOffset,
        double minimumLeadingInset)
    {
        _window = window;
        _left = left;
        _verticalOffset = verticalOffset;
        _minimumLeadingInset = minimumLeadingInset;
        window.Opened += OnOpened;
        window.SizeChanged += OnSizeChanged;
        window.ScalingChanged += OnScalingChanged;
        window.PropertyChanged += OnPropertyChanged;
        window.Closed += OnClosed;
    }

    public bool IsDisposed { get; private set; }

    /// <summary>在切换原生工具栏之前恢复原始位置，避免把自定义位置当成下一次布局的基准。</summary>
    public void PrepareForStyleChange(bool unified)
    {
        if (IsDisposed) return;
        var nsWindow = GetNativeWindow();
        if (!nsWindow.IsNull)
        {
            var buttons = GetButtons(nsWindow);
            foreach (var button in buttons)
            {
                if (_originalPositions.TryGetValue(button.Kind, out var original)
                    && original.Handle == button.View.Value
                    && original.Parent == button.Parent.Value)
                    button.View.SendMessage(SetFrameOrigin, original.Origin);
            }

            _originalPositions.Clear();
            if (unified) RememberOriginalPositions(GetButtons(nsWindow));
        }
        else
        {
            _originalPositions.Clear();
        }

        _unified = unified;
    }

    public void ScheduleUpdate()
    {
        if (IsDisposed || !_unified || _updateQueued) return;
        _updateQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _updateQueued = false;
                if (IsDisposed || !_unified || !_window.ExtendClientAreaToDecorationsHint) return;
                Apply();
            },
            DispatcherPriority.Loaded);
    }

    private void Apply()
    {
        var titleBarHeight = _window.WindowDecorationMargin.Top;
        if (titleBarHeight <= 0d) return;
        var nsWindow = GetNativeWindow();
        if (nsWindow.IsNull) return;
        var contentView = nsWindow.SendMessage<ObjCObject>(ContentView);
        if (contentView.IsNull) return;
        var root = contentView.SendMessage<ObjCObject>(Superview);
        if (root.IsNull) return;
        // 先完成 AppKit 工具栏的延迟布局，否则后续原生重排会覆盖自定义位置。
        root.SendMessage(LayoutSubtreeIfNeeded);

        var buttons = GetButtons(nsWindow);
        if (buttons.Count == 0 || buttons[0].Kind != 0) return;
        var rootBounds = ReadRect(root, Bounds);
        if (rootBounds.Size.Width <= 0d || rootBounds.Size.Height <= 0d) return;
        RememberOriginalPositions(buttons);

        var close = buttons[0];
        var currentCenter = ToRoot(close, root);
        var targetX = rootBounds.Origin.X + _left + close.Frame.Size.Width / 2d;
        var fromTop = titleBarHeight / 2d + _verticalOffset;
        var targetY = root.SendMessage<byte>(IsFlipped) != 0
            ? rootBounds.Origin.Y + fromTop
            : rootBounds.Origin.Y + rootBounds.Size.Height - fromTop;
        var dx = targetX - currentCenter.X;
        var dy = targetY - currentCenter.Y;
        var right = 0d;

        foreach (var button in buttons)
        {
            var center = ToRoot(button, root);
            var target = new NativePoint { X = center.X + dx, Y = center.Y + dy };
            var local = root.SendMessage<NativePoint, ObjCObject, NativePoint>(
                ConvertPointToView,
                target,
                button.Parent);
            button.View.SendMessage(SetFrameOrigin, new NativePoint
            {
                X = local.X - button.Frame.Size.Width / 2d,
                Y = local.Y - button.Frame.Size.Height / 2d
            });
            right = Math.Max(right, target.X + button.Frame.Size.Width / 2d - rootBounds.Origin.X);
        }

        _window.ApplyExtendedTitleBarLayout(Math.Max(_minimumLeadingInset, right + ContentSpacing));
    }

    private ObjCObject GetNativeWindow()
    {
        return _window.TryGetPlatformHandle()?.Handle is { } handle && handle != 0
            ? new ObjCObject(handle)
            : default;
    }

    private static List<CaptionButton> GetButtons(ObjCObject nsWindow)
    {
        List<CaptionButton> buttons = [];
        // NSWindowButton：关闭、最小化、缩放。原生按钮及其父视图均为借用对象。
        for (nuint kind = 0; kind < 3; kind++)
        {
            var button = nsWindow.SendMessage<nuint, ObjCObject>(StandardWindowButton, kind);
            if (button.IsNull) continue;
            var parent = button.SendMessage<ObjCObject>(Superview);
            if (parent.IsNull) continue;
            var frame = ReadRect(button, Frame);
            if (frame.Size.Width <= 0d || frame.Size.Height <= 0d) continue;
            buttons.Add(new CaptionButton(kind, button, parent, frame));
        }

        return buttons;
    }

    private void RememberOriginalPositions(List<CaptionButton> buttons)
    {
        foreach (var button in buttons)
            _originalPositions.TryAdd(
                button.Kind,
                new OriginalPosition(button.View.Value, button.Parent.Value, button.Frame.Origin));
    }

    private static NativePoint ToRoot(CaptionButton button, ObjCObject root)
    {
        return button.Parent.SendMessage<NativePoint, ObjCObject, NativePoint>(
            ConvertPointToView,
            new NativePoint
            {
                X = button.Frame.Origin.X + button.Frame.Size.Width / 2d,
                Y = button.Frame.Origin.Y + button.Frame.Size.Height / 2d
            },
            root);
    }

    private void OnOpened(object? sender, EventArgs e) => ScheduleUpdate();
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ScheduleUpdate();
    private void OnScalingChanged(object? sender, EventArgs e) => ScheduleUpdate();

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowDecorationMarginProperty || e.Property == Window.WindowStateProperty)
            ScheduleUpdate();
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _originalPositions.Clear();
        _window.Opened -= OnOpened;
        _window.SizeChanged -= OnSizeChanged;
        _window.ScalingChanged -= OnScalingChanged;
        _window.PropertyChanged -= OnPropertyChanged;
        _window.Closed -= OnClosed;
    }

    private static NativeRect ReadRect(ObjCObject view, ObjCSel selector)
    {
        // x64 的 NSRect 是 32 字节结构体，需要 objc_msgSend_stret；ARM64 使用普通消息调用。
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return view.SendMessage<NativeRect>(selector);
        ReadRectX64(out var rect, view.Value, selector.Value);
        return rect;
    }

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend_stret", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ReadRectX64(out NativeRect rect, nint receiver, nint selector);

    private readonly record struct CaptionButton(nuint Kind, ObjCObject View, ObjCObject Parent, NativeRect Frame);
    private readonly record struct OriginalPosition(nint Handle, nint Parent, NativePoint Origin);

    // AppKit 的 CGFloat 在 macOS ARM64/x64 上均为 double。类型供消息拦截器生成的代码使用。
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint
    {
        public double X;
        public double Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public NativePoint Origin;
        public NativeSize Size;
    }
}
