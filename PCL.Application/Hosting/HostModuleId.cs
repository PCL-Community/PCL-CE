// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Hosting;

public readonly record struct HostModuleId
{
    public HostModuleId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Host Module ID 不能为空。", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public bool Equals(string? value) =>
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);

    public static HostModuleId Parse(string value) => new(value);

    public static implicit operator HostModuleId(string value) => new(value);

    public static explicit operator string(HostModuleId id) => id.Value;

    public static bool operator ==(HostModuleId left, string? right) => left.Equals(right);

    public static bool operator !=(HostModuleId left, string? right) => !left.Equals(right);

    public static bool operator ==(string? left, HostModuleId right) => right.Equals(left);

    public static bool operator !=(string? left, HostModuleId right) => !right.Equals(left);
}
