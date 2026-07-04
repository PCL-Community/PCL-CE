// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;
using PCL.Online;

namespace PCL.Desktop.Platform;

internal static class WindowsOnlineAccountBridge
{
    private const string AssemblyName = "PCL.Online.Windows";
    private const string ServiceTypeName = "PCL.Online.Windows.WindowsOnlineAccountService";
    private const string AssemblyQualifiedServiceTypeName =
        "PCL.Online.Windows.WindowsOnlineAccountService, PCL.Online.Windows";
    private static readonly object LoadLock = new();
    private static Type? _serviceType;
    private static bool _loadAttempted;

    public static bool IsAvailable => OperatingSystem.IsWindows() && TryGetServiceType() is not null;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The optional Windows integration assembly is copied next to the desktop app and exposes a stable public static bridge surface.")]
    public static void RegisterIfAvailable()
    {
        Type? serviceType = TryGetServiceType();
        MethodInfo? register = serviceType?.GetMethod(
            "Register",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            Type.EmptyTypes,
            modifiers: null);
        register?.Invoke(null, null);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075",
        Justification = "The optional Windows integration assembly is copied next to the desktop app and exposes a stable public static bridge surface.")]
    public static async Task<OnlineLoginResult?> LoginWithWindowsAccountAsync(
        Window owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        Type? serviceType = TryGetServiceType();
        if (serviceType is null)
            return null;

        MethodInfo? loginMethod = serviceType.GetMethod(
            "LoginWithWindowsAccountAsync",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: [typeof(IntPtr), typeof(CancellationToken)],
            modifiers: null);
        if (loginMethod is null)
            return null;

        IntPtr handle = owner.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (loginMethod.Invoke(null, [handle, cancellationToken]) is not Task<OnlineLoginResult> loginTask)
            return null;

        return await loginTask.ConfigureAwait(true);
    }

    private static Type? TryGetServiceType()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        lock (LoadLock)
        {
            if (_loadAttempted)
                return _serviceType;

            _loadAttempted = true;
            try
            {
                _serviceType = Type.GetType(AssemblyQualifiedServiceTypeName, throwOnError: false);
            }
            catch
            {
                _serviceType = null;
            }

            return _serviceType;
        }
    }
}
