// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

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
        if (type == typeof(sbyte))
        {
            sbyte converted = Value ? (sbyte)1 : (sbyte)0;
            value = Unsafe.As<sbyte, T>(ref converted);
        }
        else if (type == typeof(byte))
        {
            byte converted = Value ? (byte)1 : (byte)0;
            value = Unsafe.As<byte, T>(ref converted);
        }
        else if (type == typeof(short))
        {
            short converted = Value ? (short)1 : (short)0;
            value = Unsafe.As<short, T>(ref converted);
        }
        else if (type == typeof(ushort))
        {
            ushort converted = Value ? (ushort)1 : (ushort)0;
            value = Unsafe.As<ushort, T>(ref converted);
        }
        else if (type == typeof(int))
        {
            int converted = Value ? 1 : 0;
            value = Unsafe.As<int, T>(ref converted);
        }
        else if (type == typeof(uint))
        {
            uint converted = Value ? 1U : 0U;
            value = Unsafe.As<uint, T>(ref converted);
        }
        else if (type == typeof(long))
        {
            long converted = Value ? 1L : 0L;
            value = Unsafe.As<long, T>(ref converted);
        }
        else if (type == typeof(ulong))
        {
            ulong converted = Value ? 1UL : 0UL;
            value = Unsafe.As<ulong, T>(ref converted);
        }
        else if (type == typeof(nint))
        {
            nint converted = Value ? 1 : 0;
            value = Unsafe.As<nint, T>(ref converted);
        }
        else if (type == typeof(nuint))
        {
            nuint converted = Value ? 1U : 0U;
            value = Unsafe.As<nuint, T>(ref converted);
        }
        else
        {
            return false;
        }
#pragma warning disable CS8762 // The analyzer sucks.
        return true;
#pragma warning restore CS8762
    }
}
