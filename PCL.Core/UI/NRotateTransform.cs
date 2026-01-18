using System;
using System.Numerics;
using System.Windows.Media;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI;

public struct NRotateTransform : 
    IEquatable<NRotateTransform>,
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

    public NRotateTransform(RotateTransform scaleTransform)
    {
        var uiAccessProvider = AnimationService.UIAccessProvider;
        if (uiAccessProvider.CheckAccess())
        {
            _rotate = GetVector(scaleTransform);
        }
        else
        {
            Vector3 localScale = default;
            uiAccessProvider.Invoke(() => localScale = GetVector(scaleTransform));
            _rotate = localScale;
        }

        return;

        Vector3 GetVector(RotateTransform rt)
        {
            return new Vector3((float)rt.Angle, (float)rt.CenterX, (float)rt.CenterY);
        }
    }
    
    #endregion
    
    #region 运算符重载

    public static NRotateTransform operator +(NRotateTransform a, NRotateTransform b) => new(a.Angle + b.Angle);
    public static NRotateTransform operator -(NRotateTransform a, NRotateTransform b) => new(a.Angle - b.Angle);
    public static NRotateTransform operator *(NRotateTransform a, float b) => new(a.Angle * b);

    public static NRotateTransform operator /(NRotateTransform a, float b) =>
        b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NRotateTransform(a.Angle / b);

    public static bool operator ==(NRotateTransform a, NRotateTransform b) => a._rotate == b._rotate;
    public static bool operator !=(NRotateTransform a, NRotateTransform b) => a._rotate != b._rotate;

    #endregion
    
    #region IEquatable

    public bool Equals(NRotateTransform other)
    {
        return _rotate.Equals(other._rotate);
    }

    public override bool Equals(object? obj)
    {
        if (obj is NRotateTransform color)
            return Equals(color);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Angle, CenterX, CenterY);
    }

    #endregion

    #region 隐式转换

    public static implicit operator RotateTransform(NRotateTransform rt) =>
        new(rt.Angle, rt.CenterX, rt.CenterY);

    public static implicit operator NRotateTransform(RotateTransform rt) => new(rt);

    #endregion
}