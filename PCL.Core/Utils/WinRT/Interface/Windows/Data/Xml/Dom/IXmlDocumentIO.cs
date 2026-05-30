using System;

namespace PCL.Core.Utils.WinRT.Interface.Windows.Data.Xml.Dom;

internal unsafe struct IXmlDocumentIO
{
    public IXmlDocumentIOVtbl* lpVtbl;
}

internal unsafe struct IXmlDocumentIOVtbl
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

    // LoadXml(string xml)
    public delegate* unmanaged<void*, IntPtr, int> LoadXml;

    // LoadXml(string xml, XmlLoadSettings settings)
    public delegate* unmanaged<void*, IntPtr, void*, int> LoadXmlWithSettings;

    // SaveToFileAsync(IStorageFile file)
    public delegate* unmanaged<void*, void*, void**, int> SaveToFileAsync;
}