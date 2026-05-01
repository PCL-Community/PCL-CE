using System;

namespace PCL.CE.Core.Utils.VersionControl;

public struct VersionData
{
    /// <summary>
    /// ID 用 GUID
    /// </summary>
    public string NodeId {get;set;}
    public DateTime Created {get;set;}
    public string Name {get;set;}
    public string Desc {get;set;}
    public long Version {get;set;}
}