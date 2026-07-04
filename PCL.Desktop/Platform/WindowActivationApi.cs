// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace PCL.Desktop.Platform;

internal static class WindowActivationApi
{
    private const int RestoreWindowCommand = 9;

    public static void BringToForeground(Window window)
    {
        if (!OperatingSystem.IsWindows())
            return;

        nint handle = window.TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0)
            return;

        WindowsForegroundApi.ShowWindow(handle, RestoreWindowCommand);
        WindowsForegroundApi.SetForegroundWindow(handle);
    }

    private static class WindowsForegroundApi
    {
        private static readonly Lazy<Api?> ApiInstance = new(LoadApi);

        public static void ShowWindow(nint hWnd, int nCmdShow)
        {
            _ = ApiInstance.Value?.ShowWindow(hWnd, nCmdShow);
        }

        public static void SetForegroundWindow(nint hWnd)
        {
            _ = ApiInstance.Value?.SetForegroundWindow(hWnd);
        }

        private static Api? LoadApi()
        {
            if (!NativeLibrary.TryLoad("user32.dll", out nint library))
                return null;

            if (!NativeLibrary.TryGetExport(library, "ShowWindow", out nint showWindow) ||
                !NativeLibrary.TryGetExport(library, "SetForegroundWindow", out nint setForegroundWindow))
            {
                NativeLibrary.Free(library);
                return null;
            }

            return new Api(
                library,
                Marshal.GetDelegateForFunctionPointer<ShowWindowDelegate>(showWindow),
                Marshal.GetDelegateForFunctionPointer<SetForegroundWindowDelegate>(setForegroundWindow));
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool ShowWindowDelegate(nint hWnd, int nCmdShow);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate bool SetForegroundWindowDelegate(nint hWnd);

        private sealed class Api(nint library, ShowWindowDelegate showWindow, SetForegroundWindowDelegate setForegroundWindow)
        {
            private readonly nint _library = library;

            public bool ShowWindow(nint hWnd, int nCmdShow) => showWindow(hWnd, nCmdShow);

            public bool SetForegroundWindow(nint hWnd) => setForegroundWindow(hWnd);

            ~Api()
            {
                if (_library != 0)
                    NativeLibrary.Free(_library);
            }
        }
    }
}
