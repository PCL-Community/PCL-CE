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

    private static Common.SafeList<HintMessage> HintWaiting
    {
        get => field ??= new Common.SafeList<HintMessage>();
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
            Launcher.frmMain!.PanHint.HorizontalAlignment = HorizontalAlignment.Right;
            Launcher.frmMain.PanHint.VerticalAlignment = VerticalAlignment.Bottom;

            var extraHeight = Launcher.frmMain.PanExtraButtons.ActualHeight;
            Launcher.frmMain.PanHint.Margin =
                new Thickness(0, 0, 0, extraHeight > 0 ? extraHeight + 20 : 20);

            if (!HintWaiting.Any())
                return;

            var currentHint = HintWaiting[0];

            var duplicate = Launcher.frmMain.PanHint.Children.OfType<MyToast>()
                .FirstOrDefault(t => !t.IsDismissing && t.Context == currentHint.Text && t.ToastType == currentHint.Type);
            if (duplicate != null)
            {
                duplicate.Emphasize();
                HintWaiting.RemoveAt(0);
                return;
            }

            var activeCount = Launcher.frmMain.PanHint.Children.OfType<MyToast>()
                .Count(t => !t.IsDismissing);
            if (activeCount >= 5)
            {
                var oldest = Launcher.frmMain.PanHint.Children.OfType<MyToast>()
                    .FirstOrDefault(t => !t.IsDismissing);
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
                DisplayDuration =
                    (800d + Common.MathClamp(currentHint.Text.Length, 5d, 23d) * 180d) *
                    Animations.aniSpeed
            };

            Launcher.frmMain.PanHint.Children.Add(toast);
            toast.Show();

            if (currentHint.Log)
                Common.Log("[UI] 弹出提示：" + currentHint.Text);
            HintWaiting.RemoveAt(0);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "显示弹出提示失败", Common.LogLevel.Normal);
        }
    }

    public static void HideAll()
    {
        foreach (var toast in Launcher.frmMain!.PanHint.Children.OfType<MyToast>().ToList())
            toast.Dismiss();
    }
}
