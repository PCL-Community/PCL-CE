using System.Windows;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;

namespace PCL;

public static partial class Launcher
{
    public static bool isAprilEnabled = DateTime.Now.Month == 4 && DateTime.Now.Day == 1;
    public static bool isAprilGiveup = false;

    private static Vector _aprilSpeed = new(0d, 0d);
    private static int _aprilIdieCount;
    private static Point _aprilMousePosLast = new(0d, 0d);
    private static int _aprilDistance;

    /// <summary>
    ///     更新愚人节按钮的位置和状态。
    /// </summary>
    private static void TimerFool()
    {
        try
        {
            var launchPage = frmLaunchLeft;
            if (launchPage?.AprilPosTrans is null || frmMain is null)
                return;

            var mouseArgs = frmMain.lastMouseArg;
            if (mouseArgs is null
                || isAprilGiveup
                || frmMain.pageCurrent != FormMain.PageType.Launch
                || Animations.AniExitsAnyControl()
                || !launchPage.BtnLaunch.IsLoaded)
                return;

            UpdateAprilMouseIdle(mouseArgs.GetPosition(frmMain));

            var halfWidth = launchPage.BtnLaunch.ActualWidth / 2d;
            var halfHeight = launchPage.BtnLaunch.ActualHeight / 2d;

            var (acceleration, direction) = CalculateAprilAcceleration(
                mouseArgs,
                halfWidth,
                halfHeight);

            WrapAprilPosition(halfWidth, halfHeight);
            MoveAprilButton(acceleration, direction);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "愚人节移动出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     记录鼠标连续静止的时间。
    /// </summary>
    private static void UpdateAprilMouseIdle(Point mousePosition)
    {
        if (mousePosition == _aprilMousePosLast)
        {
            _aprilIdieCount++;
            return;
        }

        _aprilMousePosLast = mousePosition;
        _aprilIdieCount = 0;
    }

    /// <summary>
    ///     计算按钮躲避鼠标的加速度，以及静止后的回归力。
    /// </summary>
    private static (Vector Acceleration, Vector Direction)
        CalculateAprilAcceleration(
            MouseEventArgs mouseArgs,
            double halfWidth,
            double halfHeight)
    {
        if (frmLaunchLeft is null || frmMain is null)
            return (default, default);

        var offset = mouseArgs.GetPosition(frmLaunchLeft.BtnLaunch)
            - new Point(halfWidth, halfHeight);

        var direction = -offset;
        direction.Normalize();

        var distance = new Vector(
            Math.Max(0d, Math.Abs(offset.X) - halfWidth),
            Math.Max(0d, Math.Abs(offset.Y) - halfHeight)
        ).Length;

        var breathScale = Math.Sin(_timer150Count / 37.5d * Math.PI);
        var acceleration = Math.Max(
                0d,
                breathScale * 0.25d - 0.65d - Math.Log((distance + 0.4d) / 200d)
            )
            * direction;

        if (_aprilIdieCount < 64 * 5)
            return (acceleration, direction);

        var mainPanel = frmMain.PanMain;
        var safeOffset = mouseArgs.GetPosition(mainPanel)
            - new Point(
                halfWidth,
                mainPanel.ActualHeight - halfHeight * 3d);

        var back = new Vector(
            frmLaunchLeft.AprilPosTrans.X,
            frmLaunchLeft.AprilPosTrans.Y);

        if (!(safeOffset.Length > 250d) || !(back.Length > 0.4d))
            return (acceleration, direction);

        acceleration -= back * 0.0005d;

        back.Normalize();
        acceleration -= back * 0.15d;

        return (acceleration, direction);
    }

    /// <summary>
    ///     处理按钮越过窗口边界后的穿越效果。
    /// </summary>
    private static void WrapAprilPosition(double halfWidth, double halfHeight)
    {
        if (frmLaunchLeft?.AprilPosTrans is null || frmMain is null)
            return;

        var button = frmLaunchLeft.BtnLaunch;
        var transform = frmLaunchLeft.AprilPosTrans;
        var container = frmMain.PanForm;

        var position = button.TranslatePoint(new Point(0d, 0d), container);
        var width = container.ActualWidth;
        var height = container.ActualHeight;

        if (position.X < -halfWidth * 2d || position.X > width)
        {
            var fromLeft = position.X < -halfWidth * 2d;
            var offset = width + halfWidth * 2d;

            transform.X += fromLeft ? offset : -offset;
            _aprilSpeed.X += fromLeft ? -80d : 80d;

            if (position.Y < 0d)
                transform.Y += halfHeight * 2.5d;
            else if (position.Y > height - halfHeight * 2d)
                transform.Y -= halfHeight * 2.5d;
        }
        else if (position.Y < -halfHeight * 2d || position.Y > height)
        {
            var fromTop = position.Y < -halfHeight * 2d;
            var offset = height + halfHeight * 2d;

            transform.Y += fromTop ? offset : -offset;
            _aprilSpeed.Y += fromTop ? -25d : 25d;

            if (position.X < 0d)
                transform.X += halfWidth * 2d;
            else if (position.X > width - halfWidth * 2d)
                transform.X -= halfWidth * 2d;
        }
    }

    /// <summary>
    ///     根据加速度更新按钮速度、位置和缩放。
    /// </summary>
    private static void MoveAprilButton(Vector acceleration, Vector direction)
    {
        if (frmLaunchLeft?.AprilPosTrans is null || frmLaunchLeft.AprilScaleTrans is null)
            return;

        _aprilSpeed = _aprilSpeed * 0.8d + acceleration;

        var speed = Math.Min(60d, _aprilSpeed.Length);
        if (speed < 0.01d)
            return;

        _aprilSpeed.Normalize();
        _aprilSpeed *= speed;

        _aprilDistance = (int)Math.Round(_aprilDistance + speed);

        var transform = frmLaunchLeft.AprilPosTrans;
        transform.X += _aprilSpeed.X;
        transform.Y += _aprilSpeed.Y;

        var scale = frmLaunchLeft.AprilScaleTrans;
        var absX = Math.Abs(direction.X);
        var absY = Math.Abs(direction.Y);

        scale.ScaleX = Common.MathClamp(
            1d - (absX - absY) * (speed / 160d),
            0.2d,
            1.8d);

        scale.ScaleY = Common.MathClamp(
            1d - (absY - absX) * (speed / 100d),
            0.2d,
            1.8d);

        ShowAprilGiveupHint();
    }

    /// <summary>
    ///     移动足够距离后，随机提示用户投降。
    /// </summary>
    private static void ShowAprilGiveupHint()
    {
        if (_aprilDistance <= 4000)
            return;

        _aprilDistance = -4000;

        var message = RandomUtils.NextInt(0, 3) switch
        {
            0 => Lang.Text("Main.April.Hint1"),
            1 => Lang.Text("Main.April.Hint2"),
            2 => Lang.Text("Main.April.Hint3"),
            3 => Lang.Text("Main.April.Hint4"),
            _ => null
        };

        if (message is not null)
            HintService.Show(Hints.Info(message));
    }
}
