using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.Data.Xml.Dom;

public static class IXmlDocumentIOInfo
{
    public static readonly string ActivatableClassId = "Windows.Data.Xml.Dom.XmlDocument";
    public static readonly Guid Iid = new("6cd0e74e-ee65-4489-9ebf-ca43e87ba637");
}

public unsafe struct IXmlDocumentIO
{
    public IXmlDocumentIOVtbl* lpVtbl;
}

public unsafe struct IXmlDocumentIOVtbl
{
    // IUnknown
    public delegate* unmanaged<void*, Guid*, void**, int> QueryInterface;
    public delegate* unmanaged<void*, uint> AddRef;
    public delegate* unmanaged<void*, uint> Release;

    // IInspectable
    public delegate* unmanaged<void*, uint*, Guid**, int> GetIids;
    public delegate* unmanaged<void*, IntPtr*, int> GetRuntimeClassName;
    public delegate* unmanaged<void*, int*, int> GetTrustLevel;

    // IXmlDocumentIO

    /// <summary>
    /// void LoadXml(string xml)
    /// </summary>
    public delegate* unmanaged<void*, IntPtr, int> LoadXml;

    /// <summary>
    /// void LoadXml(string xml, XmlLoadSettings settings)
    /// </summary>
    public delegate* unmanaged<void*, IntPtr, void*, int> LoadXmlWithSettings;

    /// <summary>
    /// IAsyncAction SaveToFileAsync(IStorageFile file)
    /// </summary>
    public delegate* unmanaged<void*, void*, void**, int> SaveToFileAsync;
}