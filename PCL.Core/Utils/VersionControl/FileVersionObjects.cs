using System;

namespace PCL.Core.Utils.VersionControl;

public struct FileVersionObjects
{
    public string Path {get;set;}
    /// <summary>
    /// 默认请使用 SHA512
    /// </summary>
    public string Hash {get;set;}
    public ObjectType ObjectType {get;set;}
    public long Length {get;set;}
    public DateTime CreationTime {get;set;}
    public DateTime LastWriteTime {get;set;}
}