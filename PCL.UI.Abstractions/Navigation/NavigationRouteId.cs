// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Navigation;

public readonly record struct NavigationRouteId
{
    public NavigationRouteId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("导航路由 ID 不能为空。", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public bool Equals(string? value) =>
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => Value;

    public static NavigationRouteId Parse(string value) => new(value);

    public static explicit operator string(NavigationRouteId route) => route.Value;
}
