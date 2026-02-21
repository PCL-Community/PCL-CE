using System.Collections.Generic;

namespace PCL.Core.Link.McPing.Model;
public record McPingModInfoResult(
    string Type,
    List<McPingModInfoModResult> ModList);
