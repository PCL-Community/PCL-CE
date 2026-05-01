using System;
using System.Net;

namespace PCL.CE.Core.Link;

public record BroadcastRecord(string Desc, IPEndPoint Address, DateTime FoundAt);