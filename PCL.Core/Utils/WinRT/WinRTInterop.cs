using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.WinRT;

public static partial class WinRTInterop
{
    // roapi.h
    [LibraryImport("combase.dll")]
    private static unsafe partial int RoGetActivationFactory(IntPtr activatableClassId, Guid* iid, IntPtr* factory);
    [LibraryImport("combase.dll")]
    private static unsafe partial int RoActivateInstance(IntPtr activatableClassId, IntPtr* instance);

    public static unsafe IntPtr ActivateInstance(ReadOnlySpan<char> activatableClassId)
    {
        var instance = IntPtr.Zero;

        fixed (char* pName = activatableClassId)
        {
            HStringHeader header;
            IntPtr hstring;

            Marshal.ThrowExceptionForHR(
                WindowsCreateStringReference(
                    (ushort*)pName,
                    activatableClassId.Length,
                    (IntPtr*)&header,
                    &hstring));

            Marshal.ThrowExceptionForHR(
                RoActivateInstance(
                    hstring,
                    &instance));
        }

        return instance;
    }

    public static unsafe IntPtr GetActivationFactory(ReadOnlySpan<char> activatableClassId, Guid iid)
    {
        var factory = IntPtr.Zero;

        fixed (char* pName = activatableClassId)
        {
            HStringHeader header;
            IntPtr hstring;

            Marshal.ThrowExceptionForHR(
                WindowsCreateStringReference(
                    (ushort*)pName,
                    activatableClassId.Length,
                    (IntPtr*)&header,
                    &hstring));

            Marshal.ThrowExceptionForHR(
                RoGetActivationFactory(
                    hstring,
                    &iid,
                    &factory));
        }

        return factory;
    }
    
    // winstring.h
    [LibraryImport("combase.dll")]
    internal static unsafe partial int WindowsCreateString(ushort* sourceString, int length, IntPtr* hstring);
    [LibraryImport("combase.dll")]
    internal static unsafe partial int WindowsCreateStringReference(ushort* sourceString, int length,
        IntPtr* hstringHeader, IntPtr* hstring);
    [LibraryImport("combase.dll")]
    internal static unsafe partial int WindowsDeleteString(IntPtr hstring);
    [LibraryImport("combase.dll")]
    internal static unsafe partial char* WindowsGetStringRawBuffer(IntPtr hstring, uint* length);
    
    // hstring.h
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HStringHeader
    {
        private fixed byte _data[24];
    }
}