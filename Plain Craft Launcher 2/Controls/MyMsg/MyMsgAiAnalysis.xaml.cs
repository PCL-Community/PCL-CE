using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.UI.Controls;

namespace PCL;

public partial class MyMsgAiAnalysis
{
    private readonly ModMain.MyMsgBoxConverter MyConverter;
    private readonly ModLoader.LoaderTask<string, string> Task;
    private readonly int Uuid = ModBase.GetUuid();
    private bool IsStarted;
    private string ResultText = "";

    public MyMsgAiAnalysis(ModMain.MyMsgBoxConverter converter)
    {
        InitializeComponent();
        BtnClose.Name += ModBase.GetUuid();
        BtnCopy.Name += ModBase.GetUuid();
        MyConverter = converter;
        LabTitle.Text = converter.Title;
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);

        Task = new ModLoader.LoaderTask<string, string>("AI 崩溃分析", loader =>
        {
            loader.Progress = 0.05d;
            loader.Output = CrashAiAnalyzer.Analyze(loader.Input);
            loader.Progress = 1d;
        }, Priority: ThreadPriority.BelowNormal);
        Task.OnStateChangedUi += Task_OnStateChangedUi;
        Loading.State = Task;

        Loaded += Load;
    }

    private void Load(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnClose.Focus();
            Opacity = 0d;
            ModAnimation.AniStart(
                ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    new ModBase.MyColor(90d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + Uuid);

            if (!IsStarted)
            {
                IsStarted = true;
                Task.Start(MyConverter.Content?.ToString() ?? "");
            }

            ModBase.Log("[Control] AI 崩溃分析弹窗：" + CrashAiAnalyzer.ApiTypeName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "AI 崩溃分析弹窗加载失败", ModBase.LogLevel.Hint);
        }
    }

    private void Task_OnStateChangedUi(ModLoader.LoaderBase loader, ModBase.LoadState newState,
        ModBase.LoadState oldState)
    {
        if (newState == ModBase.LoadState.Finished)
        {
            ResultText = Task.Output;
            LabResult.Markdown = ResultText;
            Loading.Visibility = Visibility.Collapsed;
            PanError.Visibility = Visibility.Collapsed;
            PanResult.Visibility = Visibility.Visible;
            BtnCopy.Visibility = Visibility.Visible;
        }
        else if (newState == ModBase.LoadState.Failed)
        {
            var ex = loader.Error;
            while (ex?.InnerException is not null)
                ex = ex.InnerException;

            LabError.Text = ex?.Message ?? "未知错误";
            Loading.Visibility = Visibility.Collapsed;
            PanResult.Visibility = Visibility.Collapsed;
            PanError.Visibility = Visibility.Visible;
        }
    }

    private void Close()
    {
        MyConverter.IsExited = true;
        try
        {
            ComponentDispatcher.PopModal();
        }
        catch
        {
        }

        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsgBackground.Background, 200,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), After: true)
        }, "MyMsgBox " + Uuid);
    }

    public void BtnClose_Click(object sender, MouseButtonEventArgs e)
    {
        if (MyConverter.IsExited)
            return;
        Close();
    }

    public void BtnCopy_Click(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ResultText))
            return;
        ModBase.ClipboardSet(ResultText);
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(ShapeLine).Y <= 2d)
                ModMain.FrmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
