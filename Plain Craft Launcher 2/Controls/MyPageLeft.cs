using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PCL;

public class MyPageLeft : Grid
{
    public static DependencyProperty TransitionControlProperty =
        DependencyProperty.Register("TransitionControl", typeof(FrameworkElement), typeof(MyPageLeft));

    private readonly int uuid = ModBase.GetUuid();

    private bool _animatedControlNullWarned;

    // 参与页面切换的控件
    public FrameworkElement TransitionControl
    {
        get
        {
            var res = GetValue(TransitionControlProperty);
            if (res is null && !_animatedControlNullWarned)
            {
                _animatedControlNullWarned = true;
                ModBase.Log($"[MyPageLeft] 获取到 TransitionControl(来自 {Name}) 的值为 null", ModBase.LogLevel.Debug);
            }

            return (FrameworkElement)res;
        }
        set => SetValue(TransitionControlProperty, value);
    }

    public void ShowPage()
    {
        Opacity = 1d;
        if (RenderTransform is ScaleTransform scale)
        {
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
        }

        if (TransitionControl is null)
            return;

        foreach (var ElementRaw in GetAllTransitionControls(true))
        {
            var element = MyVirtualizingElement.TryInit(ElementRaw);
            element.Opacity = element is TextBlock ? 0.6d : 1d;
            element.RenderTransform = new TranslateTransform(0d, 0d);
            if (element is MyListItem listItem)
            {
                listItem.IsMouseOverStyleEnabled = true;
                listItem.RefreshColor(this, new EventArgs());
            }
        }
    }

    public void HidePage()
    {
        // 隐藏时无需额外处理
    }

    // 遍历获取所有参与页面切换的控件
    private List<FrameworkElement> GetAllTransitionControls(bool ignoreInvisibility = false)
    {
        var allControls = new List<FrameworkElement>();
        GetAllTransitionControls(TransitionControl, ref allControls, ignoreInvisibility);
        return allControls;
    }

    private void GetAllTransitionControls(FrameworkElement element, ref List<FrameworkElement> allControls,
        bool ignoreInvisibility)
    {
        if (!ignoreInvisibility && element.Visibility == Visibility.Collapsed)
            return;
        if (element is MyTextButton)
            allControls.Add(element);
        else if (element is MyListItem)
            allControls.Add(element);
        else if (element is ContentControl)
            GetAllTransitionControls((FrameworkElement)((ContentControl)element).Content, ref allControls,
                ignoreInvisibility);
        else if (element is Panel)
            foreach (FrameworkElement Element2 in ((Panel)element).Children)
                GetAllTransitionControls(Element2, ref allControls, ignoreInvisibility);
        else
            allControls.Add(element);
    }
}

public interface IRefreshable
{
    void Refresh();
}