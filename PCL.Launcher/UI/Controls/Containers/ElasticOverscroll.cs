using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace PCL.UI.Controls;

/// <summary>
///     滚动到边界后的橡皮筋拉伸与弹簧回弹。只负责越界量的计算与显示，内容偏移仍由 <see cref="MyScrollViewer" /> 管理。
///     越界量为正表示底部越界，为负表示顶部越界。
/// </summary>
internal sealed class ElasticOverscroll(Control owner)
{
    private const double RubberBandCoefficient = 0.55d; // 与 AppKit 相同的阻尼系数
    private const double SpringFrequency = 12d; // 临界阻尼弹簧的角频率（1/s），约 0.4 s 回位

    private ScrollContentPresenter? _presenter;
    private MyScrollBar? _scrollBar;
    private double _viewport = 1d;
    private double _raw; // 未经阻尼的越界输入
    private double _shown;
    private bool _ownerClipped;
    private bool _springRunning;
    private int _springGeneration;
    private double _springStart;
    private double _springVelocity;
    private TimeSpan? _springStartTime;

    public bool IsStretched => _raw != 0d;

    public bool IsSpringRunning => _springRunning;

    public double Shown => _shown;

    public void Attach(ScrollContentPresenter? presenter, MyScrollBar? scrollBar)
    {
        Reset();
        _presenter = presenter;
        _scrollBar = scrollBar;
    }

    public void SetViewport(double viewport)
    {
        _viewport = Math.Max(1d, viewport);
    }

    public void StartStretch(double excess)
    {
        _raw = excess;
        _Show(_RubberBand(excess));
    }

    /// <summary>继续拉伸；拖回边界内时结束拉伸，返回还需要用来滚动内容的剩余增量。</summary>
    public double ContinueStretch(double delta)
    {
        var raw = _raw + delta;
        if (Math.Sign(raw) == Math.Sign(_raw))
        {
            _raw = raw;
            _Show(_RubberBand(raw));
            return 0d;
        }

        Reset();
        return raw;
    }

    /// <summary>手指按下状态：停住正在进行的回弹，从当前位置继续拉伸。</summary>
    public void Grab()
    {
        if (!_springRunning) return;

        _StopSpring();
        _raw = _InverseRubberBand(_shown);
    }

    /// <summary>手指抬起状态：仍在拉伸时回弹到边界。</summary>
    public void Release()
    {
        if (_raw != 0d && !_springRunning) _StartSpring(_raw, 0d);
    }

    /// <summary>惯性滚动撞到边界：以撞击速度（px/s）弹出再回弹。</summary>
    public void Bounce(double velocity)
    {
        _StartSpring(0d, velocity);
    }

    public void Reset()
    {
        _StopSpring();
        _raw = 0d;
        if (_shown != 0d) _Show(0d);
    }

    private double _RubberBand(double raw)
    {
        var magnitude = (1d - 1d / (Math.Abs(raw) * RubberBandCoefficient / _viewport + 1d)) * _viewport;
        return Math.CopySign(magnitude, raw);
    }

    private double _InverseRubberBand(double shown)
    {
        var ratio = Math.Min(Math.Abs(shown) / _viewport, 0.99d);
        return Math.CopySign(_viewport / RubberBandCoefficient * (1d / (1d - ratio) - 1d), shown);
    }

    private void _Show(double value)
    {
        _shown = value;
        _scrollBar?.SetOverscroll(value, _viewport);
        if (_presenter is null) return;

        if (value == 0d)
        {
            _presenter.RenderTransform = null;
            owner.ClipToBounds = _ownerClipped;
            return;
        }

        if (_presenter.RenderTransform is not TranslateTransform transform)
        {
            // 内容平移后会露出视口外的部分，需要由外层裁剪
            _ownerClipped = owner.ClipToBounds;
            owner.ClipToBounds = true;
            transform = new TranslateTransform();
            _presenter.RenderTransform = transform;
        }

        transform.Y = -value;
    }

    // x(t) = (x0 + (v0 + ωx0)t)·e^(−ωt)，显示时再经过橡皮筋阻尼
    private void _StartSpring(double rawStart, double velocity)
    {
        _springStart = rawStart;
        _springVelocity = velocity;
        _springStartTime = null;
        _raw = 0d;
        if (_springRunning) return;

        _springRunning = true;
        _springGeneration++;
        _RequestSpringFrame(_springGeneration);
    }

    private void _StopSpring()
    {
        _springRunning = false;
        _springGeneration++;
    }

    private void _RequestSpringFrame(int generation)
    {
        if (TopLevel.GetTopLevel(owner) is not { } topLevel)
        {
            Reset();
            return;
        }

        topLevel.RequestAnimationFrame(time => _OnSpringFrame(time, generation));
    }

    private void _OnSpringFrame(TimeSpan time, int generation)
    {
        if (!_springRunning || generation != _springGeneration) return;

        _springStartTime ??= time;
        var t = (time - _springStartTime.Value).TotalSeconds;
        var shown = _RubberBand(
            (_springStart + (_springVelocity + SpringFrequency * _springStart) * t) * Math.Exp(-SpringFrequency * t));

        if (t > 1d / SpringFrequency && Math.Abs(shown) < 0.5d)
        {
            Reset();
            return;
        }

        _Show(shown);
        _RequestSpringFrame(generation);
    }
}
