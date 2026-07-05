// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Extensions;

public readonly record struct ExtensionId
{
    public ExtensionId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("扩展 ID 不能为空。", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public bool Equals(string? value) =>
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);

    public static implicit operator ExtensionId(string value) => new(value);

    public static explicit operator string(ExtensionId id) => id.Value;
}
