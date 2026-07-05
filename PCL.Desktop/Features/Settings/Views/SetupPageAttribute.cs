// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Settings.Views;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class SetupPageAttribute(SetupPageSubType page, string title) : Attribute
{
    public SetupPageSubType Page { get; } = page;

    public string Title { get; } = title;
}
