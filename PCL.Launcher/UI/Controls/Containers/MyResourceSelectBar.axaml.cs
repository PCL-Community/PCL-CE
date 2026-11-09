using Avalonia.Media;
using static PCL.Animations;

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
        AniStart(
            new List<AniData>
            {
                AaOpacity(this, 1d - Opacity, 60),
                AaTranslateY(
                    _transSelect,
                    -27d - _transSelect.Y,
                    120,
                    ease: new AniEaseOutFluent(AniEasePower.Weak)),
                AaTranslateY(
                    _transSelect,
                    3d,
                    150,
                    120,
                    new AniEaseInoutFluent(AniEasePower.Weak)),
                AaTranslateY(
                    _transSelect,
                    -1d,
                    90,
                    270,
                    new AniEaseInoutFluent(AniEasePower.Weak))
            },
            "Resource Sidebar");
    }

    private void HideAnimated()
    {
        AniStart(
            new List<AniData>
            {
                AaOpacity(this, -Opacity, 90),
                AaTranslateY(
                    _transSelect,
                    -10d - _transSelect.Y,
                    90,
                    ease: new AniEaseInFluent(AniEasePower.Weak)),
                AaCode(() => IsVisible = false, after: true)
            },
            "Resource Sidebar");
    }

    private void ShowInstant()
    {
        AniStop("Resource Sidebar");
        IsVisible = true;
        Opacity = 1d;
        _transSelect.Y = -25d;
    }

    private void HideInstant()
    {
        AniStop("Resource Sidebar");
        IsVisible = false;
        Opacity = 0d;
        _transSelect.Y = -10d;
    }
}