using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.UI;

namespace PCL;

public static class HintService
{
    /// <summary>叠置时旧弹窗露出的上沿高度（像素）。</summary>
    private const double ToastPeek = 10d;
    /// <summary>弹窗离容器底部的边距（像素）。</summary>
    private const double ToastBottomMargin = 4d;

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
            ModMain.frmMain.PanHint.VerticalAlignment = VerticalAlignment.Bottom;

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

    // 按叠置规则重排所有弹窗：最新在最前（Z 最高）且在最下，旧弹窗向上错开露出上沿
    // 同时按叠置次序对每个弹窗做淡化：最新全亮，旧弹窗递减至 55% 下限，形成清晰视觉层次
    internal static void RearrangeToasts()
    {
        var toasts = ModMain.frmMain.PanHint.Children.OfType<MyToast>().Where(t => !t.IsDismissing).ToList();
        for (var i = 0; i < toasts.Count; i++)
        {
            var t = toasts[i]; // Children 顺序，索引 0 = 最新
            var oldBottom = t.Margin.Bottom;
            var newBottom = ToastBottomMargin + i * ToastPeek;
            var shift = oldBottom - newBottom; // >0 下落补位，<0 上抬让位
            t.Margin = new Thickness(0, 0, 16, newBottom);
            t.VerticalAlignment = VerticalAlignment.Bottom; // 底部锚定（Grid 内叠置的关键，防止 Stretch 居中断层）
            if (Math.Abs(shift) > 0.5)
            {
                var tt = t.RenderTransform as TranslateTransform ?? new TranslateTransform();
                t.RenderTransform = tt;
                tt.Y = -shift; // 视觉先停在旧位置，再动画归零，形成整体层动
                ModAnimation.AniStart(
                    ModAnimation.AaTranslateY(t, shift, 200, ease: new ModAnimation.AniEaseOutFluent()),
                    $"Toast StackSettle {t.Uuid}");
            }
            Panel.SetZIndex(t, toasts.Count - 1 - i); // 最新 Z 最高

            // 淡化：入场中/拖拽中/隐藏滑出中的弹窗，其 Opacity 分别由入场、拖拽、淡出动画接管，淡化系统不介入
            if (t.IsEntering || t.IsDragging || t.IsHiding ||
                ModAnimation.AniIsRun($"Toast Drag Return {t.Uuid}"))
                continue;
            var target = i == 0 ? 1d : Math.Max(1d - i * 0.15, 0.55d);
            var delta = target - t.Opacity;
            if (Math.Abs(delta) > 0.001)
            {
                ModAnimation.AniStart(
                    ModAnimation.AaOpacity(t, delta, 200, ease: new ModAnimation.AniEaseOutFluent()),
                    $"Toast Dim {t.Uuid}");
            }
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

    // 新弹窗入场动画结束后的回调：复位入场标记并重新分层（把已就位的弹窗淡化到各自档位）
    internal static void NotifyToastShown(MyToast toast)
    {
        toast.IsEntering = false;
        RearrangeToasts();
    }
}
