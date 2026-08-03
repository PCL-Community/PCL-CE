using System.Windows;
using PCL.Core.UI;

namespace PCL;

public static class HintService
{
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
                var oldest = ModMain.frmMain.PanHint.Children.OfType<MyToast>().FirstOrDefault(t => !t.IsDismissing);
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

            ModMain.frmMain.PanHint.Children.Add(toast);
            toast.Show();

            if (currentHint.Log)
                ModBase.Log("[UI] 弹出提示：" + currentHint.Text);
            HintWaiting.RemoveAt(0);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "显示弹出提示失败", ModBase.LogLevel.Normal);
        }
    }

    public static void HideAll()
    {
        foreach (MyToast toast in ModMain.frmMain!.PanHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }
}
