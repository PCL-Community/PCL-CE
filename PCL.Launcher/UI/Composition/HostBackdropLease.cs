using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace PCL.UI.Composition;

internal static class HostBackdropLease
{
    private const int UseHostBackdropBrush = 17;

    public static IDisposable? Acquire(TopLevel topLevel)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return null;
        var handle = topLevel.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
            return null;

        var previous = 0;
        if (DwmGetWindowAttribute(handle, UseHostBackdropBrush, ref previous, sizeof(int)) != 0)
            previous = 0;

        var enabled = 1;
        if (DwmSetWindowAttribute(handle, UseHostBackdropBrush, ref enabled, sizeof(int)) != 0)
            return null;
        return new Lease(handle, previous);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private sealed class Lease(IntPtr handle, int previous) : IDisposable
    {
        private IntPtr _handle = handle;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (current == IntPtr.Zero)
                return;
            var value = previous;
            DwmSetWindowAttribute(current, UseHostBackdropBrush, ref value, sizeof(int));
        }
    }
}
