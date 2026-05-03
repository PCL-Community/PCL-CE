using PCL.Core.App;
using PCL.Network;

namespace PCL;

public class DlMod
{
        #region DlMod | Mod 镜像源请求

    /// <summary>
    ///     对可能涉及 Mod 镜像源的请求进行处理，返回字符串或 JObject。
    ///     调用 NetGetCodeByRequest，会进行重试。
    /// </summary>
    public static object DlModRequest(string Url, bool IsJson = false)
    {
        var Urls = new List<KeyValuePair<string, int>>();
        var McimUrl = DlSource.DlSourceModGet(Url);
        if ((McimUrl ?? "") != (Url ?? ""))
            switch (Config.Download.Comp.CompSourceSolution)
            {
                case 0:
                {
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    break;
                }
                case 1:
                {
                    Urls.Add(new KeyValuePair<string, int>(Url, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }

                default:
                {
                    Urls.Add(new KeyValuePair<string, int>(Url, 5));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }
            }

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
            switch (allowMirror ? Config.Download.Comp.CompSourceSolution : 2)
            {
                case 0:
                {
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    break;
                }
                case 1:
                {
                    Urls.Add(new KeyValuePair<string, int>(Url, 5));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 5));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }

                default:
                {
                    Urls.Add(new KeyValuePair<string, int>(Url, 5));
                    Urls.Add(new KeyValuePair<string, int>(Url, 15));
                    Urls.Add(new KeyValuePair<string, int>(McimUrl, 10));
                    break;
                }
            }

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

    #endregion
}