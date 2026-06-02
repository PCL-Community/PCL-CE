namespace PCL;

public class VersionDataModel
{
    public string Changelog { get; set; } = null!;
    public string Sha256 { get; set; } = null!;
    public string Source { get; set; } = null!;
    public int VersionCode { get; set; }
    public string VersionName { get; set; } = null!;
}