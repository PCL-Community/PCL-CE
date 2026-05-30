using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public class MyPageLeft : Grid
{
    public static DependencyProperty AnimatedControlProperty =
        DependencyProperty.Register("AnimatedControl", typeof(FrameworkElement), typeof(MyPageLeft));

    private readonly int uuid = ModBase.GetUuid();

    private bool _animatedControlNullWarned;

    // 执行逐个进入动画的控件
    public FrameworkElement AnimatedControl
    {
        get
        {
            var res = GetValue(AnimatedControlProperty);
            if (res is null && !_animatedControlNullWarned)
            {
                _animatedControlNullWarned = true;
                ModBase.Log($"[MyPageLeft] 获取到 AnimatedControl(来自 {Name}) 的值为 null", ModBase.LogLevel.Debug);
            }

            return (FrameworkElement)res;
        }
        set => SetValue(AnimatedControlProperty, value);
    }

    public void TriggerShowAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(0.96d, 0.96d);
                RenderTransformOrigin = new Point(0.5d, 0.5d);
            }

            Opacity = 0d;
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX,
                        Ease: new ModAnimation.AniEaseOutBack((ModAnimation.AniEasePower)2)),
                    ModAnimation.AaOpacity(this, 1d, 100)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个进入动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var delay = 0;
            foreach (var ElementRaw in GetAllAnimControls(true))
            {
                var element = MyVirtualizingElement.TryInit(ElementRaw);
                if (element.Visibility == Visibility.Collapsed)
                {
                    // 还原之前的隐藏动画可能导致的改变（#2436）
                    element.Opacity = 1d;
                    element.RenderTransform = new TranslateTransform(0d, 0d);
                    if (element is MyListItem)
                        ((MyListItem)element).isMouseOverAnimationEnabled = true;
                }
                else
                {
                    element.Opacity = 0d;
                    element.RenderTransform = new TranslateTransform(-25, 0d);
                    if (element is MyListItem)
                        ((MyListItem)element).isMouseOverAnimationEnabled = false;
                    aniList.Add(ModAnimation.AaOpacity(element, element is TextBlock ? 0.6d : 1d, 100, delay,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                    aniList.Add(ModAnimation.AaTranslateX(element, 5d, 200, delay,
                        new ModAnimation.AniEaseOutFluent()));
                    aniList.Add(ModAnimation.AaTranslateX(element, 20d, 300, delay,
                        new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)));
                    if (element is MyListItem)
                        aniList.Add(ModAnimation.AaCode(() =>
                        {
                            ((MyListItem)element).isMouseOverAnimationEnabled = true;
                            ((MyListItem)element).RefreshColor(this, new EventArgs());
                        }, delay + 280));
                    delay += Math.Max(15 - id, 7) * 2;
                    id += 1;
                }
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    public void TriggerHideAnimation()
    {
        if (AnimatedControl is null)
        {
            // 缩放动画
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransform = new ScaleTransform(1d, 1d);
                RenderTransformOrigin = new Point(0.5d, 0.5d);
            }

            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaScaleTransform(this, 0.95d - ((ScaleTransform)RenderTransform).ScaleX, 110,
                        Ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaOpacity(this, -Opacity, 80, 30)
                }, "PageLeft PageChange " + uuid);
        }
        else
        {
            // 逐个退出动画
            var aniList = new List<ModAnimation.AniData>();
            var id = 0;
            var controls = GetAllAnimControls();
            foreach (var Element in controls)
            {
                aniList.Add(ModAnimation.AaOpacity(Element, -Element.Opacity, 50,
                    (int)Math.Round(70d / controls.Count * id)));
                aniList.Add(ModAnimation.AaTranslateX(Element, -6, 50, (int)Math.Round(70d / controls.Count * id)));
                id += 1;
            }

            ModAnimation.AniStart(aniList, "PageLeft PageChange " + uuid);
        }
    }

    // 遍历获取所有需要生成动画的控件
    private List<FrameworkElement> GetAllAnimControls(bool IgnoreInvisibility = false)
    {
        var allControls = new List<FrameworkElement>();
        GetAllAnimControls(AnimatedControl, ref allControls, IgnoreInvisibility);
        return allControls;
    }

    private void GetAllAnimControls(FrameworkElement Element, ref List<FrameworkElement> AllControls,
        bool IgnoreInvisibility)
    {
        if (!IgnoreInvisibility && Element.Visibility == Visibility.Collapsed)
            return;
        if (Element is MyTextButton)
            AllControls.Add(Element);
        else if (Element is MyListItem)
            AllControls.Add(Element);
        else if (Element is ContentControl)
            GetAllAnimControls((FrameworkElement)((ContentControl)Element).Content, ref AllControls,
                IgnoreInvisibility);
        else if (Element is Panel)
            foreach (FrameworkElement Element2 in ((Panel)Element).Children)
                GetAllAnimControls(Element2, ref AllControls, IgnoreInvisibility);
        else
            AllControls.Add(Element);
    }
}

public interface IRefreshable
{
    void Refresh();
}