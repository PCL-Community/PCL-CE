using System;
using System.Numerics;
using System.Windows.Media;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI;

public struct NScaleTransform : 
    IEquatable<NScaleTransform>,
    IAdditionOperators<NScaleTransform, NScaleTransform, NScaleTransform>,
    ISubtractionOperators<NScaleTransform, NScaleTransform, NScaleTransform>,
    IMultiplyOperators<NScaleTransform, float, NScaleTransform>,
    IDivisionOperators<NScaleTransform, float, NScaleTransform>
{
    private Vector4 _scale;

    public float ScaleX
    {
        get => _scale.X;
        set => _scale.X = value;
    }
    
    public float ScaleY
    {
        get => _scale.Y;
        set => _scale.Y = value;
    }
    
    public float CenterX
    {
        get => _scale.Z;
        set => _scale.Z = value;
    }
    
    public float CenterY
    {
        get => _scale.W;
        set => _scale.W = value;
    }

    #region 构造函数

    public NScaleTransform()
    {
        _scale = new Vector4(1, 1, 0, 0);
    }
    
    public NScaleTransform(float scaleX, float scaleY, float centerX = 0f, float centerY = 0f)
    {
        _scale = new Vector4(scaleX, scaleY, centerX, centerY);
    }

    public NScaleTransform(ScaleTransform scaleTransform)
    {
        var uiAccessProvider = AnimationService.UIAccessProvider;
        if (uiAccessProvider.CheckAccess())
        {
            _scale = GetVector(scaleTransform);
        }
        else
        {
            Vector4 localScale = default;
            uiAccessProvider.Invoke(() => localScale = GetVector(scaleTransform));
            _scale = localScale;
        }

        return;

        Vector4 GetVector(ScaleTransform st)
        {
            return new Vector4((float)st.ScaleX, (float)st.ScaleY, (float)st.CenterX, (float)st.CenterY);
        }
    }
    
    #endregion
    
    #region 运算符重载

    public static NScaleTransform operator +(NScaleTransform a, NScaleTransform b) => new(a.ScaleX + b.ScaleX, a.ScaleY + b.ScaleY);
    public static NScaleTransform operator -(NScaleTransform a, NScaleTransform b) => new(a.ScaleX - b.ScaleX, a.ScaleY - b.ScaleY);
    public static NScaleTransform operator *(NScaleTransform a, float b) => new(a.ScaleX * b, a.ScaleY * b);

    public static NScaleTransform operator /(NScaleTransform a, float b) =>
        b == 0 ? throw new DivideByZeroException("除数不能为零。") : new NScaleTransform(a.ScaleX / b, a.ScaleY / b);

    public static bool operator ==(NScaleTransform a, NScaleTransform b) => a._scale == b._scale;
    public static bool operator !=(NScaleTransform a, NScaleTransform b) => a._scale != b._scale;

    #endregion
    
    #region IEquatable

    public bool Equals(NScaleTransform other)
    {
        return _scale.Equals(other._scale);
    }

    public override bool Equals(object? obj)
    {
        if (obj is NScaleTransform color)
            return Equals(color);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ScaleX, ScaleY, CenterX, CenterY);
    }

    #endregion

    #region 隐式转换

    public static implicit operator ScaleTransform(NScaleTransform st) =>
        new(st.ScaleX, st.ScaleY, st.CenterX, st.CenterY);

    public static implicit operator NScaleTransform(ScaleTransform st) => new(st);

    #endregion
}