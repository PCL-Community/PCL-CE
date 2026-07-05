// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Features.Instances.Views;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class InstancePageAttribute(
    InstancePageSubType page,
    string title,
    string description,
    string folderRelativePath,
    bool usesGenericFolderPage) : Attribute
{
    public InstancePageSubType Page { get; } = page;

    public string Title { get; } = title;

    public string Description { get; } = description;

    public string FolderRelativePath { get; } = folderRelativePath;

    public bool UsesGenericFolderPage { get; } = usesGenericFolderPage;
}
