// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Desktop.Hosting;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class DesktopNavigationPageAttribute(
    string moduleId,
    string route,
    string title,
    string icon,
    int order) : Attribute
{
    public string ModuleId { get; } = moduleId;

    public string Route { get; } = route;

    public string Title { get; } = title;

    public string Icon { get; } = icon;

    public int Order { get; } = order;
}
