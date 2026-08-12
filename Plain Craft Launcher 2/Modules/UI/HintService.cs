using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.UI;

namespace PCL;

public static class HintService
{
    /// <summary>相邻弹窗之间的间距（像素）。</summary>
    private const double ToastGap = 4d;

    private struct HintMessage
    {
        public string Text;
        public HintType Type;
        public bool Log;
    }

    private static ModBase.SafeList<HintMessage> HintWaiting
    {
        get => field ??= new ModBase.SafeList<HintMessage>();
        set;
    }

    public static void Hint(string? text, HintType type = HintType.Info, bool log = true)
    {
        var normalized = (text ?? "").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        if (HintWaiting.Any(h => h.Text == normalized && h.Type == type)) return;
        HintWaiting.Add(new HintMessage { Text = normalized, Type = type, Log = log });
    }

    public static void HintWrapper_OnShow(string message, HintTheme messageTheme)
    {
        var hintType = messageTheme switch
        {
            HintTheme.Success => HintType.Success,
            HintTheme.Error => HintType.Error,
            HintTheme.Warning => HintType.Warning,
            _ => HintType.Info
        };
        Hint(message, hintType);
    }

    internal static void Tick()
    {
        try
        {
            ModMain.frmMain!.PanHint.HorizontalAlignment = HorizontalAlignment.Right;
            ModMain.frmMain.PanHint.VerticalAlignment = VerticalAlignment.Stretch;

            var extraHeight = ModMain.frmMain.PanExtraButtons.ActualHeight;
            ModMain.frmMain.PanHint.Margin = new Thickness(0, 0, 0, extraHeight > 0 ? extraHeight + 20 : 20);

            if (!HintWaiting.Any())
                return;

            var currentHint = HintWaiting[0];

            var duplicate = ModMain.frmMain.PanHint.Children.OfType<MyToast>()
                .FirstOrDefault(t => !t.IsDismissing && t.Context == currentHint.Text && t.ToastType == currentHint.Type);
            if (duplicate != null)
            {
                duplicate.Emphasize();
                HintWaiting.RemoveAt(0);
                return;
            }

            var activeCount = ModMain.frmMain.PanHint.Children.OfType<MyToast>().Count(t => !t.IsDismissing);
            if (activeCount >= 5)
            {
                var oldest = ModMain.frmMain.PanHint.Children.OfType<MyToast>().LastOrDefault(t => !t.IsDismissing);
                oldest?.Dismiss();
                return;
            }

            var toast = new MyToast
            {
                Context = currentHint.Text,
                ToastType = currentHint.Type,
                Icon = currentHint.Type switch
                {
                    HintType.Success => "lucide/circle-check",
                    HintType.Error => "lucide/circle-minus",
                    HintType.Warning => "lucide/triangle-alert",
                    _ => "lucide/info"
                },
                DisplayDuration = (800d + ModBase.MathClamp(currentHint.Text.Length, 5d, 23d) * 180d) * ModAnimation.aniSpeed
            };

            ModMain.frmMain.PanHint.Children.Insert(0, toast);
            toast.Show();
            RearrangeToasts();

            if (currentHint.Log)
                ModBase.Log("[UI] 弹出提示：" + currentHint.Text);
            HintWaiting.RemoveAt(0);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示弹出提示失败", ModBase.LogLevel.Normal);
        }
    }

    // 非叠置布局：弹窗自底向上纵向排开，最新在最下，旧弹窗依次上移，彼此不重叠
    internal static void RearrangeToasts()
    {
        var toasts = ModMain.frmMain.PanHint.Children.OfType<MyToast>().Where(t => !t.IsDismissing).ToList();
        if (toasts.Count == 0)
            return;
        var available = ModMain.frmMain.PanHint.ActualHeight;
        if (available > 0)
        {
            var required = toasts.Sum(t => t._targetHeight + ToastGap) + ToastGap;
            if (required > available)
            {
                toasts[^1].Dismiss(); // 超出可用高度时收掉最旧的弹窗，保证其余完整可见
                return;
            }
        }
        var bottom = ToastGap;
        for (var i = 0; i < toasts.Count; i++)
        {
            var t = toasts[i]; // Children 顺序，索引 0 = 最新
            var oldBottom = t.Margin.Bottom;
            var shift = oldBottom - bottom; // >0 下落补位，<0 上抬让位
            t.Margin = new Thickness(0, 0, 16, bottom);
            t.VerticalAlignment = VerticalAlignment.Bottom;
            Panel.SetZIndex(t, toasts.Count - 1 - i);
            if (Math.Abs(shift) > 0.5)
            {
                var tt = t.RenderTransform as TranslateTransform ?? new TranslateTransform();
                t.RenderTransform = tt;
                tt.Y = -shift; // 视觉先停在旧位置，再动画归零，形成整体层动
                ModAnimation.AniStart(
                    ModAnimation.AaTranslateY(t, shift, 200, ease: new ModAnimation.AniEaseOutFluent()),
                    $"Toast StackSettle {t.Uuid}");
            }
            bottom += t._targetHeight + ToastGap;
        }
    }

    public static void HideAll()
    {
        foreach (MyToast toast in ModMain.frmMain!.PanHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }

    // 弹窗移除后回填错位（由 MyToast 移除自身后调用）
    internal static void OnToastRemoved(MyToast toast)
    {
        RearrangeToasts();
    }
}
