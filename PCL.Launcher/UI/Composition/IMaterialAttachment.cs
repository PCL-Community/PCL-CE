namespace PCL.UI.Composition;

internal interface IMaterialAttachment : IDisposable
{
    void MaterialChanged();
    void GeometryChanged();
    void VisualStateChanged();
}
