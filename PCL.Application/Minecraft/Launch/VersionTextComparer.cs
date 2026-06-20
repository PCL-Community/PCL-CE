// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.RegularExpressions;

namespace PCL.Application.Minecraft.Launch;

internal static partial class VersionTextComparer
{
    public static int Compare(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            return 0;
        if (string.IsNullOrWhiteSpace(left))
            return -1;
        if (string.IsNullOrWhiteSpace(right))
            return 1;

        int[] leftParts = ParseParts(left);
        int[] rightParts = ParseParts(right);
        int max = Math.Max(leftParts.Length, rightParts.Length);
        for (int i = 0; i < max; i++)
        {
            int leftValue = i < leftParts.Length ? leftParts[i] : 0;
            int rightValue = i < rightParts.Length ? rightParts[i] : 0;
            int comparison = leftValue.CompareTo(rightValue);
            if (comparison != 0)
                return comparison;
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static int[] ParseParts(string value) =>
        NumberRegex()
            .Matches(value)
            .Select(static match => int.TryParse(match.ValueSpan, out int part) ? part : 0)
            .ToArray();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
