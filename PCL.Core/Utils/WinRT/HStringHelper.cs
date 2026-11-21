using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.WinRT;

public class HStringHelper
{
    public static unsafe HString ToHString(string? value)
    {
        if (value is null)
        {
            return new HString(IntPtr.Zero);
        }

        IntPtr handle;
        fixed (char* lpValue = value)
        {
            Marshal.ThrowExceptionForHR(
                WinRTInterop.WindowsCreateString((ushort*)lpValue, value.Length, &handle));
        }

        return new HString(handle);
    }

    public static unsafe string ToManagedString(HString value)
    {
        if (value == IntPtr.Zero)
            return "";
        uint length;
        var buffer = WinRTInterop.WindowsGetStringRawBuffer(value, &length);
        return length != 0 ? new string(buffer, 0, (int)length) : string.Empty;
    }

    public static void DeleteHString(HString value)
    {
        if (!value.IsNull)
            WinRTInterop.WindowsDeleteString(value);
    }

    public static unsafe void WithReference(
        ReadOnlySpan<char> value,
        Action<HString> action)
    {
        fixed (char* p = value)
        {
            WinRTInterop.HStringHeader header;
            IntPtr hstring;

            Marshal.ThrowExceptionForHR(
                WinRTInterop.WindowsCreateStringReference(
                    (ushort*)p,
                    value.Length,
                    (IntPtr*)&header,
                    &hstring));

            action((HString)hstring);
        }
    }
}