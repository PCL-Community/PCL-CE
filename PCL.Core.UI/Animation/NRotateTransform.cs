using System.Numerics;
using Avalonia.Media;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

/// <summary>
///     旋转变换的值类型：角度加旋转中心，可与其他变换值直接做加减与缩放。
/// </summary>
public struct NRotateTransform
    : IEquatable<NRotateTransform>,
        IAdditionOperators<NRotateTransform, NRotateTransform, NRotateTransform>,
        ISubtractionOperators<NRotateTransform, NRotateTransform, NRotateTransform>,
        IMultiplyOperators<NRotateTransform, float, NRotateTransform>,
        IDivisionOperators<NRotateTransform, float, NRotateTransform>
{
    private Vector3 _rotate;

    public float Angle
    {
        get => _rotate.X;
        set => _rotate.X = value;
    }

    public float CenterX
    {
        get => _rotate.Y;
        set => _rotate.Y = value;
    }

    public float CenterY
    {
        get => _rotate.Z;
        set => _rotate.Z = value;
    }

    #region 构造函数

    public NRotateTransform()
    {
        _rotate = new Vector3(0, 0, 0);
    }

    public NRotateTransform(float angle, float centerX = 0f, float centerY = 0f)
    {
        _rotate = new Vector3(angle, centerX, centerY);
    }

    public NRotateTransform(RotateTransform rotateTransform)
    {
        var uiAccessProvider = AnimationService.UiAccessProvider;
        if (uiAccessProvider.CheckAccess())
        {
            _rotate = _GetVector(rotateTransform);
        }
        else
        {
            Vector3 local = default;
            uiAccessProvider.Invoke(() => local = _GetVector(rotateTransform));
            _rotate = local;
        }
    }

    private static Vector3 _GetVector(RotateTransform rotateTransform)
    {
        return new Vector3(
            (float)rotateTransform.Angle,
            (float)rotateTransform.CenterX,
            (float)rotateTransform.CenterY);
    }

    #endregion

    #region 运算符重载

    public static NRotateTransform operator +(NRotateTransform a, NRotateTransform b)
    {
        return new NRotateTransform(
            a.Angle + b.Angle,
            a.CenterX + b.CenterX,
            a.CenterY + b.CenterY);
    }

    public static NRotateTransform operator -(NRotateTransform a, NRotateTransform b)
    {
        return new NRotateTransform(
            a.Angle - b.Angle,
            a.CenterX - b.CenterX,
            a.CenterY - b.CenterY);
    }

    public static NRotateTransform operator *(NRotateTransform a, float b)
    {
        return new NRotateTransform(a.Angle * b, a.CenterX * b, a.CenterY * b);
    }

    public static NRotateTransform operator /(NRotateTransform a, float b)
    {
        return b == 0
            ? throw new DivideByZeroException("除数不能为零。")
            : new NRotateTransform(a.Angle / b, a.CenterX / b, a.CenterY / b);
    }

    public static bool operator ==(NRotateTransform a, NRotateTransform b)
    {
        return a._rotate == b._rotate;
    }

    public static bool operator !=(NRotateTransform a, NRotateTransform b)
    {
        return a._rotate != b._rotate;
    }

    #endregion

    #region IEquatable

    public bool Equals(NRotateTransform other)
    {
        return _rotate.Equals(other._rotate);
    }

    public override bool Equals(object? obj)
    {
        if (obj is NRotateTransform rotateTransform)
            return Equals(rotateTransform);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Angle, CenterX, CenterY);
    }

    #endregion

    #region 隐式转换

    public static implicit operator RotateTransform(NRotateTransform rt)
    {
        return new RotateTransform(rt.Angle, rt.CenterX, rt.CenterY);
    }

    public static implicit operator NRotateTransform(RotateTransform rt)
    {
        return new NRotateTransform(rt);
    }

    #endregion
}