using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PCL.Core.App.Cli;

public class DecimalArgument : CommandArgument<decimal>
{
    public override ArgumentValueKind ValueKind => ArgumentValueKind.Decimal;

    protected override decimal ParseValueText() => decimal.Parse(ValueText);

    public new decimal Value
    {
        get => base.Value;
        init => base.Value = value;
    }

    public override bool TryCastValue<T>([NotNullWhen(true)] out T value)
    {
        if (base.TryCastValue(out value)) return true;
        var type = typeof(T);
        try
        {
            if (type == typeof(int)) Unsafe.As<T, int>(ref value) = Convert.ToInt32(Value);
            else if (type == typeof(long)) Unsafe.As<T, long>(ref value) = Convert.ToInt64(Value);
            else if (type == typeof(double)) Unsafe.As<T, double>(ref value) = Convert.ToDouble(Value);
            else if (type == typeof(float)) Unsafe.As<T, float>(ref value) = Convert.ToSingle(Value);
            else if (type == typeof(short)) Unsafe.As<T, short>(ref value) = Convert.ToInt16(Value);
            else if (type == typeof(sbyte)) Unsafe.As<T, sbyte>(ref value) = Convert.ToSByte(Value);
            else if (type == typeof(ulong)) Unsafe.As<T, ulong>(ref value) = Convert.ToUInt64(Value);
            else if (type == typeof(uint)) Unsafe.As<T, uint>(ref value) = Convert.ToUInt32(Value);
            else if (type == typeof(ushort)) Unsafe.As<T, ushort>(ref value) = Convert.ToUInt16(Value);
            else if (type == typeof(byte)) Unsafe.As<T, byte>(ref value) = Convert.ToByte(Value);
            else if (type == typeof(nint)) Unsafe.As<T, nint>(ref value) = checked((nint)Convert.ToInt64(Value));
            else if (type == typeof(nuint)) Unsafe.As<T, nuint>(ref value) = checked((nuint)Convert.ToUInt64(Value));
            else return false;
#pragma warning disable CS8762 // The analyzer sucks.
            return true;
#pragma warning restore CS8762
        }
        catch (Exception ex) when (ex is OverflowException or InvalidCastException or FormatException)
        {
            return false;
        }
    }
}
