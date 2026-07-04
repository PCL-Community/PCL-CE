// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1708
public sealed class RouteEventArgs(bool raiseByMouse = false) : EventArgs
{
    public bool Handled { get; set; }

    public bool handled
    {
        get => Handled;
        set => Handled = value;
    }

    public bool RaiseByMouse { get; } = raiseByMouse;

    public bool raiseByMouse => RaiseByMouse;
}
#pragma warning restore CA1708
