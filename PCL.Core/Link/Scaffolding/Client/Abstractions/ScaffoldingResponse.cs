using System;

namespace PCL.Core.Link.Scaffolding.Client.Abstractions;

public record ScaffoldingResponse(byte Status, ReadOnlyMemory<byte> Body);