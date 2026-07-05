// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Commands;

public readonly record struct CommandId
{
    public CommandId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("命令 ID 不能为空。", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public bool Equals(string? value) =>
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);

    public static CommandId Parse(string value) => new(value);

    public static explicit operator string(CommandId id) => id.Value;
}
