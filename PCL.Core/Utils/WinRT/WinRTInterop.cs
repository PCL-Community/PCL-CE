using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.WinRT;

public static partial class WinRTInterop
{
    // roapi.h
    [LibraryImport("combase.dll")]
    public static unsafe partial int RoGetActivationFactory(IntPtr activatableClassId, Guid* iid, IntPtr* factory);
    [LibraryImport("combase.dll")]
    public static unsafe partial int RoActiveInstance(IntPtr activatableClassId, IntPtr* instance);
    
    // winstring.h
    [LibraryImport("combase.dll")]
    public static unsafe partial int WindowsCreateString(ushort* sourceString, int length, IntPtr* hstring);
    [LibraryImport("combase.dll")]
    public static unsafe partial int WindowsCreateStringReference(ushort* sourceString, int length,
        IntPtr* hstringHeader, IntPtr* hstring);
    [LibraryImport("combase.dll")]
    public static unsafe partial int WindowsDeleteString(IntPtr hstring);
    [LibraryImport("combase.dll")]
    public static unsafe partial char* WindowsGetStringRawBuffer(IntPtr hstring, uint* length);
    
    // hstring.h
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct HStringHeader
    {
        private fixed byte _data[24];
    }
}