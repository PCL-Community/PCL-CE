using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PCL.Core.App.Cli;

public class BoolArgument : CommandArgument<bool>
{
    public override ArgumentValueKind ValueKind => ArgumentValueKind.Bool;

    protected override bool ParseValueText()
    {
        var text = ValueText.ToLowerInvariant().Trim();
        return text is not ("0" or "false");
    }

    public override bool TryCastValue<T>([NotNullWhen(true)] out T value)
    {
        if (base.TryCastValue(out value)) return true;
        var type = typeof(T);
        if (type != typeof(sbyte) &&
            type != typeof(byte) &&
            type != typeof(short) &&
            type != typeof(ushort) &&
            type != typeof(int) &&
            type != typeof(uint) &&
            type != typeof(long) &&
            type != typeof(ulong) &&
            type != typeof(nint) &&
            type != typeof(nuint)) return false;
        // magic code
        var v = Value;
        Unsafe.As<T, byte>(ref value) = Unsafe.As<bool, byte>(ref v);
        // Unsafe.As 直接改写 ref 参数的存储，可空性分析无法跟踪这条写入路径。
        // 已由上方类型检查保证 T 为可容纳 bool 的基元类型，返回 true 时 value 必然已被赋值。
#pragma warning disable CS8762 // 见上方说明：Unsafe.As 写入对可空性分析不可见
        return true;
#pragma warning restore CS8762
    }
}
