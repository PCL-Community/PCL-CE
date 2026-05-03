using PCL.Core.App;
using PCL.Network;

namespace PCL;

public class DlMod
{
    /// DlMod | Mod 镜像源请求

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JObject。
    ///     调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
    public static object DlModRequest(string Url, bool IsJson = false)
    {
        var Urls = new List<KeyValuePair<string, int>>();
        var McimUrl = DlSource.DlSourceModGet(Url);
        if ((McimUrl ?? "") != (Url ?? ""))
            Urls = BuildModRequestUrls(Url, McimUrl, Config.Download.Comp.CompSourceSolution);

        var Exs = "";
        foreach (var Source in Urls)
            try
            {
                return IsJson
                    ? Requester.FetchJson(Source.Key, new RequestParam
                    {
                        Timeout = Source.Value * 1000,
                        UseBrowserUserAgent = true
                    })
                    : Requester.FetchString(Source.Key, new RequestParam
                    {
                        Timeout = Source.Value * 1000,
                        UseBrowserUserAgent = true
                    });
            }
            catch (Exception ex)
            {
                if (!ex.Message.ContainsF("mcimirror")) Exs += ex.Message + "\r\n";
            }

        throw new Exception(Exs);
    }

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理。
    ///     调用 NetRequest，会进行重试。
    /// </summary>
    public static string DlModRequest(string Url, string Method, string Data, string ContentType,
        bool allowMirror = false)
    {
        var Urls = new List<KeyValuePair<string, int>>();
        var McimUrl = DlSource.DlSourceModGet(Url);
        if ((McimUrl ?? "") != (Url ?? ""))
            Urls = BuildModRequestUrls(Url, McimUrl, allowMirror ? Config.Download.Comp.CompSourceSolution : 2);

        var Exs = "";
        foreach (var Source in Urls)
            try
            {
                return Requester.Fetch(Source.Key, new FetchParam
                {
                    Method = Method,
                    Content = Data, 
                    ContentType = ContentType,
                    Timeout = Source.Value * 1000
                });
            }
            catch (Exception ex)
            {
                if (!ex.Message.ContainsF("mcimirror")) Exs += ex.Message + "\r\n";
            }

        throw new Exception(Exs);
    }

    private static List<KeyValuePair<string, int>> BuildModRequestUrls(string url, string mcimUrl, int sourceSolution)
    {
        var urls = new List<KeyValuePair<string, int>>();
        switch (sourceSolution)
        {
            case 0:
                urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                urls.Add(new KeyValuePair<string, int>(url, 15));
                break;
            case 1:
                urls.Add(new KeyValuePair<string, int>(url, 5));
                urls.Add(new KeyValuePair<string, int>(mcimUrl, 5));
                urls.Add(new KeyValuePair<string, int>(url, 15));
                urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                break;
            default:
                urls.Add(new KeyValuePair<string, int>(url, 5));
                urls.Add(new KeyValuePair<string, int>(url, 15));
                urls.Add(new KeyValuePair<string, int>(mcimUrl, 10));
                break;
        }
        return urls;
    }
}