using System.Numerics;
using Avalonia;
using Avalonia.Media;
using PCL.Core.UI.Animation.Core;

namespace PCL.Core.UI.Animation;

/// <summary>
///     缩放变换的值类型：缩放比例加缩放中心，可与其他变换值直接做加减与缩放。
/// </summary>
/// <remarks>
///     <see cref="ScaleTransform" /> 没有 CenterX/CenterY，缩放中心改由
///     <see cref="Visual.RenderTransformOrigin" /> 表达；写回时由
///     <see cref="Animatable.AvaloniaAnimatable" />
///     按控件当前尺寸换算。
/// </remarks>
public struct NScaleTransform
    : IEquatable<NScaleTransform>,
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

    public NScaleTransform(
        float scaleX,
        float scaleY,
        float centerX = 0f,
        float centerY = 0f)
    {
        _scale = new Vector4(scaleX, scaleY, centerX, centerY);
    }

    public NScaleTransform(ScaleTransform scaleTransform)
    {
        var uiAccessProvider = AnimationService.UiAccessProvider;
        if (uiAccessProvider.CheckAccess())
        {
            _scale = _GetVector(scaleTransform);
        }
        else
        {
            Vector4 local = default;
            uiAccessProvider.Invoke(() => local = _GetVector(scaleTransform));
            _scale = local;
        }
    }

    private static Vector4 _GetVector(ScaleTransform scaleTransform)
    {
        return new Vector4(
            (float)scaleTransform.ScaleX,
            (float)scaleTransform.ScaleY,
            0f,
            0f);
    }

    #endregion

    #region 运算符重载

    public static NScaleTransform operator +(NScaleTransform a, NScaleTransform b)
    {
        return new NScaleTransform(
            a.ScaleX + b.ScaleX,
            a.ScaleY + b.ScaleY,
            a.CenterX + b.CenterX,
            a.CenterY + b.CenterY);
    }

    public static NScaleTransform operator -(NScaleTransform a, NScaleTransform b)
    {
        return new NScaleTransform(
            a.ScaleX - b.ScaleX,
            a.ScaleY - b.ScaleY,
            a.CenterX - b.CenterX,
            a.CenterY - b.CenterY);
    }

    public static NScaleTransform operator *(NScaleTransform a, float b)
    {
        return new NScaleTransform(a.ScaleX * b, a.ScaleY * b, a.CenterX * b, a.CenterY * b);
    }

    public static NScaleTransform operator /(NScaleTransform a, float b)
    {
        return b == 0
            ? throw new DivideByZeroException("除数不能为零。")
            : new NScaleTransform(a.ScaleX / b, a.ScaleY / b, a.CenterX / b, a.CenterY / b);
    }

    public static bool operator ==(NScaleTransform a, NScaleTransform b)
    {
        return a._scale == b._scale;
    }

    public static bool operator !=(NScaleTransform a, NScaleTransform b)
    {
        return a._scale != b._scale;
    }

    #endregion

    #region IEquatable

    public bool Equals(NScaleTransform other)
    {
        return _scale.Equals(other._scale);
    }

    public override bool Equals(object? obj)
    {
        if (obj is NScaleTransform scaleTransform)
            return Equals(scaleTransform);
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ScaleX, ScaleY, CenterX, CenterY);
    }

    #endregion

    #region 隐式转换

    public static implicit operator ScaleTransform(NScaleTransform st)
    {
        return new ScaleTransform(st.ScaleX, st.ScaleY);
    }

    public static implicit operator NScaleTransform(ScaleTransform st)
    {
        return new NScaleTransform(st);
    }

    #endregion
}