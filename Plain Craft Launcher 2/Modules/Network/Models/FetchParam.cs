using System.Text;

namespace PCL.Network;

public struct FetchParam
{
    public string Method { get; set; }
    public object? Content { get; set; }
    public string? ContentType { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
    public Encoding? Encoding { get; set; }
    public string? Accept { get; set; }
    public string? FallbackUrl { get; set; }
    public bool UseBrowserUserAgent { get; set; }
    public bool MakeLog { get; set; }
    public bool DontRetryOnRefused { get; set; }
    public int Timeout { get; set; }
    public bool RequireContent { get; set; }
}
