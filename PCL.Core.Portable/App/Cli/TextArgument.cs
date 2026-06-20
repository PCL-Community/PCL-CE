// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.App.Cli;

public class TextArgument : CommandArgument<string>
{
    public override ArgumentValueKind ValueKind => ArgumentValueKind.Text;

    protected override string ParseValueText() => ValueText;
}
