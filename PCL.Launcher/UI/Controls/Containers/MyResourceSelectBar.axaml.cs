using Avalonia.Media;

namespace PCL.UI.Controls;

/// <summary>
///     资源页与数据包页共用的底部多选操作条，对齐 WPF <c>CardSelect</c>：
///     首次出现与选中数量增加时上移回弹，数量减少只更新文字，归零时淡出收起。
/// </summary>
public partial class MyResourceSelectBar : MyCard
{
    private readonly TranslateTransform _transSelect = new(0d, -10d);
    private int _shownCount;

    public MyResourceSelectBar()
    {
        InitializeComponent();
        RenderTransform = _transSelect;
    }

    public MyIconTextButton UpdateButton => BtnSelectUpdate;

    public MyIconTextButton EnableButton => BtnSelectEnable;

    public MyIconTextButton DisableButton => BtnSelectDisable;

    public MyIconTextButton FavoritesButton => BtnSelectFavorites;

    public MyIconTextButton ShareButton => BtnSelectShare;

    public MyIconTextButton DeleteButton => BtnSelectDelete;

    public MyIconTextButton CancelButton => BtnSelectCancel;

    /// <summary>当前是否正在执行滑动选择；滑动期间只同步文字，不触发回弹动画。</summary>
    public bool IsSwipeActive { get; set; }

    public void UpdateSelection(
        bool visible,
        int count,
        string text,
        bool animate = true)
    {
        LabSelect.Text = text;
        if (!visible)
        {
            _shownCount = 0;
            if (!IsVisible)
            {
                HideInstant();
                return;
            }

            if (!animate || !ContainerAnimation.CanAnimate(this))
                HideInstant();
            else
                HideAnimated();
            return;
        }

        var shouldAnimate = animate
            && !IsSwipeActive
            && ContainerAnimation.CanAnimate(this);
        if (!IsVisible)
        {
            if (shouldAnimate)
                ShowAnimated();
            else
                ShowInstant();
            _shownCount = count;
            return;
        }

        if (count > _shownCount && shouldAnimate)
            ShowAnimated();
        else if (!shouldAnimate)
            ShowInstant();

        _shownCount = count;
    }

    private void ShowAnimated()
    {
        IsVisible = true;
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(this, 1d - Opacity, 60),
                new TranslateYAnimation(
                    _transSelect,
                    -27d - _transSelect.Y,
                    120,
                    ease: new FluentEaseOut(EasePower.Weak)),
                new TranslateYAnimation(
                    _transSelect,
                    3d,
                    150,
                    120,
                    new FluentEaseInOut(EasePower.Weak)),
                new TranslateYAnimation(
                    _transSelect,
                    -1d,
                    90,
                    270,
                    new FluentEaseInOut(EasePower.Weak))
            },
            "Resource Sidebar");
    }

    private void HideAnimated()
    {
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(this, -Opacity, 90),
                new TranslateYAnimation(
                    _transSelect,
                    -10d - _transSelect.Y,
                    90,
                    ease: new FluentEaseIn(EasePower.Weak)),
                new ActionAnimation(() => IsVisible = false, true)
            },
            "Resource Sidebar");
    }

    private void ShowInstant()
    {
        AnimationService.CancelAnimationByName("Resource Sidebar");
        IsVisible = true;
        Opacity = 1d;
        _transSelect.Y = -25d;
    }

    private void HideInstant()
    {
        AnimationService.CancelAnimationByName("Resource Sidebar");
        IsVisible = false;
        Opacity = 0d;
        _transSelect.Y = -10d;
    }
}