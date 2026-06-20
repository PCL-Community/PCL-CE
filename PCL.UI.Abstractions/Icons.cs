// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions;

public sealed record IconResource(
    string Key,
    Uri ResourceUri);

public interface IIconService
{
    IconResource? GetIcon(string key);
}
