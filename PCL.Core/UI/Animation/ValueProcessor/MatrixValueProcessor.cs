using System.Windows.Media;

namespace PCL.Core.UI.Animation.ValueProcessor;

public class MatrixValueProcessor : IValueProcessor<Matrix>
{
    public Matrix Filter(Matrix value) => value;

    public Matrix Add(Matrix value1, Matrix value2)
    {
        return new Matrix(
            value1.M11 + value2.M11, value1.M12 + value2.M12,
            value1.M21 + value2.M21, value1.M22 + value2.M22,
            value1.OffsetX + value2.OffsetX, value1.OffsetY + value2.OffsetY);
    }

    public Matrix Subtract(Matrix value1, Matrix value2)
    {
        return new Matrix(
            value1.M11 - value2.M11, value1.M12 - value2.M12,
            value1.M21 - value2.M21, value1.M22 - value2.M22,
            value1.OffsetX - value2.OffsetX, value1.OffsetY - value2.OffsetY);
    }

    public Matrix Scale(Matrix value, double factor)
    {
        return new Matrix(
            value.M11 * factor, value.M12 * factor,
            value.M21 * factor, value.M22 * factor,
            value.OffsetX * factor, value.OffsetY * factor);
    }

    public Matrix DefaultValue() => new();
    
    public bool Equal(Matrix value1, Matrix value2) => value1 == value2;
}