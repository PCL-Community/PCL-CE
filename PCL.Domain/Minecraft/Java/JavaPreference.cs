// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Domain.Minecraft.Java;

public abstract record JavaPreference;

public sealed record AutoSelectJavaPreference : JavaPreference;

public sealed record ExistingJavaPreference(string JavaExecutablePath) : JavaPreference;

public sealed record UseGlobalJavaPreference : JavaPreference;

public sealed record UseRelativeJavaPreference(string RelativePath) : JavaPreference;
