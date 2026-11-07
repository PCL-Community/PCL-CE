using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.Views.Pages;
using static PCL.Animations;

namespace PCL.UI.Controls;

/// <summary>
///     页面左栏容器：为 <see cref="AnimatedControl" /> 中的按钮与列表项生成逐个进入/退出动画；
///     未设置 <see cref="AnimatedControl" /> 时退化为整栏缩放淡入。
///     语言切换后 <see cref="RefreshLocalization" /> 会在下次加载时重刷页面文本。
/// </summary>
public class MyPageLeft : Grid, IPageView
{
    protected INavigationService Navigation => PageRouter.Navigation;

    public static readonly StyledProperty<Control?> AnimatedControlProperty =
        AvaloniaProperty.Register<MyPageLeft, Control?>(nameof(AnimatedControl));

    private readonly int _uuid = LauncherCommon.GetUuid();

    private bool _animatedControlNullWarned;
    private int _localizationRevision = LocalizationService.UiRevision;

    public MyPageLeft()
    {
        ContainerTheme.AttachStyles(this);
        Loaded += (_, _) => RefreshLocalizationIfStale();
    }

    // 执行逐个进入动画的控件
    public Control? AnimatedControl
    {
        get
        {
            var res = GetValue(AnimatedControlProperty);
            if (res is null && !_animatedControlNullWarned)
            {
                _animatedControlNullWarned = true;
                LauncherCommon.Log(
                    $"[MyPageLeft] 获取到 AnimatedControl(来自 {Name}) 的值为 null",
                    LauncherLogLevel.Debug);
            }

            return res;
        }
        set => SetValue(AnimatedControlProperty, value);
    }

    public virtual void OnNavigatedTo(object? parameter)
    {
    }

    public virtual void OnNavigatedFrom()
    {
    }

    private void RefreshLocalizationIfStale()
    {
        if (_localizationRevision == LocalizationService.UiRevision)
            return;
        _localizationRevision = LocalizationService.UiRevision;
        try
        {
            RefreshLocalization();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新页面本地化文本失败");
        }
    }

    protected virtual void RefreshLocalization()
    {
    }

    public void ApplyLocalizationChanged()
    {
        RefreshLocalizationIfStale();
    }

    public void TriggerShowAnimation()
    {
        var animationKey = "PageLeft PageChange " + _uuid;

        if (AnimatedControl is null)
        {
            // 缩放动画：0.96 放大到 1，同时淡入
            var scale = ContainerAnimation.EnsureScale(this, 0.96d, 0.96d);
            if (!ContainerAnimation.CanAnimatePage(this))
            {
                AniStop(animationKey);
                scale.ScaleX = 1d;
                scale.ScaleY = 1d;
                Opacity = 1d;
                return;
            }

            Opacity = 0d;
            AniStart(
                new List<AniData>
                {
                    AaScaleTransform(
                        this,
                        1d - scale.ScaleX,
                        ease: new AniEaseOutBack(AniEasePower.Weak)),
                    AaOpacity(this, 1d, 100)
                },
                animationKey);
            return;
        }

        // 动画关闭或控件未加载：直接落到终值
        if (!ContainerAnimation.CanAnimatePage(this))
        {
            AniStop(animationKey);
            foreach (var element in GetAllAnimControls(true))
            {
                element.Opacity = element is TextBlock ? 0.6d : 1d;
                element.RenderTransform = null;
                if (element is MyListItem listItem)
                {
                    listItem.isMouseOverAnimationEnabled = true;
                    listItem.RefreshColor(this, EventArgs.Empty);
                }
            }

            return;
        }

        // 逐个进入动画：左侧滑入 + 淡入，列表项按次序错开
        var aniList = new List<AniData>();
        var id = 0;
        var delay = 0;
        foreach (var element in GetAllAnimControls(true))
            if (!element.IsVisible)
            {
                // 还原之前的隐藏动画可能导致的改变（#2436）
                element.Opacity = 1d;
                element.RenderTransform = new TranslateTransform(0d, 0d);
                if (element is MyListItem hiddenItem)
                    hiddenItem.isMouseOverAnimationEnabled = true;
            }
            else
            {
                element.Opacity = 0d;
                element.RenderTransform = new TranslateTransform(-25d, 0d);
                if (element is MyListItem listItem)
                    listItem.isMouseOverAnimationEnabled = false;
                aniList.Add(
                    AaOpacity(
                        element,
                        element is TextBlock ? 0.6d : 1d,
                        100,
                        delay,
                        new AniEaseOutFluent(AniEasePower.Weak)));
                aniList.Add(AaTranslateX(element, 5d, 200, delay, new AniEaseOutFluent()));
                aniList.Add(
                    AaTranslateX(
                        element,
                        20d,
                        300,
                        delay,
                        new AniEaseOutBack(AniEasePower.Weak)));
                if (element is MyListItem item)
                    aniList.Add(
                        AaCode(
                            () =>
                            {
                                item.isMouseOverAnimationEnabled = true;
                                item.RefreshColor(this, EventArgs.Empty);
                            },
                            delay + 280));
                delay += Math.Max(15 - id, 7) * 2;
                id += 1;
            }

        AniStart(aniList, animationKey);
    }

    public void TriggerHideAnimation()
    {
        var animationKey = "PageLeft PageChange " + _uuid;

        if (AnimatedControl is null)
        {
            // 缩放动画：1 缩到 0.95，同时淡出
            var scale = ContainerAnimation.EnsureScale(this);
            if (!ContainerAnimation.CanAnimatePage(this))
            {
                AniStop(animationKey);
                scale.ScaleX = 1d;
                scale.ScaleY = 1d;
                Opacity = 0d;
                return;
            }

            AniStart(
                new List<AniData>
                {
                    AaScaleTransform(
                        this,
                        0.95d - scale.ScaleX,
                        110,
                        ease: new AniEaseInFluent(AniEasePower.Weak)),
                    AaOpacity(this, -Opacity, 80, 30)
                },
                animationKey);
            return;
        }

        var controls = GetAllAnimControls();

        if (!ContainerAnimation.CanAnimatePage(this) || controls.Count == 0)
        {
            AniStop(animationKey);
            foreach (var element in controls)
            {
                element.Opacity = 0d;
                element.RenderTransform = new TranslateTransform(-6d, 0d);
            }

            RestoreListItemHover(controls);
            return;
        }

        // 逐个退出动画：右侧滑出 + 淡出
        var aniList = new List<AniData>();
        var id = 0;
        foreach (var element in controls)
        {
            var delay = (int)Math.Round(70d / controls.Count * id);
            aniList.Add(AaOpacity(element, -element.Opacity, 50, delay));
            aniList.Add(AaTranslateX(element, -6d, 50, delay));
            id += 1;
        }

        // 动画被下一次切换打断时，列表项的悬停开关不能留在关闭状态
        aniList.Add(AaCode(() => RestoreListItemHover(controls), after: true));
        AniStart(aniList, animationKey);
    }

    private static void RestoreListItemHover(IEnumerable<Control> controls)
    {
        foreach (var control in controls)
            if (control is MyListItem listItem)
                listItem.isMouseOverAnimationEnabled = true;
    }

    // 遍历获取所有需要生成动画的控件
    private List<Control> GetAllAnimControls(bool ignoreInvisibility = false)
    {
        var allControls = new List<Control>();
        if (AnimatedControl is not null)
            GetAllAnimControls(AnimatedControl, allControls, ignoreInvisibility);
        return allControls;
    }

    private static void GetAllAnimControls(
        Control element,
        List<Control> allControls,
        bool ignoreInvisibility)
    {
        while (true)
        {
            if (!ignoreInvisibility && !element.IsVisible) return;
            switch (element)
            {
                case MyTextButton or MyListItem:
                {
                    allControls.Add(element);
                    break;
                }

                case ContentControl contentControl:
                {
                    if (contentControl.Content is Control content)
                    {
                        element = content;
                        continue;
                    }

                    break;
                }

                case Decorator decorator:
                {
                    if (decorator.Child is not null)
                    {
                        element = decorator.Child;
                        continue;
                    }

                    break;
                }

                case Panel panel:
                {
                    foreach (var child in panel.Children.OfType<Control>())
                        GetAllAnimControls(child, allControls, ignoreInvisibility);
                    break;
                }

                default:
                    allControls.Add(element);
                    break;
            }

            break;
        }
    }
}

/// <summary>页面内可刷新的控件约定。</summary>
public interface IRefreshable
{
    void Refresh();
}