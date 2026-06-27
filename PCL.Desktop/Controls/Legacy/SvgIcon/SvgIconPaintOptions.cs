// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

internal readonly record struct SvgIconPaintOptions(
    IBrush IconBrush,
    double StrokeThickness,
    bool UseOriginalColor);
