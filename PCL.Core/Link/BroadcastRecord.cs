using System;
using System.Net;

namespace PCL.Core.Link;

public record BroadcastRecord(string Desc, IPEndPoint Address, DateTime FoundAt);