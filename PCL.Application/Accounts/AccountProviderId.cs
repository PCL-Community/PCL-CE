// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Accounts;

public readonly record struct AccountProviderId
{
    public AccountProviderId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("账号提供者 ID 不能为空。", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public bool Equals(string? value) =>
        string.Equals(Value, value, StringComparison.OrdinalIgnoreCase);

    public static implicit operator AccountProviderId(string value) => new(value);

    public static explicit operator string(AccountProviderId id) => id.Value;
}
