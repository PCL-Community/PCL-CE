using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;


namespace PCL;

public static partial class ModMain
{
    public static MySlider? DragControl = null;
    private static int Timer4Count;
    private static int Timer150Count;


    private static void TimerMain()
    {
        try
        {
            #region 每 50ms 执行一次的代码

            HintTick();
            MyMsgBoxTick();
            FrmMain!.DragTick();
            ModLoader.LoaderTaskbarProgressRefresh();
        }

        #endregion

        catch (Exception ex)
        {
            ModBase.Log(ex, "短程主时钟执行异常", ModBase.LogLevel.Critical);
        }

        Timer4Count += 1;
        if (Timer4Count == 4)
        {
            Timer4Count = 0;
            try
            {
                #region 每 250ms 执行一次的代码
            }

            #endregion

            catch (Exception ex)
            {
                ModBase.Log(ex, "中程主时钟执行异常");
            }
        }

        Timer150Count += 1;
        if (Timer150Count == 150)
        {
            Timer150Count = 0;
            try
            {
                #region 每 7.5s 执行一次的代码

                if (FrmMain!.BtnExtraApril_ShowCheck() && AprilDistance != 0)
                    FrmMain.BtnExtraApril.Ribble();
                // 以未知原因窗口被丢到一边去的修复（Top、Left = -25600），还有 #745
                ModBase.RunInUi(() =>
                {
                    if (!FrmMain.Hidden)
                    {
                        if (FrmMain.Top < -9000) FrmMain.Top = 100d;
                        if (FrmMain.Left < -9000) FrmMain.Left = 100d;
                    }
                }); // 窗口拉至最大时 Left = -18.8
            }

            #endregion

            catch (Exception ex)
            {
                ModBase.Log(ex, "长程主时钟执行异常", ModBase.LogLevel.Critical);
            }
        }
    }

    public static void TimerMainStart()
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                while (true)
                {
                    ModBase.RunInUiWait(TimerMain);
                    Thread.Sleep((int)Math.Round(50d * 0.98d));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "程序主时钟出错", ModBase.LogLevel.Feedback);
            }
        }, "Timer Main");
        if (!IsAprilEnabled)
            return;
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var LastTime = Environment.TickCount;
                while (true)
                {
                    if (LastTime != Environment.TickCount)
                    {
                        LastTime = Environment.TickCount;
                        ModBase.RunInUiWait(TimerFool);
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "愚人节主时钟出错", ModBase.LogLevel.Feedback);
            }
        }, "Timer Main Fool");
    }

    #region 愚人节

    public static bool IsAprilEnabled = DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
    public static bool IsAprilGiveup = false;
    private static Vector AprilSpeed = new(0d, 0d);
    private static int AprilIdieCount;
    private static Point AprilMousePosLast = new(0d, 0d);
    private static int AprilDistance;

    private static void TimerFool()
    {
        try
        {
            if (FrmLaunchLeft is null || FrmLaunchLeft.AprilPosTrans is null || FrmMain.lastMouseArg is null)
                return;
            if (IsAprilGiveup || FrmMain.PageCurrent != FormMain.PageType.Launch ||
                ModAnimation.AniControlEnabled != 0 || !FrmLaunchLeft.BtnLaunch.IsLoaded)
                return;

            // 计算是否空闲
            var MousePos = FrmMain.lastMouseArg.GetPosition(FrmMain);
            if (MousePos == AprilMousePosLast)
            {
                AprilIdieCount += 1;
            }
            else
            {
                AprilMousePosLast = MousePos;
                AprilIdieCount = 0;
            }

            // 计算躲避移动
            Vector Direction;
            double Distance;
            var ButtonWidth = FrmLaunchLeft.BtnLaunch.ActualWidth / 2d;
            var ButtonHeight = FrmLaunchLeft.BtnLaunch.ActualHeight / 2d;
            var Vec = (Vector)(FrmMain.lastMouseArg.GetPosition(FrmLaunchLeft.BtnLaunch) -
                               new Vector(ButtonWidth, ButtonHeight));
            var Dir = new Vector(Vec.X, Vec.Y);
            Dir.Normalize();
            Direction = -Dir;
            Distance = new Vector(Math.Max(0d, Math.Abs(Vec.X) - ButtonWidth),
                Math.Max(0d, Math.Abs(Vec.Y) - ButtonHeight)).Length;
            var BreathScale = Math.Sin(Timer150Count / 37.5d * Math.PI);
            var Acc = Math.Max(0d, BreathScale * 0.25d - 0.65d - Math.Log((Distance + 0.4d) / 200d)) * Direction; // 加速度
            // 计算回归移动
            if (AprilIdieCount >= 64 * 5)
            {
                var SafeDist = (Vector)(FrmMain.lastMouseArg.GetPosition(FrmMain.PanMain) -
                                        new Vector(ButtonWidth, FrmMain.PanMain.ActualHeight - ButtonHeight * 3d));
                var Back = new Vector(FrmLaunchLeft.AprilPosTrans.X, FrmLaunchLeft.AprilPosTrans.Y);
                if (SafeDist.Length > 250d && Back.Length > 0.4d)
                {
                    Acc -= Back * 0.0005d;
                    Back.Normalize();
                    Acc -= Back * 0.15d;
                }
            }

            // 回到边界
            var Relative = FrmLaunchLeft.BtnLaunch.TranslatePoint(new Point(0d, 0d), FrmMain.PanForm);
            if (Relative.X < -ButtonWidth * 2d)
            {
                FrmLaunchLeft.AprilPosTrans.X += FrmMain.PanForm.ActualWidth + ButtonWidth * 2d; // 离开左边界
                AprilSpeed.X -= 80d;
                if (Relative.Y < 0d)
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5d;
                else if (Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2d)
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5d;
            }
            else if (Relative.X > FrmMain.PanForm.ActualWidth)
            {
                FrmLaunchLeft.AprilPosTrans.X -= FrmMain.PanForm.ActualWidth + ButtonWidth * 2d; // 离开右边界
                AprilSpeed.X += 80d;
                if (Relative.Y < 0d)
                    FrmLaunchLeft.AprilPosTrans.Y += ButtonHeight * 2.5d;
                else if (Relative.Y > FrmMain.PanForm.ActualHeight - ButtonHeight * 2d)
                    FrmLaunchLeft.AprilPosTrans.Y -= ButtonHeight * 2.5d;
            }
            else if (Relative.Y < -ButtonHeight * 2d)
            {
                FrmLaunchLeft.AprilPosTrans.Y += FrmMain.PanForm.ActualHeight + ButtonHeight * 2d; // 离开上边界
                AprilSpeed.Y -= 25d;
                if (Relative.X < 0d)
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2d;
                else if (Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2d)
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2d;
            }
            else if (Relative.Y > FrmMain.PanForm.ActualHeight)
            {
                FrmLaunchLeft.AprilPosTrans.Y -= FrmMain.PanForm.ActualHeight + ButtonHeight * 2d; // 离开下边界
                AprilSpeed.Y += 25d;
                if (Relative.X < 0d)
                    FrmLaunchLeft.AprilPosTrans.X += ButtonWidth * 2d;
                else if (Relative.X > FrmMain.PanForm.ActualWidth - ButtonWidth * 2d)
                    FrmLaunchLeft.AprilPosTrans.X -= ButtonWidth * 2d;
            }

            // 移动
            AprilSpeed = AprilSpeed * 0.8d + Acc;
            var SpeedValue = Math.Min(60d, AprilSpeed.Length);
            if (SpeedValue < 0.01d)
                return;
            AprilSpeed.Normalize();
            AprilSpeed *= SpeedValue;
            AprilDistance = (int)Math.Round(AprilDistance + SpeedValue);
            FrmLaunchLeft.AprilPosTrans.X += AprilSpeed.X;
            FrmLaunchLeft.AprilPosTrans.Y += AprilSpeed.Y;
            // 大小改变
            FrmLaunchLeft.AprilScaleTrans.ScaleX =
                ModBase.MathClamp(1d - (Math.Abs(Direction.X) - Math.Abs(Direction.Y)) * (SpeedValue / 160d), 0.2d,
                    1.8d);
            FrmLaunchLeft.AprilScaleTrans.ScaleY =
                ModBase.MathClamp(1d - (Math.Abs(Direction.Y) - Math.Abs(Direction.X)) * (SpeedValue / 100d), 0.2d,
                    1.8d);
            // 放弃提示
            if (AprilDistance > 4000)
            {
                AprilDistance = -4000;
                switch (RandomUtils.NextInt(0, 3))
                {
                    case 0:
                    {
                        Hint("放弃吧！只需要点一下右下角的小白旗……");
                        break;
                    }
                    case 1:
                    {
                        Hint("看到右下角的那面小白旗了吗？");
                        break;
                    }
                    case 2:
                    {
                        Hint("这里建议点一下右下角的小白旗投降呢.jpg");
                        break;
                    }
                    case 3:
                    {
                        Hint("右下角的小白旗永远等着你……");
                        break;
                    }
                }
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "愚人节移动出错", ModBase.LogLevel.Feedback);
        }
    }

    #endregion
}
