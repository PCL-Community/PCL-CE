using Avalonia;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class MatrixValueProcessor : IValueProcessor<Matrix>
{
    public Matrix Filter(Matrix value)
    {
        return value;
    }

    public Matrix Add(Matrix value1, Matrix value2)
    {
        return new Matrix(
            value1.M11 + value2.M11,
            value1.M12 + value2.M12,
            value1.M21 + value2.M21,
            value1.M22 + value2.M22,
            value1.M31 + value2.M31,
            value1.M32 + value2.M32);
    }

    public Matrix Subtract(Matrix value1, Matrix value2)
    {
        return new Matrix(
            value1.M11 - value2.M11,
            value1.M12 - value2.M12,
            value1.M21 - value2.M21,
            value1.M22 - value2.M22,
            value1.M31 - value2.M31,
            value1.M32 - value2.M32);
    }

    public Matrix Scale(Matrix value, double factor)
    {
        return new Matrix(
            value.M11 * factor,
            value.M12 * factor,
            value.M21 * factor,
            value.M22 * factor,
            value.M31 * factor,
            value.M32 * factor);
    }

    public Matrix DefaultValue()
    {
        return Matrix.Identity;
    }

    public bool Equal(Matrix value1, Matrix value2)
    {
        return value1 == value2;
    }
}