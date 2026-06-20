// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PCL.Core.Utils.Diff;

public interface IBinaryDiff
{
    public Task<byte[]> MakeAsync(byte[] originData, byte[] newData);
    public Task<byte[]> ApplyAsync(byte[] originData, byte[] diffData);
}
