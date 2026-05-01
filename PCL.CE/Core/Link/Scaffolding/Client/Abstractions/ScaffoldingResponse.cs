using System;

namespace PCL.CE.Core.Link.Scaffolding.Client.Abstractions;

public record ScaffoldingResponse(byte Status, ReadOnlyMemory<byte> Body);