// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Downloads.Views;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DownloadPageAttribute(DownloadPageSubType page, string title) : Attribute
{
    public DownloadPageSubType Page { get; } = page;

    public string Title { get; } = title;
}
