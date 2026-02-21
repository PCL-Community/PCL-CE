using System.Buffers;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

public static class ModNet
{
    /// <summary>
    ///     预下载检查行为。
    /// </summary>
    public enum NetPreDownloadBehaviour
    {
        /// <summary>
        ///     当文件已存在时，显示提示以提醒用户是否继续下载。
        /// </summary>
        HintWhileExists,

        /// <summary>
        ///     当文件已存在或正在下载时，直接退出下载函数执行，不对用户进行提示。
        /// </summary>
        ExitWhileExistsOrDownloading,

        /// <summary>
        ///     不进行已存在检查。
        /// </summary>
        IgnoreCheck
    }

    /// <summary>
    ///     下载进度标示。
    /// </summary>
    public enum NetState
    {
        /// <summary>
        ///     尚未进行已存在检查。
        /// </summary>
        WaitingToCheck = -1,

        /// <summary>
        ///     尚未开始。
        /// </summary>
        WaitingToDownload = 0,

        /// <summary>
        ///     正在连接，尚未获取文件大小。
        /// </summary>
        Connecting = 1,

        /// <summary>
        ///     已获取文件大小，尚未有有效下载。
        /// </summary>
        Reading = 2,

        /// <summary>
        ///     正在下载。
        /// </summary>
        Downloading = 3,

        /// <summary>
        ///     正在合并文件。
        /// </summary>
        Merging = 4,

        /// <summary>
        ///     已完成。
        /// </summary>
        Finished = 5,

        /// <summary>
        ///     已失败或中断。
        /// </summary>
        Interrupted = 6
    }

    public const string NetDownloadEnd = ".PCLDownloading";

    /// <summary>
    ///     最大线程数。
    /// </summary>
    public static int NetTaskThreadLimit;

    /// <summary>
    ///     速度下限。
    /// </summary>
    public static long NetTaskSpeedLimitLow = 256L * 1024L; // 256K/s

    /// <summary>
    ///     速度上限。若无限制则为 -1。
    /// </summary>
    public static long NetTaskSpeedLimitHigh = -1;

    /// <summary>
    ///     基于限速，当前可以下载的剩余量。
    /// </summary>
    public static long NetTaskSpeedLimitLeft = -1;

    private static readonly object NetTaskSpeedLimitLeftLock = new();
    private static long NetTaskSpeedLimitLeftLast;

    /// <summary>
    ///     正在运行中的线程数。
    /// </summary>
    public static int NetTaskThreadCount;

    private static readonly object NetTaskThreadCountLock = new();

    // 快速进行大小校验
    private static readonly ModBase.SafeDictionary<string, long> _CheckExistingFile_Sizes = new();
    public static NetManagerClass NetManager = new();

    /// <summary>
    ///     测试 Ping。失败则返回 -1。
    /// </summary>
    public static int Ping(string Ip, int Timeout = 10000, bool MakeLog = true)
    {
        PingReply PingResult;
        try
        {
            PingResult = new Ping().Send(Ip);
        }
        catch (Exception ex)
        {
            if (MakeLog)
                ModBase.Log("[Net] Ping " + Ip + " 失败：" + ex.Message);
            return -1;
        }

        if (PingResult.Status == IPStatus.Success)
        {
            if (MakeLog)
                ModBase.Log("[Net] Ping " + Ip + " 结束：" + PingResult.RoundtripTime + "ms");
            return (int)PingResult.RoundtripTime;
        }

        if (MakeLog)
            ModBase.Log("[Net] Ping " + Ip + " 失败");
        return -1;
    }

    /// <summary>
    ///     <see cref="HttpResponseMessage.EnsureSuccessStatusCode" /> 的改进版，将抛出附带 <c>StatusCode</c> 和 <c>ReasonPhrase</c>
    ///     属性的异常。
    ///     这个改进已经在 .NET 5 官方实装，鬼知道为什么 .NET Framework 连最新的 4.8.1 都这么原始。
    /// </summary>
    /// <exception cref="HttpRequestFailedException">HTTP 响应失败</exception>
    private static void EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            response.Content?.Dispose();
            throw new HttpRequestFailedException(response, content);
        }
    }

    /// <summary>
    ///     以 WebRequest 获取网页源代码或 Json。会进行至多 45 秒 3 次的尝试，允许最长 30s 的超时。
    /// </summary>
    /// <param name="Url">网页的 Url。</param>
    /// <param name="Encode">网页的编码，通常为 UTF-8。</param>
    /// <param name="BackupUrl">如果第一次尝试失败，换用的备用 URL。</param>
    /// <param name="IsJson">是否解析为 Json。</param>
    /// <param name="Accept">请求的套接字类型。</param>
    /// <param name="UseBrowserUserAgent">是否使用浏览器 User-Agent。</param>
    public static object NetGetCodeByRequestRetry(string Url, Encoding Encode = null, string Accept = "",
        bool IsJson = false, string BackupUrl = null, bool UseBrowserUserAgent = false)
    {
        var RetryCount = 0;
        Exception RetryException = null;
        var StartTime = TimeUtils.GetTimeTick();
        while (RetryCount <= 3)
        {
            RetryCount += 1;
            try
            {
                switch (RetryCount)
                {
                    case 0: // 正常尝试
                    {
                        return NetGetCodeByRequestOnce(Url, Encode, 10000, IsJson, Accept, UseBrowserUserAgent);
                    }
                    case 1: // 慢速重试
                    {
                        Thread.Sleep(500);
                        return NetGetCodeByRequestOnce(BackupUrl ?? Url, Encode, 30000, IsJson, Accept,
                            UseBrowserUserAgent); // 快速重试
                    }

                    default:
                    {
                        if (TimeUtils.GetTimeTick() - StartTime > 5500)
                        {
                            // 若前两次加载耗费 5 秒以上，才进行重试
                            Thread.Sleep(500);
                            return NetGetCodeByRequestOnce(BackupUrl ?? Url, Encode, 4000, IsJson, Accept,
                                UseBrowserUserAgent);
                        }

                        throw RetryException;
                    }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                RetryException = ex;
            }
        }

        throw RetryException;
    }

    public static object NetGetCodeByRequestOnce(string Url, Encoding Encode = null, int Timeout = 30000,
        bool IsJson = false, string Accept = "", bool UseBrowserUserAgent = false)
    {
        if (ModBase.RunInUi() && !Url.Contains("//127."))
            throw new Exception("在 UI 线程执行了网络请求");
        try
        {
            Url = Conversions.ToString(ModSecret.SecretCdnSign(Url));
            ModBase.Log($"[Net] 获取网络结果：{Url}，超时 {Timeout}ms{(IsJson ? "，要求 Json" : "")}");
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(Timeout);
                using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
                {
                    request.Headers.Accept.ParseAdd(Accept);
                    var argClient = request;
                    ModSecret.SecretHeadersSign(Url, ref argClient, UseBrowserUserAgent);
                    using (var response = NetworkService.GetClient()
                               .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter()
                               .GetResult())
                    {
                        EnsureSuccessStatusCode(response);
                        if (Encode is null)
                            Encode = Encoding.UTF8;
                        using (var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        {
                            // 读取流并转换为字符串
                            using (var reader = new StreamReader(responseStream, Encode))
                            {
                                var content = reader.ReadToEnd();
                                if (string.IsNullOrEmpty(content))
                                    throw new WebException("获取结果失败，内容为空（" + Url + "）");
                                return IsJson ? ModBase.GetJson(content) : content;
                            }
                        }
                    }
                }
            }
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException("连接服务器超时（" + Url + "）", ex);
        }
        catch (HttpRequestFailedException ex)
        {
            throw new HttpWebException("获取结果失败，" + ex.Message + "（" + Url + "）", ex);
        }
        catch (Exception ex)
        {
            throw new WebException("获取结果失败，" + ex.Message + "（" + Url + "）", ex);
        }
    }

    /// <summary>
    ///     以多线程下载网页文件的方式获取网页源代码。
    /// </summary>
    /// <param name="Url">网页的 Url。</param>
    public static string NetGetCodeByLoader(string Url, int Timeout = 45000, bool IsJson = false,
        bool UseBrowserUserAgent = false)
    {
        string NetGetCodeByLoaderRet = default;
        var Temp = ModMain.RequestTaskTempFolder() + "download.txt";
        var NewTask = new LoaderDownload("源码获取 " + ModBase.GetUuid() + "#",
            new List<NetFile>
                { new(new[] { Url }, Temp, new ModBase.FileChecker { IsJson = IsJson }, UseBrowserUserAgent) });
        try
        {
            NewTask.WaitForExitTime(Timeout, TimeoutMessage: "连接服务器超时（" + Url + "）");
            NetGetCodeByLoaderRet = ModBase.ReadFile(Temp);
            File.Delete(Temp);
        }
        finally
        {
            NewTask.Abort();
        }

        return NetGetCodeByLoaderRet;
    }

    /// <summary>
    ///     以多线程下载网页文件的方式获取网页源代码。
    /// </summary>
    /// <param name="Urls">网页的 Url 列表。</param>
    public static string NetGetCodeByLoader(IEnumerable<string> Urls, int Timeout = 45000, bool IsJson = false,
        bool UseBrowserUserAgent = false)
    {
        string NetGetCodeByLoaderRet = default;
        var Temp = ModMain.RequestTaskTempFolder() + "download.txt";
        var NewTask = new LoaderDownload("源码获取 " + ModBase.GetUuid() + "#",
            new List<NetFile> { new(Urls, Temp, new ModBase.FileChecker { IsJson = IsJson }, UseBrowserUserAgent) });
        try
        {
            NewTask.WaitForExitTime(Timeout, TimeoutMessage: "连接服务器超时（第一下载源：" + Urls.First() + "）");
            NetGetCodeByLoaderRet = ModBase.ReadFile(Temp);
            File.Delete(Temp);
        }
        finally
        {
            NewTask.Abort();
        }

        return NetGetCodeByLoaderRet;
    }

    /// <summary>
    ///     使用 HttpClient 从网络中下载文件。这不能下载 CDN 中的文件。
    /// </summary>
    /// <param name="Url">网络 Url。</param>
    /// <param name="LocalFile">下载的本地地址。</param>
    public static async Task NetDownloadByClient(string Url, string LocalFile, bool UseBrowserUserAgent = false)
    {
        ModBase.Log("[Net] 直接下载文件：" + Url);
        try
        {
            Directory.CreateDirectory(ModBase.GetPathFromFullPath(LocalFile));
            if (File.Exists(LocalFile))
                File.Delete(LocalFile);
            using (var request = new HttpRequestMessage(HttpMethod.Get, Url))
            {
                var argClient = request;
                ModSecret.SecretHeadersSign(Url, ref argClient, UseBrowserUserAgent);
                using (var response = await NetworkService.GetClient()
                           .SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    EnsureSuccessStatusCode(response);
                    using (var httpStream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var fileStream = new FileStream(LocalFile, FileMode.Create))
                        {
                            await httpStream.CopyToAsync(fileStream);
                        }
                    }
                }
            }
        }
        catch (TaskCanceledException ex) when (ex.InnerException is null)
        {
            throw new TimeoutException($"下载超时（{Url}）", ex);
        }
        catch (HttpRequestFailedException ex)
        {
            throw new HttpWebException($"下载失败：{ex.Message}（{Url}）", ex);
        }
        catch (Exception ex)
        {
            if (File.Exists(LocalFile))
                File.Delete(LocalFile);
            throw new WebException($"下载失败：{ex.Message}（{Url}）", ex);
        }
    }

    /// <summary>
    ///     简单的多线程下载文件。可以下载 CDN 中的文件。
    /// </summary>
    /// <param name="Url">文件的 Url。</param>
    /// <param name="LocalFile">下载的本地地址。</param>
    public static void NetDownloadByLoader(string Url, string LocalFile,
        ModLoader.LoaderBase LoaderToSyncProgress = null, ModBase.FileChecker Check = null,
        bool UseBrowserUserAgent = false)
    {
        var NewTask = new LoaderDownload("文件下载 " + ModBase.GetUuid() + "#",
            new List<NetFile> { new(new[] { Url }, LocalFile, Check, UseBrowserUserAgent) });
        try
        {
            NewTask.WaitForExit(LoaderToSyncProgress: LoaderToSyncProgress);
        }
        catch (Exception ex)
        {
            throw new WebException($"多线程直接下载文件失败（{Url}）", ex);
        }
        finally
        {
            NewTask.Abort();
        }
    }

    /// <summary>
    ///     简单的多线程下载文件。可以下载 CDN 中的文件。
    /// </summary>
    /// <param name="Urls">文件的 Url 列表。</param>
    /// <param name="LocalFile">下载的本地地址。</param>
    public static void NetDownloadByLoader(IEnumerable<string> Urls, string LocalFile,
        ModLoader.LoaderBase LoaderToSyncProgress = null, ModBase.FileChecker Check = null,
        bool UseBrowserUserAgent = false)
    {
        var NewTask = new LoaderDownload("文件下载 " + ModBase.GetUuid() + "#",
            new List<NetFile> { new(Urls, LocalFile, Check, UseBrowserUserAgent) });
        try
        {
            NewTask.WaitForExit(LoaderToSyncProgress: LoaderToSyncProgress);
        }
        catch (Exception ex)
        {
            throw new WebException("多线程直接下载文件失败（第一下载源：" + Urls.First() + "）", ex);
        }
        finally
        {
            NewTask.Abort();
        }
    }

    /// <summary>
    ///     发送一个网络请求并获取返回内容，会重试三次并在最长 45s 后超时。
    /// </summary>
    /// <param name="Url">请求的服务器地址。</param>
    /// <param name="Method">请求方式（POST 或 GET）。</param>
    /// <param name="Data">请求的内容。</param>
    /// <param name="ContentType">请求的套接字类型。</param>
    /// <param name="DontRetryOnRefused">当返回 40x 时不重试。</param>
    public static string NetRequestRetry(string Url, string Method, object Data, string ContentType,
        bool DontRetryOnRefused = true, Dictionary<string, string> Headers = null)
    {
        var RetryCount = 0;
        Exception RetryException = null;
        var StartTime = TimeUtils.GetTimeTick();
        while (RetryCount <= 3)
        {
            RetryCount += 1;
            try
            {
                switch (RetryCount)
                {
                    case 0: // 正常尝试
                    {
                        return NetRequestOnce(Url, Method, Data, ContentType, 15000, Headers);
                    }
                    case 1: // 慢速重试
                    {
                        Thread.Sleep(500);
                        return NetRequestOnce(Url, Method, Data, ContentType, 25000, Headers); // 快速重试
                    }

                    default:
                    {
                        if (TimeUtils.GetTimeTick() - StartTime > 5500)
                        {
                            // 若前两次加载耗费 5 秒以上，才进行重试
                            Thread.Sleep(500);
                            return NetRequestOnce(Url, Method, Data, ContentType, 4000, Headers);
                        }

                        throw RetryException;
                    }
                }
            }
            catch (ThreadInterruptedException ex)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (ex.InnerException is not null && ex.InnerException is HttpRequestFailedException &&
                    ((int)((HttpRequestFailedException)ex.InnerException).StatusCode).ToString().StartsWithF("4") &&
                    DontRetryOnRefused)
                    throw;
                RetryException = ex;
                ModBase.Log(ex, $"[Net] 网络请求第 {RetryCount} 次失败（{Url}）");
            }
        }

        throw RetryException;
    }

    /// <summary>
    ///     发送一次网络请求并获取返回内容。
    /// </summary>
    /// <param name="Url"></param>
    /// <param name="Method"></param>
    /// <param name="Data"></param>
    /// <param name="ContentType">仅 Data 为 string 时可用</param>
    /// <param name="Timeout"></param>
    /// <param name="Headers"></param>
    /// <param name="MakeLog"></param>
    /// <param name="UseBrowserUserAgent"></param>
    /// <returns></returns>
    public static string NetRequestOnce(string Url, string Method, object Data, string ContentType, int Timeout = 25000,
        Dictionary<string, string> Headers = null, bool MakeLog = true, bool UseBrowserUserAgent = false)
    {
        if (ModBase.RunInUi() && !Url.Contains("//127."))
            throw new Exception("在 UI 线程执行了网络请求");
        Url = Conversions.ToString(ModSecret.SecretCdnSign(Url));
        if (MakeLog)
            ModBase.Log("[Net] 发起网络请求（" + Method + "，" + Url + "），最大超时 " + Timeout);
        try
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.CancelAfter(Timeout);
                var RequestMethod = HttpMethod.Get;
                switch (Method.ToUpper() ?? "") // 我不相信上面的输入.jpg
                {
                    case "POST":
                    {
                        RequestMethod = HttpMethod.Post;
                        break;
                    }
                    case "PUT":
                    {
                        RequestMethod = HttpMethod.Put;
                        break;
                    }
                    case "DELETE":
                    {
                        RequestMethod = HttpMethod.Delete;
                        break;
                    }
                    case "HEAD":
                    {
                        RequestMethod = HttpMethod.Head;
                        break;
                    }
                    case "OPTIONS":
                    {
                        RequestMethod = HttpMethod.Options;
                        break;
                    }
                }

                using (var request = new HttpRequestMessage(RequestMethod, Url))
                {
                    var argClient = request;
                    ModSecret.SecretHeadersSign(Url, ref argClient, UseBrowserUserAgent);
                    if (new[] { HttpMethod.Post, HttpMethod.Put }.Contains(RequestMethod))
                        if (!(Data == null))
                        {
                            if (Data is byte[])
                                request.Content = new ByteArrayContent((byte[])Data);
                            else if (Data is string)
                                request.Content = new StringContent(Conversions.ToString(Data), Encoding.UTF8,
                                    ContentType);
                            else if (Data.GetType().IsSubclassOf(typeof(HttpContent)))
                                request.Content = (HttpContent)Data;
                            else
                                throw new ArgumentException("Data 参数类型不支持");
                        }

                    if (Headers is not null)
                        foreach (var Pair in Headers)
                        {
                            if (string.IsNullOrWhiteSpace(Pair.Key) || string.IsNullOrWhiteSpace(Pair.Value))
                                continue;
                            // 标头覆盖
                            if (request.Headers.Contains(Pair.Key)) request.Headers.Remove(Pair.Key);
                            request.Headers.Add(Pair.Key, Pair.Value);
                        }

                    using (var response = NetworkService.GetClient()
                               .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).GetAwaiter()
                               .GetResult())
                    {
                        EnsureSuccessStatusCode(response);
                        using (var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        {
                            using (var reader = new StreamReader(responseStream, Encoding.UTF8))
                            {
                                return reader.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }
        catch (ThreadInterruptedException ex)
        {
            throw;
        }
        catch (Exception ex)
        {
            var nx = ex is HttpRequestFailedException
                ? new HttpWebException("网络请求失败（" + Url + "）", (HttpRequestFailedException)ex)
                : new WebException("网络请求失败（" + Url + "）", ex);
            if (MakeLog)
                ModBase.Log(nx, "NetRequestOnce 请求失败", ModBase.LogLevel.Developer);
            throw nx;
        }
    }

    /// <summary>
    ///     是否有正在进行中、需要在任务管理页面显示的下载任务？
    /// </summary>
    public static bool HasDownloadingTask(bool IgnoreCustomDownload = false)
    {
        foreach (var Task in ModLoader.LoaderTaskbar.ToList())
            if (Task.Show && Task.State == ModBase.LoadState.Loading &&
                (!IgnoreCustomDownload || !Task.Name.Contains("自定义下载")))
                return true;

        return false;
    }

    /// <summary>
    ///     安全获取路径所在的根盘符（如 "C:\"），仅支持本地绝对路径。
    ///     若路径无效或非本地盘，返回 Nothing。
    /// </summary>
    private static string TryGetLocalDriveRoot(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;
        try
        {
            var root = Path.GetPathRoot(path);
            // 仅接受 X:\ 格式（长度为3，第二个字符是冒号）
            if (((((root?.Length is { } arg2 ? arg2 == 3 : (bool?)null) is var arg3 && arg3.HasValue && !arg3.Value
                    ?
                    false
                    : root[1] == ':'
                        ? arg3
                        : false) is var arg4 && !arg4.HasValue) || arg4.Value) && root[2] == '\\' &&
                arg4.HasValue) return root.ToUpperInvariant();
        }
        catch
        {
            // 路径非法（如包含通配符、相对路径、UNC 等）
        }

        return null;
    }

    /// <summary>
    ///     当调用 <see cref="EnsureSuccessStatusCode" /> 时，若给定响应的 <c>IsSuccessStatusCode</c> 属性不为 <c>True</c> 则抛出该异常。
    /// </summary>
    public class HttpRequestFailedException : HttpRequestException
    {
        public HttpRequestFailedException(HttpResponseMessage response, string webResponse = null) : base(
            $"HTTP 响应失败: {response.ReasonPhrase} ({(int)response.StatusCode})")
        {
            Response = response;
            StatusCode = response.StatusCode;
            ReasonPhrase = response.ReasonPhrase;
            WebResponse = webResponse;
        }

        public new HttpStatusCode StatusCode { get; }
        public string ReasonPhrase { get; private set; }

        /// <summary>
        ///     不要尝试读取 <c>Content</c> 属性的内容，它已经被 dispose 了
        /// </summary>
        public HttpResponseMessage Response { get; private set; }

        /// <summary>
        ///     站点的原始返回内容
        /// </summary>
        public string WebResponse { get; private set; }
    }

    /// <summary>
    ///     <see cref="HttpRequestFailedException" /> 的套壳，包含 <c>StatusCode</c> 属性。<br />
    ///     在此，向龙猫的石山代码致敬。
    /// </summary>
    public class HttpWebException : WebException
    {
        public HttpWebException(string message, HttpRequestFailedException ex) : base(message, ex)
        {
            InnerHttpException = ex;
        }

        public HttpRequestFailedException InnerHttpException { get; }
        public HttpStatusCode StatusCode => InnerHttpException.StatusCode;
    }

    public class ResponsedWebException : WebException
    {
        public ResponsedWebException(string Message, string Response, Exception InnerException) : base(Message,
            InnerException)
        {
            this.Response = Response;
        }

        /// <summary>
        ///     远程服务器给予的回复。
        /// </summary>
        public new string Response { get; set; }
    }

    /// <summary>
    ///     下载源。
    /// </summary>
    public class NetSource
    {
        public Exception Ex;
        public int FailCount;
        public int Id;
        public bool IsFailed;

        /// <summary>
        ///     若该下载源正在进行强制单线程下载，标记这个唯一的线程。
        /// </summary>
        public NetThread SingleThread;

        public string Url;

        public override string ToString()
        {
            return Url;
        }
    }

    /// <summary>
    ///     下载线程。
    /// </summary>
    public class NetThread : IEnumerable<NetThread>, IEquatable<NetThread>
    {
        private long _Speed;

        /// <summary>
        ///     线程已下载的文件大小。
        /// </summary>
        public long DownloadDone;

        /// <summary>
        ///     线程下载起始位置。
        /// </summary>
        public long DownloadStart;

        /// <summary>
        ///     线程初始化时的时间。
        /// </summary>
        public long InitTime = TimeUtils.GetTimeTick();

        /// <summary>
        ///     上次接受到有效数据的时间，-1 表示尚未有有效数据。
        /// </summary>
        public long LastReceiveTime = -1;

        /// <summary>
        ///     链表中的下一个线程。
        /// </summary>
        public NetThread NextThread;

        /// <summary>
        ///     当前选取的是哪一个 Url。
        /// </summary>
        public NetSource Source;

        /// <summary>
        ///     上次记速时的已下载大小。
        /// </summary>
        private long SpeedLastDone;

        /// <summary>
        ///     上次记速时的时间。
        /// </summary>
        private long SpeedLastTime = TimeUtils.GetTimeTick();

        /// <summary>
        ///     当前线程的状态。
        /// </summary>
        public NetState State = NetState.WaitingToDownload;

        /// <summary>
        ///     对应的下载任务。
        /// </summary>
        public NetFile Task;

        /// <summary>
        ///     该线程的缓存文件。
        /// </summary>
        public string Temp;

        /// <summary>
        ///     对应的线程。
        /// </summary>
        public Thread Thread;

        /// <summary>
        ///     分配给任务中每个线程（无论其是否失败）的编号。
        /// </summary>
        public int Uuid;

        private IEnumerable<NetThread> Next
        {
            get
            {
                var CurrentChain = this;
                while (CurrentChain is not null)
                {
                    yield return CurrentChain;
                    CurrentChain = CurrentChain.NextThread;
                }
            }
        }

        /// <summary>
        ///     是否为第一个线程。
        /// </summary>
        public bool IsFirstThread => DownloadStart == 0L && Task.FileSize == -2;

        /// <summary>
        ///     线程下载结束位置。
        /// </summary>
        public long DownloadEnd
        {
            get
            {
                lock (Task.LockChain)
                {
                    if (NextThread is null)
                    {
                        if (Task.IsUnknownSize) return 5 * 1024 * 1024 * 1024L; // 5G

                        return Task.FileSize - 1L;
                    }

                    return NextThread.DownloadStart - 1L;
                }
            }
        }

        /// <summary>
        ///     线程未下载的文件大小。
        /// </summary>
        public long DownloadUndone => DownloadEnd - (DownloadStart + DownloadDone) + 1L;

        /// <summary>
        ///     当前的下载速度，单位为 Byte / 秒。
        /// </summary>
        public long Speed
        {
            get
            {
                if (TimeUtils.GetTimeTick() - SpeedLastTime > 200)
                {
                    var DeltaTime = TimeUtils.GetTimeTick() - SpeedLastTime;
                    _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                    SpeedLastDone = DownloadDone;
                    SpeedLastTime += DeltaTime;
                }

                return _Speed;
            }
        }

        /// <summary>
        ///     是否已经结束。
        /// </summary>
        public bool IsEnded => State == NetState.Finished || State == NetState.Interrupted;

        public IEnumerator<NetThread> GetEnumerator()
        {
            return Next.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return IEnumerable_GetEnumerator();
        }

        // 允许进行 UUID 比较
        public bool Equals(NetThread other)
        {
            return other is not null && Uuid == other.Uuid;
        }

        private IEnumerator IEnumerable_GetEnumerator()
        {
            return Next.GetEnumerator();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NetThread);
        }

        public static bool operator ==(NetThread left, NetThread right)
        {
            return EqualityComparer<NetThread>.Default.Equals(left, right);
        }

        public static bool operator !=(NetThread left, NetThread right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    ///     下载单个文件。
    /// </summary>
    public class NetFile
    {
        /// <summary>
        ///     新建一个需要下载的文件。
        /// </summary>
        /// <param name="localPath">包含文件名的本地地址。</param>
        public NetFile(IEnumerable<string> urls, string localPath, ModBase.FileChecker checker = null,
            bool useBrowserUserAgent = false, string customUserAgent = "")
        {
            var sources = new List<NetSource>();
            var count = 0;
            urls = urls.Distinct().ToArray();
            foreach (var source in urls)
            {
                sources.Add(new NetSource
                {
                    FailCount = 0,
                    Url = Conversions.ToString(ModSecret.SecretCdnSign(source.Replace(Constants.vbCr, "")
                        .Replace(Constants.vbLf, "").Trim())),
                    Id = count, IsFailed = false, Ex = null
                });
                count += 1;
            }

            Sources = new ModBase.SafeList<NetSource>(sources);
            LocalPath = localPath;
            Check = checker;
            UseBrowserUserAgent = useBrowserUserAgent;
            CustomUserAgent = customUserAgent;
            LocalName = ModBase.GetFileNameFromPath(localPath);
        }

        /// <summary>
        ///     尝试开始一个新的下载线程。
        ///     如果失败，返回 Nothing。
        /// </summary>
        public NetThread? TryBeginThread()
        {
            try
            {
                // 1. 基础状态拦截
                if (NetTaskThreadCount >= NetTaskThreadLimit || !HasAvailableSource()) return null;

                // 小文件卡住检测与不分割逻辑
                if (IsNoSplit && Threads != null &&
                    Threads.State != NetState.Interrupted &&
                    Threads.State != NetState.WaitingToDownload &&
                    TimeUtils.GetTimeTick() - Threads.InitTime < 30000) return null;

                if (State >= NetState.Merging || State == NetState.WaitingToCheck) return null;

                lock (LockState)
                {
                    if (State < NetState.Connecting) State = NetState.Connecting;
                }

                long startPosition = 0;
                NetSource? startSource = null;
                NetThread? threadInfo = null;

                lock (LockChain)
                {
                    // 2. 核心调度算法：确定下载起点 (StartPosition) 和 源 (StartSource)
                    var shouldCapture = false;

                    if (IsNoSplit)
                    {
                        shouldCapture = true;
                    }
                    else if (!HasAvailableSource(false))
                    {
                        // 单线程模式：检查是否有点可用
                        if (SourcesOnce[0].SingleThread != null &&
                            SourcesOnce[0].SingleThread.State != NetState.Interrupted)
                            return null;
                        shouldCapture = true;
                    }

                    if (shouldCapture)
                    {
                        // 执行 Capture 逻辑：清理旧线程与缓存
                        if (IsNoSplit && SmallFileCache != null && Threads != null &&
                            Threads.State != NetState.Interrupted && Threads.State != NetState.Finished)
                            return null;

                        SmallFileCache?.Dispose();
                        SmallFileCache = null;
                        Threads = null;
                        NetManager.DownloadDone -= DownloadDone;
                        lock (LockDone)
                        {
                            DownloadDone = 0;
                        }

                        SpeedLastDone = 0;
                        State = NetState.Reading;
                    }

                    // 3. 寻找切入点
                    if (Threads == null)
                    {
                        // 情况 1: 首个线程
                        startPosition = 0;
                        startSource = GetSource(FirstThreadSource);
                        FirstThreadSource = startSource.Id + 1;
                    }
                    else
                    {
                        // 情况 2: 寻找之前失败/中断的碎片
                        foreach (var thread in Threads) // 假设 Threads 实现了 IEnumerable
                            if (thread.State == NetState.Interrupted && thread.DownloadUndone > 0)
                            {
                                startPosition = thread.DownloadStart + thread.DownloadDone;
                                startSource = GetSource(thread.Source.Id + 1);
                                break;
                            }

                        // 情况 3: 尝试开启多线程分段 (分段抢夺算法)
                        if (startSource == null)
                        {
                            var targetUrl = GetSource().Url;
                            // 过滤不支持/不建议多线程的源
                            string[] restrictedDomains =
                            {
                                "pcl2-server", "bmclapi", "github.com", "optifine.net", "modrinth", "gitcode",
                                "pysio.online", "mirrorchyan.com", "naids.com"
                            };
                            if (!AllowMuiltThread || restrictedDomains.Any(d => targetUrl.Contains(d))) return null;

                            // 寻找最大的剩余碎片进行切割
                            var filePieceMax = Threads;
                            foreach (var thread in Threads)
                                if (thread.DownloadUndone > filePieceMax.DownloadUndone)
                                    filePieceMax = thread;

                            if (filePieceMax == null || filePieceMax.DownloadUndone < FilePieceLimit) return null;

                            // 从最大碎片的后 40% 处切入
                            startPosition = (long)(filePieceMax.DownloadEnd - filePieceMax.DownloadUndone * 0.4);
                            startSource = GetSource();
                        }
                    }

                    // 4. 构建并启动线程
                    if ((startPosition > FileSize && FileSize >= 0 && !IsUnknownSize) || startPosition < 0 ||
                        startSource == null) return null;
                    if (!Tasks.Any()) return null;

                    var threadUuid = ModBase.GetUuid();
                    var th = new Thread(() => Thread(threadInfo))
                    {
                        Name = $"NetTask {Tasks[0].Uuid}/{Uuid} Download {threadUuid}#",
                        Priority = ThreadPriority.BelowNormal
                    };

                    threadInfo = new NetThread
                    {
                        Uuid = threadUuid,
                        DownloadStart = startPosition,
                        Thread = th,
                        Source = startSource,
                        Task = this,
                        State = NetState.WaitingToDownload
                    };

                    // 5. 维护下载链表
                    if (threadInfo.IsFirstThread || Threads == null)
                    {
                        Threads = threadInfo;
                    }
                    else
                    {
                        var currentChain = Threads;
                        while (currentChain.DownloadEnd <= startPosition && currentChain.NextThread != null)
                            currentChain = currentChain.NextThread;
                        threadInfo.NextThread = currentChain.NextThread;
                        currentChain.NextThread = threadInfo;
                    }

                    // 6. 更新全局计数与源占用
                    lock (NetTaskThreadCountLock)
                    {
                        NetTaskThreadCount++;
                    }

                    lock (LockSource)
                    {
                        if (!HasAvailableSource(false)) SourcesOnce[0].SingleThread = threadInfo;
                    }

                    th.Start(threadInfo);
                }

                return threadInfo;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, $"尝试开始下载线程失败（{LocalName ?? "Nothing"}）");
                return null;
            }
        }

        /// <summary>
        ///     每个下载线程执行的代码。
        /// </summary>
        private void Thread(NetThread th)
        {
            if (ModBase.ModeDebug || th.DownloadStart == 0)
                LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：开始，起始点 {th.DownloadStart}，{th.Source.Url}");

            Stream? resultStream = null;
            var timeout = Math.Min(Math.Max(ConnectAverage, 6000) * (1 + th.Source.FailCount), 25000);
            long contentLength = 0;
            th.State = NetState.Connecting;

            try
            {
                var httpDataCount = 0;
                if (SourcesOnce.Contains(th.Source) && !th.Equals(th.Source.SingleThread)) return;

                using var temp = new HttpRequestMessage(HttpMethod.Get, th.Source.Url);
                var request = temp;
                ModSecret.SecretHeadersSign(th.Source.Url, ref request, UseBrowserUserAgent, CustomUserAgent);

                if (!th.IsFirstThread || th.DownloadStart != 0)
                    request.Headers.Range = new RangeHeaderValue(th.DownloadStart, null);

                using var cts = new CancellationTokenSource();
                cts.CancelAfter(timeout);

                using var response = NetworkService.GetClient()
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .GetAwaiter().GetResult();

                EnsureSuccessStatusCode(response);
                if (State == NetState.Interrupted) return;

                var redirected = response.RequestMessage?.RequestUri;
                if (redirected != null && redirected.OriginalString != th.Source.Url)
                {
                    LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：重定向至 {redirected.OriginalString}");
                    th.Source.Url = redirected.OriginalString;
                }

                // --- 内嵌 HandleContentLength ---
                contentLength = response.Content.Headers.ContentLength.GetValueOrDefault(-1);
                if (contentLength == -1)
                {
                    if (FileSize > 1)
                    {
                        if (th.DownloadStart != 0)
                        {
                            LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：ContentLength 返回 -1，视作不支持分段");
                            lock (LockSource)
                            {
                                if (!SourcesOnce.Contains(th.Source)) SourcesOnce.Add(th.Source);
                            }

                            throw new WebException($"该下载源不支持分段下载（Range: {th.DownloadStart}）");
                        }
                    }
                    else
                    {
                        FileSize = -1;
                        IsUnknownSize = true;
                        LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：文件大小未知");
                    }
                }
                else if (contentLength < 0)
                {
                    throw new Exception("获取片大小失败，结果为 " + contentLength);
                }
                else if (th.IsFirstThread)
                {
                    // 首次线程校验文件大小
                    if (Check != null)
                    {
                        if (Check.MinSize > 0 && contentLength < Check.MinSize)
                            throw new Exception($"文件大小不足：获取到 {contentLength}，要求至少 {Check.MinSize}");
                        if (Check.ActualSize > 0 && contentLength != Check.ActualSize)
                            throw new Exception($"文件大小不一致：获取到 {contentLength}，要求必须为 {Check.ActualSize}");
                    }

                    FileSize = contentLength;
                    IsUnknownSize = false;
                    LogWrapper.Info(
                        $"[Download] {LocalName} {th.Uuid}#：文件大小 {contentLength} ({ModBase.GetString(contentLength)})");

                    // 磁盘空间校验
                    if (contentLength > 50 * 1024 * 1024)
                    {
                        var tempRoot = TryGetLocalDriveRoot(ModBase.PathTemp);
                        var localRoot = TryGetLocalDriveRoot(LocalPath);
                        if (tempRoot != null && localRoot != null)
                            foreach (var drive in DriveInfo.GetDrives())
                            {
                                if (!drive.IsReady || (drive.DriveType != DriveType.Fixed &&
                                                       drive.DriveType != DriveType.Removable)) continue;

                                long requiredSpace = 0;
                                if (string.Equals(drive.Name, tempRoot, StringComparison.OrdinalIgnoreCase))
                                    requiredSpace += (long)(contentLength * 1.1);
                                if (string.Equals(drive.Name, localRoot, StringComparison.OrdinalIgnoreCase))
                                    requiredSpace += contentLength + 5 * 1024 * 1024;

                                if (requiredSpace > 0 && drive.TotalFreeSpace < requiredSpace)
                                    throw new IOException(
                                        $"{drive.Name.TrimEnd('\\')} 盘空间不足，需要 {ModBase.GetString(requiredSpace)}。");
                            }
                    }
                }
                else if (FileSize < 0)
                {
                    throw new Exception("尚未获取文件大小");
                }
                else if (th.DownloadStart > 0 && contentLength == FileSize)
                {
                    lock (LockSource)
                    {
                        if (!SourcesOnce.Contains(th.Source)) SourcesOnce.Add(th.Source);
                    }

                    throw new WebException("该下载源不支持分段下载（返回全量大小）");
                }
                else if (FileSize - th.DownloadStart != contentLength)
                {
                    throw new WebException($"分段大小不一致：预期 {FileSize - th.DownloadStart}，实际 {contentLength}");
                }
                // --- HandleContentLength 结束 ---

                th.State = NetState.Reading;
                lock (LockState)
                {
                    if (State < NetState.Reading) State = NetState.Reading;
                }

                if (IsNoSplit)
                {
                    th.Temp = null;
                    SmallFileCache = new MemoryStream();
                    resultStream = SmallFileCache;
                }
                else
                {
                    th.Temp = Path.Combine(ModBase.PathTemp, "Download",
                        $"{Uuid}_{th.Uuid}_{RandomUtils.NextInt(0, 999999)}.tmp");
                    resultStream = new FileStream(th.Temp, FileMode.Create, FileAccess.Write, FileShare.Read);
                }

                using var httpStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                const int bufferSize = 16384;
                using var bufferOwner = MemoryPool<byte>.Shared.Rent(bufferSize);
                var dataBuffer = bufferOwner.Memory;

                httpDataCount = httpStream.Read(dataBuffer.Span);
                th.LastReceiveTime = TimeUtils.GetTimeTick();

                while ((IsUnknownSize || th.DownloadUndone > 0) && httpDataCount > 0 &&
                       !ModBase.IsProgramEnded && State < NetState.Merging && !th.Source.IsFailed)
                {
                    while (NetTaskSpeedLimitHigh > 0 && NetTaskSpeedLimitLeft <= 0) System.Threading.Thread.Sleep(8);

                    var realDataCount = IsUnknownSize ? httpDataCount : (int)Math.Min(httpDataCount, th.DownloadUndone);
                    lock (NetTaskSpeedLimitLeftLock)
                    {
                        if (NetTaskSpeedLimitHigh > 0) NetTaskSpeedLimitLeft -= realDataCount;
                    }

                    if (th.DownloadDone == 0)
                    {
                        th.State = NetState.Downloading;
                        lock (LockState)
                        {
                            if (State < NetState.Downloading) State = NetState.Downloading;
                        }

                        lock (LockCount)
                        {
                            ConnectCount++;
                            ConnectTime += TimeUtils.GetTimeTick() - th.InitTime;
                        }
                    }

                    lock (LockCount)
                    {
                        th.Source.FailCount = 0;
                        foreach (var task in Tasks) task.FailCount = 0;
                    }

                    lock (LockDone)
                    {
                        DownloadDone += realDataCount;
                    }

                    NetManager.DownloadDone += realDataCount;
                    th.DownloadDone += realDataCount;

                    resultStream.Write(dataBuffer.Span.Slice(0, realDataCount));

                    var deltaTime = TimeUtils.GetTimeTick() - th.LastReceiveTime;
                    if (deltaTime > 1500 && deltaTime > realDataCount) throw new TimeoutException("速度过慢断开连接");

                    th.LastReceiveTime = TimeUtils.GetTimeTick();
                    if (th.DownloadUndone == 0 && !IsUnknownSize) break;

                    var readStartTime = TimeUtils.GetTimeTick();
                    httpDataCount = httpStream.Read(dataBuffer.Span);
                    if (TimeUtils.GetTimeTick() - readStartTime > timeout * 0.5 && httpDataCount == 0)
                        throw new TimeoutException("读取超时");
                }

                if (State == NetState.Interrupted || th.Source.IsFailed || (th.DownloadUndone > 0 && !IsUnknownSize))
                {
                    th.State = NetState.Interrupted;
                    LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：中断");
                }
                else if (httpDataCount == 0 && th.DownloadUndone > 0 && !IsUnknownSize)
                {
                    throw new Exception($"数据不足：服务器提前关闭连接 ({th.DownloadDone}/{contentLength})");
                }
                else
                {
                    th.State = NetState.Finished;
                    if (ModBase.ModeDebug) LogWrapper.Info($"[Download] {LocalName} {th.Uuid}#：完成");
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Debug($"[Download] {LocalName}：出错，{(ex is TimeoutException ? "已超时" : ex.Message)}");
                SourceFail(th, ex, false);
            }
            finally
            {
                if (!IsNoSplit) resultStream?.Dispose();
                lock (NetTaskThreadCountLock)
                {
                    NetTaskThreadCount--;
                }

                if ((FileSize >= 0 ? DownloadDone >= FileSize : DownloadDone > 0) && State < NetState.Merging) Merge();
            }
        }

        private void SourceFail(NetThread th, Exception ex, bool isMergeFailure)
        {
            // 状态变更
            lock (LockCount)
            {
                th.Source.FailCount += 1;
                foreach (var Task in Tasks)
                    Task.FailCount += 1;
            }

            var isTimeoutString = ex.ToString().ToLower().Replace(" ", "");
            var isTimeout = isTimeoutString.Contains("由于连接方在一段时间后没有正确答复或连接的主机没有反应") || isTimeoutString.Contains("超时") ||
                            isTimeoutString.Contains("timeout") || isTimeoutString.Contains("timedout") ||
                            ex.GetType() == typeof(TimeoutException) || ex.GetType() == typeof(TaskCanceledException) ||
                            (ex.GetType() == typeof(AggregateException) &&
                             ((AggregateException)ex).InnerExceptions.Any(x =>
                                 x.GetType() == typeof(TaskCanceledException) ||
                                 x.GetType() == typeof(TimeoutException)));
            // Log("[Download] " & LocalName & " " & th.Uuid & If(isTimeout, "#：超时（" & (th. * 0.001) & "s）", "#：出错，" & ex.ToString()))
            th.State = NetState.Interrupted;
            th.Source.Ex = ex;
            // 根据情况判断，是否在多线程下禁用下载源（连续错误过多，或不支持断点续传）
            var IsRangeNotSupported = ex is RangeNotSupportedException || ex.Message.Contains("(416)");
            if (isMergeFailure || IsRangeNotSupported || ex.Message.Contains("(502)") || ex.Message.Contains("(404)") ||
                ex.Message.Contains("未能解析") || ex.Message.Contains("无返回数据") || ex.Message.Contains("空间不足") ||
                ((ex.Message.Contains("(403)") || ex.Message.Contains("(429)")) &&
                 !th.Source.Url.ContainsF("bmclapi")) ||
                (th.Source.FailCount >= ModBase.MathClamp(NetTaskThreadLimit, 5d, 30d) && DownloadDone < 1L) ||
                th.Source.FailCount > NetTaskThreadLimit + 2) // BMCLAPI 的部分源在高频率请求下会返回 403/429，所以不应因此禁用下载源
            {
                // 当一个下载源有多个线程在下载时，只选择其中一个线程进行后续处理
                var IsThisFail = false;
                lock (LockSource)
                {
                    if (!th.Source.IsFailed || th.Source.SingleThread == th)
                    {
                        IsThisFail = true;
                        th.Source.IsFailed = true;
                    }
                }

                // ……后续处理
                if (IsThisFail)
                {
                    ModBase.Log(
                        $"[Download] {LocalName}：下载源被禁用（{th.Source.Id}，Range 问题：{IsRangeNotSupported}）：{th.Source.Url}");
                    ModBase.Log(ex,
                        $"{(SourcesOnce.FirstOrDefault()?.SingleThread is null ? "" : "单线程")}下载源 {th.Source.Id} 已被禁用",
                        IsRangeNotSupported || ex.Message.Contains("(404)")
                            ? ModBase.LogLevel.Developer
                            : ModBase.LogLevel.Debug);
                    lock (LockSource)
                    {
                        SourcesOnce.Remove(th.Source);
                    }

                    if (ex.Message.Contains("空间不足"))
                    {
                        // 硬盘空间不足：强制失败
                        Fail(ex);
                    }
                    else if (HasAvailableSource() && !isMergeFailure)
                    {
                    }
                    // 当前源失败，但还有下载源：正常地继续执行
                    else if (!Retried)
                    {
                        // 合并失败或首次下载失败，未重试：将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载
                        // 若所有源均不支持 Range，也会走到这里重试
                        if (!IsRangeNotSupported)
                            ModBase.Log($"[Download] {LocalName}：文件下载失败，正在自动重试……", ModBase.LogLevel.Debug);
                        Retried = true;
                        lock (LockSource)
                        {
                            SourcesOnce.Clear();
                            foreach (var Source in Sources)
                            {
                                SourcesOnce.Add(Source);
                                Source.IsFailed = true;
                            }
                        }

                        FileSystem.Reset();
                        lock (LockState)
                        {
                            State = NetState.WaitingToDownload;
                        }
                    }
                    else if (HasAvailableSource() && isMergeFailure)
                    {
                        // 合并失败且单个源失败：继续下一个源
                        FileSystem.Reset();
                        lock (LockState)
                        {
                            State = NetState.WaitingToDownload;
                        }
                    }
                    else
                    {
                        // 失败
                        ModBase.Log($"[Download] {LocalName}：已无可用下载源，下载失败");
                        Exception ExampleEx = null;
                        lock (LockSource)
                        {
                            foreach (var Source in Sources)
                            {
                                ModBase.Log("[Download] 已禁用的下载源：" + Source.Url);
                                if (Source.Ex is not null)
                                {
                                    ExampleEx = Source.Ex;
                                    ModBase.Log(Source.Ex, "下载源禁用原因", ModBase.LogLevel.Developer);
                                }
                            }
                        }

                        Fail(ExampleEx);
                    }
                }
            }

            // 清理当前已下载的内容
            if (FileSize == -2)
                FileSystem.Reset();
        }

        /// <summary>
        ///     从 HTTP 响应头中获取文件名。
        ///     如果没有，返回 Nothing。
        /// </summary>
        private string GetFileNameFromResponse(HttpResponseMessage response)
        {
            return response.Content.Headers.ContentDisposition.FileName;
        }

        // 下载文件的最终收束事件
        /// <summary>
        ///     下载完成。合并文件。
        /// </summary>
        private void Merge()
        {
            // 1. 状态判断：确保合并逻辑只被触发一次
            lock (LockState)
            {
                if (State < NetState.Merging)
                    State = NetState.Merging;
                else
                    return;
            }

            var retryCount = 0;
            while (true)
                try
                {
                    lock (LockChain)
                    {
                        // 2. 准备目录与清理旧文件
                        if (File.Exists(LocalPath)) File.Delete(LocalPath);
                        var directory = Path.GetDirectoryName(LocalPath);
                        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                        // 3. 开始合并文件逻辑
                        if (IsNoSplit)
                        {
                            // 情况 A：从内存缓存输出（小文件）
                            if (SmallFileCache == null)
                                throw new Exception($"小文件缓存为空，无法合并文件（{LocalName}）。");

                            if (ModBase.ModeDebug)
                                LogWrapper.Info($"[Download] {LocalName}：下载结束，从缓存输出文件，长度：{SmallFileCache.Length}");

                            SmallFileCache.Seek(0, SeekOrigin.Begin);
                            using (var mergeFile = new FileStream(LocalPath, FileMode.Create, FileAccess.Write))
                            {
                                SmallFileCache.CopyTo(mergeFile);
                            }
                        }
                        else if (Threads.Count() == 1 && Threads.Temp != null)
                        {
                            // 情况 B：仅有一个分段文件，直接移动/复制
                            if (ModBase.ModeDebug) LogWrapper.Info($"[Download] {LocalName}：下载结束，仅有一个文件，无需合并");
                            File.Copy(Threads.Temp, LocalPath, true);
                        }
                        else
                        {
                            // 情况 C：多线程分段合并
                            if (ModBase.ModeDebug) LogWrapper.Info($"[Download] {LocalName}：下载结束，开始合并分段文件");
                            using (var mergeFile = new FileStream(LocalPath, FileMode.Create, FileAccess.Write))
                            {
                                foreach (var th in Threads)
                                {
                                    if (th.DownloadDone == 0 || th.Temp == null) continue;
                                    using (var fs = new FileStream(th.Temp, FileMode.Open, FileAccess.Read,
                                               FileShare.Read))
                                    {
                                        fs.CopyTo(mergeFile);
                                    }
                                }
                            }
                        }

                        // 4. 最终大小一致性校验
                        if (!IsUnknownSize && Check != null)
                        {
                            if (Check.ActualSize == -1)
                                Check.ActualSize = FileSize;
                            else if (Check.ActualSize != FileSize)
                                throw new Exception($"文件大小不一致：任务要求 {Check.ActualSize} B，网络结果 {FileSize} B");
                        }

                        // 5. 业务自定义校验 (MD5/SHA1 等)
                        var checkResult = Check?.Check(LocalPath);
                        if (checkResult != null)
                        {
                            LogWrapper.Info($"[Download] {LocalName} 文件校验失败，下载线程细节：");
                            foreach (var th in Threads)
                                LogWrapper.Info(
                                    $"[Download]     {th.Uuid}#，状态 {th.State}，完成 {th.DownloadDone}，剩余 {th.DownloadUndone}");
                            throw new Exception(checkResult);
                        }

                        // 6. 清理临时资源
                        if (IsNoSplit)
                        {
                            SmallFileCache?.Dispose();
                            SmallFileCache = null;
                        }
                        else
                        {
                            foreach (var th in Threads)
                                if (th.Temp != null && File.Exists(th.Temp))
                                    File.Delete(th.Temp);
                        }

                        Finish(); // 调用完成回调
                        return; // 合并成功，退出循环
                    }
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, $"合并文件出错（{LocalName}）");

                    if (retryCount < 3)
                    {
                        retryCount++;
                        System.Threading.Thread.Sleep(RandomUtils.NextInt(500, 1000));
                        continue; // 重新进入 while 循环尝试重试
                    }

                    Fail(ex); // 重试次数耗尽，彻底失败
                    return;
                }
        }

        /// <summary>
        ///     下载失败。
        /// </summary>
        private void Fail(Exception RaiseEx = null)
        {
            lock (LockState)
            {
                if (State >= NetState.Finished)
                    return;
                if (RaiseEx is not null)
                    Ex.Add(RaiseEx);
                // 凉凉
                State = NetState.Interrupted;
            }

            InterruptAndDelete();
            foreach (var Task in Tasks)
                Task.OnFileFail(this);
        }

        /// <summary>
        ///     下载中断。
        /// </summary>
        public void Abort(LoaderDownload CausedByTask)
        {
            // 从特定任务中移除，如果它还属于其他任务，则继续下载
            Tasks.Remove(CausedByTask);
            if (Tasks.Any())
                return;
            // 确认中断
            lock (LockState)
            {
                if (State >= NetState.Finished)
                    return;
                State = NetState.Interrupted;
            }

            InterruptAndDelete();
        }

        private void InterruptAndDelete()
        {
            // On Error Resume Next
            if (File.Exists(LocalPath))
                File.Delete(LocalPath);
            lock (NetManager.LockRemain)
            {
                NetManager.FileRemain -= 1;
                ModBase.Log($"[Download] {LocalName}：状态 {State}，剩余文件 {NetManager.FileRemain}");
            }
        }

        // 状态改变接口
        /// <summary>
        ///     将该文件设置为已下载完成。
        /// </summary>
        public void Finish(bool PrintLog = true)
        {
            lock (LockState)
            {
                if (State >= NetState.Finished)
                    return;
                State = NetState.Finished;
            }

            lock (NetManager.LockRemain)
            {
                NetManager.FileRemain -= 1;
                if (PrintLog)
                    ModBase.Log("[Download] " + LocalName + "：已完成，剩余文件 " + NetManager.FileRemain);
            }

            foreach (var Task in Tasks)
                Task.OnFileFinish(this);
        }

        #region 属性

        /// <summary>
        ///     所属的文件列表任务。
        /// </summary>
        public ModBase.SafeList<LoaderDownload> Tasks = new();

        /// <summary>
        ///     所有下载源。
        /// </summary>
        public ModBase.SafeList<NetSource> Sources;

        /// <summary>
        ///     用于在第一个线程出错时切换下载源。
        /// </summary>
        private int FirstThreadSource;

        /// <summary>
        ///     所有已经被标记为失败的，但未完整尝试过的，不允许断点续传的下载源。
        /// </summary>
        public ModBase.SafeList<NetSource> SourcesOnce = new();

        /// <summary>
        ///     仅当合并失败或首次下载失败时，会将所有下载源重新标记为不允许断点续传的下载源，逐个重新尝试下载。
        ///     这一策略可以兼容多个下载源中的一部分返回错误的文件的情况，以及部分在多线程下载时会抽风的源。
        /// </summary>
        private bool Retried;

        /// <summary>
        ///     获取从某个源开始，第一个可用的源。
        /// </summary>
        private NetSource GetSource(int Id = 0)
        {
            if (Sources.Count == 0)
                return null;
            Id = Id % Sources.Count;
            lock (LockSource)
            {
                if (HasAvailableSource(false))
                {
                    // 存在多线程可用源
                    var CurrentSource = Sources[Id];
                    while (CurrentSource.IsFailed)
                    {
                        Id += 1;
                        if (Id >= Sources.Count)
                            Id = 0;
                        CurrentSource = Sources[Id];
                    }

                    return CurrentSource;
                }

                if (SourcesOnce.Any())
                    // 仅存在单线程可用源
                    return SourcesOnce[0];

                // 没有可用源
                return null;
            }
        }

        /// <summary>
        ///     是否存在可用源。
        /// </summary>
        public bool HasAvailableSource(bool AllowOnceSource = true)
        {
            lock (LockSource)
            {
                if (Sources.Any(s => !s.IsFailed))
                    return true; // 存在多线程可用源
                if (AllowOnceSource && SourcesOnce.Any())
                    return true; // 存在单线程可用源
            }

            return false;
        }

        /// <summary>
        ///     存储在本地的带文件名的地址。
        /// </summary>
        public string LocalPath;

        /// <summary>
        ///     存储在本地的文件名。
        /// </summary>
        public string LocalName;

        /// <summary>
        ///     当前的下载状态。
        /// </summary>
        public NetState State = NetState.WaitingToCheck;

        /// <summary>
        ///     导致下载失败的原因。
        /// </summary>
        public List<Exception> Ex = new();

        /// <summary>
        ///     作为文件组成部分的线程链表。
        ///     如果没有线程，可以为 Nothing。
        /// </summary>
        public NetThread Threads;

        /// <summary>
        ///     文件的总大小。若为 -2 则为未获取，若为 -1 则为无法获取准确大小。
        /// </summary>
        public long FileSize = -2;

        /// <summary>
        ///     该文件是否无法获取准确大小。
        /// </summary>
        public bool IsUnknownSize;

        /// <summary>
        ///     该文件是否不需要分割。
        /// </summary>
        public bool IsNoSplit => IsUnknownSize || FileSize < FilePieceLimit;

        /// <summary>
        ///     为不需要分割的小文件进行临时存储。
        /// </summary>
        private MemoryStream SmallFileCache;

        /// <summary>
        ///     文件的已下载大小。
        /// </summary>
        public long DownloadDone;

        private readonly object LockDone = new();

        /// <summary>
        ///     文件的校验规则。
        /// </summary>
        public ModBase.FileChecker Check;

        /// <summary>
        ///     下载时是否添加浏览器 UA。
        /// </summary>
        public bool UseBrowserUserAgent;

        /// <summary>
        ///     是否允许多线程下载
        /// </summary>
        public bool AllowMuiltThread = true;

        /// <summary>
        ///     自定义User-Agent
        /// </summary>
        public string CustomUserAgent = "";

        /// <summary>
        ///     上次记速时的时间。
        /// </summary>
        private long SpeedLastTime = TimeUtils.GetTimeTick();

        /// <summary>
        ///     上次记速时的已下载大小。
        /// </summary>
        private long SpeedLastDone;

        /// <summary>
        ///     当前的下载速度，单位为 Byte / 秒。
        /// </summary>
        public long Speed
        {
            get
            {
                if (TimeUtils.GetTimeTick() - SpeedLastTime > 200)
                {
                    var DeltaTime = TimeUtils.GetTimeTick() - SpeedLastTime;
                    _Speed = (long)Math.Round((DownloadDone - SpeedLastDone) / (DeltaTime / 1000d));
                    SpeedLastDone = DownloadDone;
                    SpeedLastTime += DeltaTime;
                }

                return _Speed;
            }
        }

        private long _Speed;

        /// <summary>
        ///     该文件是否由本地文件直接拷贝完成。
        /// </summary>
        public bool IsCopy;

        /// <summary>
        ///     本文件的显示进度。
        /// </summary>
        public double Progress
        {
            get
            {
                switch (State)
                {
                    case NetState.WaitingToCheck:
                    {
                        return 0d;
                    }
                    case NetState.WaitingToDownload:
                    {
                        return 0.01d;
                    }
                    case NetState.Connecting:
                    {
                        return 0.02d;
                    }
                    case NetState.Reading:
                    {
                        return 0.04d;
                    }
                    case NetState.Downloading:
                    {
                        // 正在下载中，对应 5% ~ 98%
                        var OriginalProgress = IsUnknownSize ? 0.5d : DownloadDone / (double)Math.Max(FileSize, 1L);
                        OriginalProgress = 1d - Math.Pow(1d - OriginalProgress, 0.9d);
                        return OriginalProgress * 0.93d + 0.05d;
                    }
                    case NetState.Merging:
                    {
                        return 0.99d;
                    }
                    case NetState.Finished:
                    case NetState.Interrupted:
                    {
                        return 1d;
                    }

                    default:
                    {
                        return 0.5d;
                    }
                    // Throw New ArgumentOutOfRangeException("文件状态未知：" & State)
                }
            }
        }

        /// <summary>
        ///     各个线程建立连接成功的总次数。
        /// </summary>
        private int ConnectCount;

        /// <summary>
        ///     各个线程建立连接成功的总时间。
        /// </summary>
        private long ConnectTime;

        /// <summary>
        ///     各个线程建立连接成功的平均时间，单位为毫秒，-1 代表尚未有成功连接。
        /// </summary>
        private int ConnectAverage
        {
            get
            {
                lock (LockCount)
                {
                    return (int)Math.Round(ConnectCount == 0 ? -1 : ConnectTime / (double)ConnectCount);
                }
            }
        }

        private const long FilePieceLimit = 262144L;
        public readonly object LockCount = new();
        public readonly object LockState = new();
        public readonly object LockChain = new();
        public readonly object LockSource = new();

        public readonly int Uuid = ModBase.GetUuid();

        public override bool Equals(object obj)
        {
            var file = obj as NetFile;
            return file is not null && Uuid == file.Uuid;
        }

        #endregion
    }

    private class RangeNotSupportedException : WebException
    {
        public RangeNotSupportedException(string message) : base(message)
        {
        }
    }

    /// <summary>
    ///     下载一系列文件的加载器。
    /// </summary>
    public class LoaderDownload : ModLoader.LoaderBase
    {
        public LoaderDownload(string Name, List<NetFile> FileTasks)
        {
            this.Name = Name;
            Files = new ModBase.SafeList<NetFile>(FileTasks);
        }

        /// <summary>
        ///     刷新公开属性。由 NetManager 每 0.1 秒调用一次。
        /// </summary>
        public void RefreshStat()
        {
            // 计算进度
            var NewProgress = 0d;
            var TotalProgress = 0d;
            foreach (var File in Files)
                if (File.IsCopy)
                {
                    NewProgress += File.Progress * 0.2d;
                    TotalProgress += 0.2d;
                }
                else
                {
                    NewProgress += File.Progress;
                    TotalProgress += 1d;
                }

            if (TotalProgress > 0d && !double.IsNaN(TotalProgress))
                NewProgress /= TotalProgress;
            // 刷新进度
            _Progress = NewProgress;
        }

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            if (Input is not null)
                Files = new ModBase.SafeList<NetFile>((IEnumerable<NetFile>)Input);
            // 去重
            Files = new ModBase.SafeList<NetFile>(Files.Distinct((a, b) => (a.LocalPath ?? "") == (b.LocalPath ?? "")));
            // 设置剩余文件数
            lock (FileRemainLock)
            {
                FileRemain += Files.Where(f => f.State != NetState.Finished).Count();
            }

            State = ModBase.LoadState.Loading;
            // 开始执行
            // 输入检测
            // 接入任务管理器
            // ====================================
            // 已存在文件查找
            // ====================================

            // 整理允许进行查找的文件
            // 获取 MC 文件夹列表
            // 平均分配到多个检查线程
            ModBase.RunInNewThread(() =>
            {
                try
                {
                    if (!Files.Any())
                    {
                        OnFinish();
                        return;
                    }

                    foreach (var File in Files)
                    {
                        if (File is null) throw new ArgumentException("存在空文件请求！");
                        foreach (var Source in File.Sources)
                            if (!(Source.Url.StartsWithF("https://", true) || Source.Url.StartsWithF("http://", true)))
                            {
                                Source.Ex = new ArgumentException("输入的下载链接不正确！");
                                Source.IsFailed = true;
                            }

                        if (!File.HasAvailableSource()) throw new ArgumentException("输入的下载链接不正确！");
                        File.LocalPath = File.LocalPath.Replace("/", @"\");
                        if (!File.LocalPath.ToLower().Contains(@":\"))
                            throw new ArgumentException("输入的本地文件地址不正确: " + File.LocalPath);
                        if (File.LocalPath.EndsWithF(@"\"))
                            throw new ArgumentException("请输入含文件名的完整文件路径: " + File.LocalPath);
                        Directory.CreateDirectory(ModBase.GetPathFromFullPath(File.LocalPath));
                    }

                    NetManager.Start(this);
                    var FilesToCheck = new List<NetFile>();
                    var DisabledCopy = Conversions.ToBoolean(ModBase.Setup.Get("SystemDebugSkipCopy"));
                    foreach (var File in Files)
                        if (!DisabledCopy && (File.Check?.CanUseExistsFile).GetValueOrDefault())
                            FilesToCheck.Add(File);
                        else
                            lock (LockState)
                            {
                                File.State = NetState.WaitingToDownload;
                                File.IsCopy = false;
                            }

                    if (!FilesToCheck.Any()) return;
                    var Folders = new List<string>();
                    Folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\");
                    Folders.AddRange(ModMinecraft.McFolderList.Select(f => f.Location));
                    Folders = Folders.Distinct().Where(f => Directory.Exists(f)).ToList();
                    var ThreadCount = (int)Math.Round(ModBase.MathClamp(FilesToCheck.Count / 40, 1d, 8d));
                    if (ThreadCount == 1)
                    {
                        CheckExistingFiles(FilesToCheck, Folders);
                    }
                    else
                    {
                        var BaseSize = FilesToCheck.Count / ThreadCount;
                        var Remainder = FilesToCheck.Count % ThreadCount;
                        var Index = 0;
                        for (int i = 0, loopTo = ThreadCount - 1; i <= loopTo; i++)
                        {
                            var Size = BaseSize + (i < Remainder ? 1 : 0);
                            var ThreadFiles = FilesToCheck.GetRange(Index, Size);
                            Index += Size;
                            ModBase.RunInNewThread(() => CheckExistingFiles(ThreadFiles, Folders),
                                $"下载 文件复制 {Uuid}/{ModBase.GetUuid()}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnFail(new List<Exception> { new("下载初始化失败", ex) });
                }
            }, "L/下载 " + Uuid); // 创建目标文件夹
            // 在设置中禁用了复制
            // 不允许，直接开始下载
            // 总是添加官启文件夹，因为 HMCL 会把所有文件存在这里
            // 每个线程至少 40 个文件，最多 8 线程
            // 只有一个线程，直接执行
        }

        private void CheckExistingFiles(List<NetFile> Files, List<string> FolderList)
        {
            try
            {
                if (ModBase.ModeDebug)
                    ModBase.Log($"[Download] 文件检查线程已启动，分配的文件数：{Files.Count}");
                // 列出 MC 文件夹中的各个版本文件夹
                var VersionFolders = new List<string>();
                foreach (var McFolder in FolderList)
                {
                    var VersionsFolder = new DirectoryInfo(McFolder + @"versions\");
                    if (VersionsFolder.Exists)
                        foreach (var VersionFolder in VersionsFolder.GetDirectories())
                            VersionFolders.Add(VersionFolder.FullName + @"\");
                }

                // 处理每个文件
                foreach (var File in Files)
                {
                    var Target = CheckExistingFile(FolderList, VersionFolders, File);
                    if (File.State >= NetState.WaitingToDownload)
                        return; // 中断
                    if (Target is null)
                    {
                        // 未找到相同文件
                        lock (LockState)
                        {
                            File.State = NetState.WaitingToDownload;
                            File.IsCopy = false;
                        }
                    }
                    else
                    {
                        // 已找到相同文件
                        File.IsCopy = true;
                        var RetryCount = 0;
                        Retry: ;

                        try
                        {
                            if ((Target ?? "") != (File.LocalPath ?? ""))
                            {
                                ModBase.Log($"[Download] 复制已存在的文件：{Target} → {File.LocalPath}");
                                ModBase.CopyFile(Target, File.LocalPath);
                            }

                            File.Finish(false);
                        }
                        catch (Exception ex)
                        {
                            RetryCount += 1;
                            ModBase.Log(ex, $"复制已存在的文件失败，第 {RetryCount} 次重试（{Target} → {File.LocalPath}）");
                            if (RetryCount < 3)
                            {
                                Thread.Sleep(200);
                                goto Retry;
                            }

                            // 失败，回退到下载
                            lock (LockState)
                            {
                                File.State = NetState.WaitingToDownload;
                                File.IsCopy = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnFail(new List<Exception> { new("下载已存在文件查找失败", ex) });
            }
        }

        private string CheckExistingFile(List<string> FolderList, List<string> VersionFolders, NetFile File)
        {
            // 目标文件已存在
            if (File.Check.Check(File.LocalPath) is null)
                return File.LocalPath;
            // 没有可用的检查规则，只能开始下载
            if (File.Check.Hash is null && File.Check.ActualSize < 0L)
                return null;
            // 大致判断文件类别
            var TypeIndexes =
                new[]
                    {
                        @"\assets\", @"\libraries\", @"\versions\", @"\mods\", @"\coremods\", @"\lib\",
                        @"\resourcepacks\",
                        @"\texturepacks\", @"\shaderpacks\"
                    }.Select(FolderName => (FolderName, File.LocalPath.IndexOfF(FolderName, true)))
                    .Where(kv => kv.Item2 >= 0).ToList();
            if (!TypeIndexes.Any())
            {
                if (File.LocalName.EndsWithF(".jar"))
                    TypeIndexes.Add((@"\versions\", 1)); // 总是对 jar 进行版本文件检查，以包括另存为 jar 的情况
                else
                    return null;
            }

            var Type = TypeIndexes.MaxOrDefault(kv => kv.Item2).FolderName.TrimStart('\\');
            // 根据类别进行查找
            switch (Type)
            {
                case @"assets\":
                case @"libraries\":
                {
                    // assets/libraries：查找 MC 文件夹下的相同路径
                    foreach (var Folder in FolderList)
                    {
                        var Candidate = Folder + Type + File.LocalPath.AfterFirst(Type);
                        if (File.Check.Check(Candidate) is null)
                            return Candidate;
                    }

                    break;
                }
                case @"versions\":
                {
                    // 版本 jar 或 json：查找 MC 文件夹下的各个版本文件夹
                    foreach (var VersionFolder in VersionFolders)
                    foreach (var Candidate in Directory.GetFiles(VersionFolder,
                                 "*." + ModBase.GetFileNameFromPath(File.LocalPath).AfterLast(".").ToLower(),
                                 SearchOption.TopDirectoryOnly))
                        if (File.Check.Check(Candidate) is null)
                            return Candidate;

                    break;
                }

                default:
                {
                    // 社区资源
                    if (File.Check.ActualSize < 0L || File.Check.Hash is null)
                        return null; // 必须要求指定了文件大小和 Hash
                    foreach (var Folder in FolderList.Concat(VersionFolders))
                    {
                        var TargetFolder = Folder + Type;
                        if (!Directory.Exists(TargetFolder))
                            continue;
                        foreach (var Candidate in Directory.GetFiles(TargetFolder))
                        {
                            if (!_CheckExistingFile_Sizes.ContainsKey(Conversions.ToString(Candidate)))
                                _CheckExistingFile_Sizes[Conversions.ToString(Candidate)] =
                                    new FileInfo(Conversions.ToString(Candidate)).Length;
                            if (File.Check.ActualSize != _CheckExistingFile_Sizes[Conversions.ToString(Candidate)])
                                continue;
                            // Hash 校验
                            if (File.Check.Check(Conversions.ToString(Candidate)) is null)
                                return Conversions.ToString(Candidate);
                        }
                    }

                    break;
                }
            }

            return null;
        }

        public void OnFileFinish(NetFile File)
        {
            // 要求全部文件完成
            lock (FileRemainLock)
            {
                FileRemain -= 1;
                if (FileRemain > 0)
                    return;
            }

            OnFinish();
        }

        public void OnFinish()
        {
            RaisePreviewFinish();
            lock (LockState)
            {
                if (State > ModBase.LoadState.Loading)
                    return;
                State = ModBase.LoadState.Finished;
            }
        }

        public void OnFileFail(NetFile File)
        {
            // 将下载源的错误加入主错误列表
            foreach (var Source in File.Sources)
                if (!(Source.Ex == null))
                    File.Ex.Add(Source.Ex);
            OnFail(File.Ex);
        }

        public void OnFail(List<Exception> ExList)
        {
            lock (LockState)
            {
                if (State > ModBase.LoadState.Loading)
                    return;
                if (ExList is null || !ExList.Any())
                    ExList = new List<Exception> { new("未知错误！") };
                // 寻找第一个不是 404 的下载源
                var UsefulExs = ExList.Where(e => !e.Message.Contains("404 (")).ToList();
                Error = UsefulExs.Any() ? UsefulExs[0] : ExList[0];
                // 获取实际失败的文件
                foreach (var File in Files)
                    if (File.State == NetState.Interrupted)
                    {
                        Error = new Exception(
                            "文件下载失败：" + File.LocalPath + Constants.vbCrLf + File.Sources
                                .Select(s => s.Ex is null ? s.Url : s.Ex.Message + "（" + s.Url + "）")
                                .Join(Constants.vbCrLf), Error);
                        break;
                    }

                // 在设置 Error 对象后再更改为失败，避免 WaitForExit 无法捕获错误
                State = ModBase.LoadState.Failed;
            }

            // 中断所有文件
            foreach (var TaskFile in Files)
                if (TaskFile.State < NetState.Merging)
                    TaskFile.State = NetState.Interrupted;
            // 在退出同步锁后再进行日志输出
            var ErrOutput = new List<string>();
            foreach (var Ex in ExList)
                ErrOutput.Add(Ex.Message);
            ModBase.Log("[Download] " + ErrOutput.Distinct().ToArray().Join(Constants.vbCrLf));
        }

        public override void Abort()
        {
            lock (LockState)
            {
                if (State >= ModBase.LoadState.Finished)
                    return;
                State = ModBase.LoadState.Aborted;
            }

            ModBase.Log("[Download] " + Name + " 已取消！");
            // 中断所有文件
            foreach (var TaskFile in Files)
                TaskFile.Abort(this);
        }

        #region 属性

        /// <summary>
        ///     需要下载的文件。
        /// </summary>
        public ModBase.SafeList<NetFile> Files;

        /// <summary>
        ///     剩余未完成的文件数。（用于减轻 FilesLock 的占用）
        /// </summary>
        private int FileRemain;

        private readonly object FileRemainLock = new();

        /// <summary>
        ///     用于显示的百分比进度。
        /// </summary>
        public override double Progress
        {
            get
            {
                if (State >= ModBase.LoadState.Finished)
                    return 1d;
                if (!Files.Any())
                    return 0d; // 必须返回 0，否则在获取列表的时候会错觉已经下载完了
                return _Progress;
            }
            set => throw new Exception("文件下载不允许指定进度");
        }

        private double _Progress;

        /// <summary>
        ///     任务中的文件的连续失败计数。
        /// </summary>
        public int FailCount
        {
            get => _FailCount;
            set
            {
                _FailCount = value;
                if (State == ModBase.LoadState.Loading && value >= Math.Min(10000d,
                        Math.Max(FileRemain * 5.5d, NetTaskThreadLimit * 5.5d + 3d)))
                {
                    ModBase.Log("[Download] 由于同加载器中失败次数过多引发强制失败：连续失败了 " + value + " 次", ModBase.LogLevel.Debug);
                    // On Error Resume Next
                    var ExList = new List<Exception>();
                    foreach (var File in Files)
                    foreach (var Source in File.Sources)
                        if (Source.Ex is not null)
                        {
                            ExList.Add(Source.Ex);
                            if (ExList.Count > 10)
                                goto FinishExCatch;
                        }

                    FinishExCatch: ;

                    OnFail(ExList);
                }
            }
        }

        private int _FailCount;

        #endregion
    }

    /// <summary>
    ///     下载单个 UNC 文件的加载器。
    /// </summary>
    public class LoaderDownloadUnc : ModLoader.LoaderBase
    {
        /// <summary>
        ///     下载线程。
        /// </summary>
        private Thread DlThread;

        /// <summary>
        ///     保存路径。
        /// </summary>
        public string SavePath;

        /// <summary>
        ///     UNC 路径。
        /// </summary>
        public string Unc;

        public LoaderDownloadUnc(string Name, Tuple<string, string> File)
        {
            this.Name = Name;
            Unc = File.Item1;
            SavePath = File.Item2;
        }

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            if (Input is not null)
            {
                Unc = Conversions.ToString(((dynamic)Input).Item1);
                SavePath = Conversions.ToString(((dynamic)Input).Item2);
            }

            State = ModBase.LoadState.Loading;
            Directory.CreateDirectory(ModBase.GetPathFromFullPath(SavePath));
            DlThread = ModBase.RunInNewThread(DownloadThread, "Download UNC File");
        }

        private void DownloadThread()
        {
            try
            {
                var fileInfo = new FileInfo(Unc);
                var totalBytes = fileInfo.Length;
                var bytesRead = 0L;

                var tempFile = ModBase.PathTemp + Uuid + @"\" + ModBase.GetFileNameFromPath(SavePath);
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(tempFile));
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
                using (var sourceStream = new FileStream(Unc, FileMode.Open, FileAccess.Read))
                {
                    using (var destStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                    {
                        var buffer = new byte[81921]; // 80KB 缓冲区
                        int currentBytesRead;

                        do
                        {
                            currentBytesRead = sourceStream.Read(buffer, 0, buffer.Length);
                            destStream.Write(buffer, 0, currentBytesRead);
                            bytesRead += currentBytesRead;

                            Progress = bytesRead / (double)totalBytes;
                        } while (currentBytesRead > 0 && State == ModBase.LoadState.Loading);
                    }
                }

                if (State > ModBase.LoadState.Loading)
                    return;
                ModBase.CopyFile(tempFile, SavePath);
                if (State == ModBase.LoadState.Loading)
                    State = ModBase.LoadState.Finished;
            }
            catch (ThreadAbortException ex)
            {
            }
        }

        public override void Abort()
        {
            if (State >= ModBase.LoadState.Finished)
                return;
            State = ModBase.LoadState.Aborted;
            ModBase.Log("[Download] " + Name + " 已取消！");
        }
    }

    #region 刷新整体速度

    // 计算瞬时速度
    private static readonly List<long> _RefreshStat_SpeedLast = new(); // 记录至多最近 30 次下载速度的记录，较新的在前面

    // 上次记速时的已下载大小
    private static long _RefreshStat_SpeedLastDone;
    private static bool _StartManager_IsStarted = false;

    /// <summary>
    ///     下载文件管理。
    /// </summary>
    public class NetManagerClass
    {
        #region 属性

        /// <summary>
        ///     需要下载的文件。为“本地地址 - 文件对象”键值对。
        /// </summary>
        public Dictionary<string, NetFile> Files = new();

        public readonly object LockFiles = new();

        /// <summary>
        ///     当前的所有下载任务。
        /// </summary>
        public ModBase.SafeList<LoaderDownload> Tasks = new();

        /// <summary>
        ///     已下载完成的大小。
        /// </summary>
        public long DownloadDone
        {
            get => _DownloadDone;
            set
            {
                lock (LockDone)
                {
                    _DownloadDone = value;
                }
            }
        }

        private long _DownloadDone;
        private readonly object LockDone = new();


        /// <summary>
        ///     尚未完成下载的文件数。
        /// </summary>
        public int FileRemain;

        public readonly object LockRemain = new();

        // 这些属性由 RefreshStat 刷新
        /// <summary>
        ///     当前的全局下载速度，单位为 Byte / 秒。
        /// </summary>
        public long Speed;

        public readonly int Uuid = ModBase.GetUuid();

        #endregion

        /// <summary>
        ///     进度与下载速度由任务管理线程每隔约 0.1 秒刷新一次。
        /// </summary>
        private void RefreshStat()
        {
            try
            {
                var DeltaTime = TimeUtils.GetTimeTick() - RefreshStatLast;
                if (DeltaTime == 0L)
                    return;
                RefreshStatLast += DeltaTime;
                var ActualSpeed = Math.Max(0d, (DownloadDone - _RefreshStat_SpeedLastDone) / (DeltaTime / 1000d));
                _RefreshStat_SpeedLast.Insert(0, (long)Math.Round(ActualSpeed));
                if (_RefreshStat_SpeedLast.Count >= 31)
                    _RefreshStat_SpeedLast.RemoveAt(30);
                _RefreshStat_SpeedLastDone = DownloadDone;
                // 计算用于显示的速度
                var SpeedSum = 0L;
                var SpeedDiv = 0L;
                var Weight = _RefreshStat_SpeedLast.Count;
                foreach (var SpeedRecord in _RefreshStat_SpeedLast)
                {
                    SpeedSum += SpeedRecord * Weight;
                    SpeedDiv += Weight;
                    Weight -= 1;
                }

                Speed = (long)Math.Round(SpeedDiv > 0L ? SpeedSum / (double)SpeedDiv : 0d);
                // 计算新的速度下限
                var Limit = 0L;
                if (_RefreshStat_SpeedLast.Count >= 10)
                    Limit = (long)Math.Round(_RefreshStat_SpeedLast.Take(10).Average() * 0.85d); // 取近 1 秒的平均速度的 85%
                if (Limit > NetTaskSpeedLimitLow)
                {
                    NetTaskSpeedLimitLow = Limit;
                    ModBase.Log("[Download] " + "速度下限已提升到 " + ModBase.GetString(Limit));
                }

                #endregion

                #region 刷新下载任务属性

                foreach (var Task in Tasks)
                    Task.RefreshStat();
            }

            #endregion

            catch (Exception ex)
            {
                ModBase.Log(ex, "刷新下载公开属性失败");
            }
        }

        /// <summary>
        ///     启动监控线程，用于新增下载线程。
        /// </summary>
        private static bool _isManagerStarted;

        // Public FileRemainList As New List(Of String)
        private bool IsDownloadCacheCleared;
        private long RefreshStatLast;

        private void StartManager()
        {
            if (_isManagerStarted) return;
            _isManagerStarted = true;

            // 调度器逻辑封装
            Action<int> threadStarter = id =>
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(20);

                        // 1. 获取文件快照
                        List<NetFile> allFiles;
                        lock (LockFiles)
                        {
                            // 若已完成则清空列表 (仅由 ID 为 0 的线程负责)
                            if (id == 0 && FileRemain == 0 && Files.Any()) Files.Clear();
                            allFiles = Files.Values.ToList();
                        }

                        var waitingFiles = new List<NetFile>();
                        var ongoingFiles = new List<NetFile>();

                        // 2. 任务分类
                        foreach (var file in allFiles)
                        {
                            if (file.Uuid % 2 == id) continue; // 根据 UUID 奇偶性分工

                            if (file.State == NetState.WaitingToDownload)
                                waitingFiles.Add(file);
                            else if (file.State < NetState.Merging)
                                ongoingFiles.Add(file);
                        }

                        // 3. 启动等待中的任务
                        foreach (var file in waitingFiles)
                        {
                            if (NetTaskThreadCount >= NetTaskThreadLimit) break; // 最大线程数限制

                            var newThread = file.TryBeginThread();
                            // 针对 BMCLAPI 限流优化
                            if (newThread?.Source.Url.Contains("bmclapi") == true) Thread.Sleep(100);
                        }

                        // 4. 为进行中的任务追加线程（提速逻辑）
                        if (Speed >= NetTaskSpeedLimitLow) continue; // 速度够快就不管了

                        foreach (var file in ongoingFiles)
                        {
                            if (NetTaskThreadCount >= NetTaskThreadLimit) break;

                            var preparingCount = 0;
                            var downloadingCount = 0;

                            if (file.Threads != null)
                                foreach (var thread in file.Threads.ToList())
                                    if (thread.State < NetState.Downloading) preparingCount++;
                                    else if (thread.State == NetState.Downloading) downloadingCount++;

                            // 如果准备中的线程已经比下载中的多了，先等等
                            if (preparingCount > downloadingCount) continue;

                            var newThread = file.TryBeginThread();
                            if (newThread?.Source.Url.Contains("bmclapi") == true) Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, $"任务管理启动线程 {id} 出错");
                }
            };

            // 启动两个调度线程
            Basics.RunInNewThread(() => threadStarter(0), "NetManager ThreadStarter 0");
            Basics.RunInNewThread(() => threadStarter(1), "NetManager ThreadStarter 1");

            // 统计刷新线程
            Basics.RunInNewThread(() =>
            {
                try
                {
                    var nextTick = TimeUtils.GetTimeTick();
                    while (true)
                    {
                        // 刷新限速余量与公开属性
                        if (NetTaskSpeedLimitHigh > 0) NetTaskSpeedLimitLeft = NetTaskSpeedLimitHigh / 10;
                        RefreshStat();

                        // 精准定时：等待 100ms 并补偿追帧
                        nextTick += 100;
                        var sleepTime = nextTick - TimeUtils.GetTimeTick();

                        if (sleepTime > 0) Thread.Sleep((int)sleepTime);
                        else nextTick = TimeUtils.GetTimeTick(); // 已经超时，重置时间戳追帧
                    }
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "任务管理刷新线程出错");
                }
            }, "NetManager StatRefresher");
        }

        /// <summary>
        ///     开始一个下载任务。
        /// </summary>
        public void Start(LoaderDownload Task)
        {
            StartManager();
            // 清理缓存
            if (!IsDownloadCacheCleared)
            {
                try
                {
                    ModBase.DeleteDirectory(ModBase.PathTemp + "Download");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "清理下载缓存失败");
                }

                IsDownloadCacheCleared = true;
            }

            Directory.CreateDirectory(ModBase.PathTemp + "Download");
            // 文件处理
            lock (LockFiles)
            {
                // 添加每个文件
                for (int i = 0, loopTo = Task.Files.Count - 1; i <= loopTo; i++)
                {
                    var File = Task.Files[i];
                    if (Files.ContainsKey(File.LocalPath))
                    {
                        // 已有该文件
                        if (Files[File.LocalPath].State >= NetState.Finished)
                        {
                            // 该文件已经下载过一次，且下载完成
                            // 将已下载的文件替换成当前文件，重新下载
                            File.Tasks.Add(Task);
                            Files[File.LocalPath] = File;
                            lock (LockRemain)
                            {
                                FileRemain += 1;
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Download] " + File.LocalName + "：已替换列表，剩余文件 " + FileRemain);
                                // FileRemainList.Add(File.LocalPath)
                            }
                        }
                        else
                        {
                            // 该文件正在下载中
                            // 将当前文件替换成下载中的文件，即两个任务指向同一个文件
                            File = Files[File.LocalPath];
                            File.Tasks.Add(Task);
                        }
                    }
                    else
                    {
                        // 没有该文件
                        File.Tasks.Add(Task);
                        Files.Add(File.LocalPath, File);
                        lock (LockRemain)
                        {
                            FileRemain += 1;
                            if (ModBase.ModeDebug)
                                ModBase.Log("[Download] " + File.LocalName + "：已加入列表，剩余文件 " + FileRemain);
                            // FileRemainList.Add(File.LocalPath)
                        }
                    }

                    Task.Files[i] = File; // 回设
                }
            }

            Tasks.Add(Task);
        }
    }
}