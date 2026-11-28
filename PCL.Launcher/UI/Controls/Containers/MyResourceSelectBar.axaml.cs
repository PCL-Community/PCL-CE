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
    private string AnimationKey => $"Resource Sidebar {uuid}";

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

    /// <summary>当前是否正在执行滑动选择；滑动期间不重复触发数量增加的回弹动画。</summary>
    public bool IsSwipeActive { get; set; }

    public void UpdateSelection(
        bool visible,
        int count,
        string text,
        bool animate = true)
    {
        LabSelect.Text = text;
        var canAnimate = animate && ContainerAnimation.CanAnimate(this);
        if (!visible)
        {
            if (_shownCount == 0 && canAnimate && AnimationService.IsAnimationRunning(AnimationKey))
                return;
            _shownCount = 0;
            if (!IsVisible)
            {
                HideInstant();
                return;
            }

            if (!canAnimate)
                HideInstant();
            else
                HideAnimated();
            return;
        }

        if (!IsVisible || _shownCount == 0)
        {
            if (canAnimate)
                ShowAnimated();
            else
                ShowInstant();
            _shownCount = count;
            return;
        }

        if (count > _shownCount && canAnimate && !IsSwipeActive)
            ShowAnimated();
        else if (!canAnimate)
            ShowInstant();

        _shownCount = count;
    }

    private void ShowAnimated()
    {
        RenderTransform = _transSelect;
        if (!IsVisible)
        {
            Opacity = 0d;
            _transSelect.Y = -10d;
        }
        IsVisible = true;
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(this, 1d - Opacity, 60),
                new TranslateYAnimation(
                    this,
                    -27d - _transSelect.Y,
                    120,
                    ease: new FluentEaseOut(EasePower.Weak)),
                new TranslateYAnimation(
                    this,
                    3d,
                    150,
                    120,
                    new FluentEaseInOut(EasePower.Weak)),
                new TranslateYAnimation(
                    this,
                    -1d,
                    90,
                    270,
                    new FluentEaseInOut(EasePower.Weak))
            },
            AnimationKey);
    }

    private void HideAnimated()
    {
        RenderTransform = _transSelect;
        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(this, -Opacity, 90),
                new TranslateYAnimation(
                    this,
                    -10d - _transSelect.Y,
                    90,
                    ease: new FluentEaseIn(EasePower.Weak)),
                new ActionAnimation(() => IsVisible = false, true)
            },
            AnimationKey);
    }

    private void ShowInstant()
    {
        AnimationService.CancelAnimationByName(AnimationKey);
        RenderTransform = _transSelect;
        IsVisible = true;
        Opacity = 1d;
        _transSelect.Y = -25d;
    }

    private void HideInstant()
    {
        AnimationService.CancelAnimationByName(AnimationKey);
        IsVisible = false;
        Opacity = 0d;
        _transSelect.Y = -10d;
    }
}