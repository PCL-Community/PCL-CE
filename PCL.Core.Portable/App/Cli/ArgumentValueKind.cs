// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.App.Cli;

public enum ArgumentValueKind
{
    Bool,
#pragma warning disable CA1720 // Public serialized name retained for compatibility.
    Decimal,
#pragma warning restore CA1720
    Text,
}
