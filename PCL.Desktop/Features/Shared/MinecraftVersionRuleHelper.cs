// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace PCL.Desktop.Features.Shared;

internal static class MinecraftVersionRuleHelper
{
    public static bool IsFormatFit(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        if (version.Length >= 3 &&
            version.StartsWith("1.", StringComparison.Ordinal) &&
            char.IsDigit(version[2]))
        {
            return true;
        }

        string major = version.Split('.', 2)[0];
        return int.TryParse(major, NumberStyles.Integer, CultureInfo.InvariantCulture, out int majorValue) &&
               majorValue > 25 &&
               version.Contains('.', StringComparison.Ordinal);
    }

    public static int VersionToDrop(string? version, bool allowSnapshot = false)
    {
        if (string.IsNullOrWhiteSpace(version))
            return 0;
        if (!allowSnapshot && version.Contains('-', StringComparison.Ordinal))
            return 0;

        string[] segments = version.Split('-', 2)[0].Split('.');
        if (segments.Length < 2 ||
            !int.TryParse(segments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major) ||
            !int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minor))
        {
            return 0;
        }

        if (major == 1)
            return minor * 10;
        if (major < 25)
            return 0;

        return major * 10 + minor;
    }
}
