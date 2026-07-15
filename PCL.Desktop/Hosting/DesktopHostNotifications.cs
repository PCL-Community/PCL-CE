// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using PCL.Application.Hosting.RuntimeExtensions;

namespace PCL.Desktop.Hosting;

/// <summary>
/// Forwards plugin notifications to the active main-window toast surface when attached;
/// otherwise captures messages for diagnostics (design §9 notifications).
/// </summary>
internal sealed class DesktopHostNotifications : IHostNotifications
{
    public static DesktopHostNotifications Instance { get; } = new();

    private readonly ConcurrentQueue<string> _captured = new();
    private Action<string, bool>? _handler;

    public IReadOnlyCollection<string> CapturedMessages => _captured.ToArray();

    public void Attach(Action<string, bool> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public void Detach(Action<string, bool> handler)
    {
        if (ReferenceEquals(_handler, handler))
            _handler = null;
    }

    public void ShowInformation(string message) => Dispatch(message, critical: false);

    public void ShowWarning(string message) => Dispatch(message, critical: true);

    private void Dispatch(string message, bool critical)
    {
        string text = message ?? string.Empty;
        Action<string, bool>? handler = _handler;
        if (handler is not null)
        {
            try
            {
                handler(text, critical);
                return;
            }
            catch
            {
                // fall through to capture
            }
        }

        _captured.Enqueue((critical ? "[warn] " : "[info] ") + text);
    }
}
