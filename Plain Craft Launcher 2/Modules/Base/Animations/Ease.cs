namespace PCL;

public static partial class Animations
{
    public enum AniEasePower
    {
        Weak = 2,
        Middle = 3,
        Strong = 4,
        ExtraStrong = 5
    }

    /// <summary>
    ///     缓动函数基类。
    /// </summary>
    public abstract class AniEase
    {
        /// <summary>
        ///     获取函数值。
        /// </summary>
        /// <param name="t">时间百分比。</param>
        public abstract double GetValue(double t);

        /// <summary>
        ///     获取增量值。
        /// </summary>
        /// <param name="t1">较大的 X。</param>
        /// <param name="t0">较小的 X。</param>
        public virtual double GetDelta(double t1, double t0)
        {
            return GetValue(t1) - GetValue(t0);
        }
    }

    /// <summary>
    ///     渐入渐出组合。
    /// </summary>
    public class AniEaseInout(
        AniEase easeIn,
        AniEase easeOut,
        double easeInPercent = 0.5d) : AniEase
    {
        public override double GetValue(double t)
        {
            if (t < easeInPercent)
                return easeInPercent * easeIn.GetValue(t / easeInPercent);

            return (1d - easeInPercent)
                * easeOut.GetValue((t - easeInPercent) / (1d - easeInPercent))
                + easeInPercent;
        }
    }

    /// <summary>
    ///     线性，无缓动。
    /// </summary>
    public class AniEaseLinear : AniEase
    {
        public override double GetValue(double t)
        {
            return Common.MathClamp(t, 0d, 1d);
        }

        public override double GetDelta(double t1, double t0)
        {
            return Common.MathClamp(t1, 0d, 1d) - Common.MathClamp(t0, 0d, 1d);
        }
    }

    /// <summary>
    ///     平滑开始。
    /// </summary>
    public class AniEaseInFluent(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        public override double GetValue(double t)
        {
            return Math.Pow(Common.MathClamp(t, 0d, 1d), (double)power);
        }
    }

    /// <summary>
    ///     平滑结束。
    /// </summary>
    public class AniEaseOutFluent(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        public override double GetValue(double t)
        {
            return 1d - Math.Pow(Common.MathClamp(1d - t, 0d, 1d), (double)power);
        }
    }

    /// <summary>
    ///     平滑开始与结束。
    /// </summary>
    public class AniEaseInoutFluent(
        AniEasePower power = AniEasePower.Middle,
        double middle = 0.5d) : AniEase
    {
        private readonly AniEaseInout _ease = new(
            new AniEaseInFluent(power),
            new AniEaseOutFluent(power),
            middle);

        public override double GetValue(double t)
        {
            return _ease.GetValue(t);
        }
    }

    /// <summary>
    ///     以特定速度开始的平滑结束。
    /// </summary>
    public class AniEaseOutFluentWithInitial : AniEase
    {
        private readonly double _alpha;

        /// <param name="initialPixelPerSecond">初速度，px/s</param>
        /// <param name="totalSecond">总时长，s</param>
        /// <param name="totalDistance">总路程，px</param>
        public AniEaseOutFluentWithInitial(
            double initialPixelPerSecond,
            double totalSecond,
            double totalDistance)
        {
            var v0Norm = initialPixelPerSecond * totalSecond / totalDistance;
            _alpha = v0Norm - 1.0d;
            if (_alpha < 0d)
                _alpha = 0d;
        }

        public override double GetValue(double percent)
        {
            var p = Common.MathClamp(percent, 0d, 1d);
            if (_alpha == 0d)
                return p;
            return (_alpha + 1d) * p / (1d + _alpha * p);
        }
    }

    /// <summary>
    ///     回弹开始。有效时间为 1/3。
    /// </summary>
    public class AniEaseInBack(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly double _p = 3d - (double)power * 0.5d;

        public override double GetValue(double t)
        {
            t = Common.MathClamp(t, 0d, 1d);
            return Math.Pow(t, _p) * Math.Cos(1.5d * Math.PI * (1d - t));
        }
    }

    /// <summary>
    ///     回弹结束。有效时间为 1/3。
    /// </summary>
    public class AniEaseOutBack(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly double _p = 3d - (double)power * 0.5d;

        public override double GetValue(double t)
        {
            t = Common.MathClamp(t, 0d, 1d);
            return 1d - Math.Pow(1d - t, _p) * Math.Cos(1.5d * Math.PI * t);
        }
    }

    /// <summary>
    ///     回弹开始，短平滑结束。
    /// </summary>
    public class AniEaseInCar(
        double middle = 0.7d,
        AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly AniEaseInout _ease = new(
            new AniEaseInBack(power),
            new AniEaseOutFluent(power),
            middle);

        public override double GetValue(double t)
        {
            return _ease.GetValue(t);
        }
    }

    /// <summary>
    ///     短平滑开始，回弹结束。
    /// </summary>
    public class AniEaseOutCar(
        double middle = 0.3d,
        AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly AniEaseInout _ease = new(
            new AniEaseInFluent(power),
            new AniEaseOutBack(power),
            middle);

        public override double GetValue(double t)
        {
            return _ease.GetValue(t);
        }
    }

    /// <summary>
    ///     弹簧开始。约在 60% 到达最小值。
    /// </summary>
    public class AniEaseInElastic(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly int _p = (int)power + 4;

        public override double GetValue(double t)
        {
            t = Common.MathClamp(t, 0d, 1d);
            return Math.Pow(t, (_p - 1) * 0.25d)
                * Math.Cos((_p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }

    /// <summary>
    ///     弹簧结束。约在 40% 到达最大值。
    /// </summary>
    public class AniEaseOutElastic(AniEasePower power = AniEasePower.Middle) : AniEase
    {
        private readonly int _p = (int)power + 4;

        public override double GetValue(double t)
        {
            t = 1d - Common.MathClamp(t, 0d, 1d);
            return 1d
                - Math.Pow(t, (_p - 1) * 0.25d)
                * Math.Cos((_p - 3.5d) * Math.PI * Math.Pow(1d - t, 1.5d));
        }
    }
}