using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App.Essentials.Announcement.Models;
using PCL.Core.App.IoC;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Logging;
using PCL.Core.UI;

namespace PCL.Core.App.Essentials.Announcement;

[LifecycleScope("announcement","公告")]
[LifecycleService(LifecycleState.Running)]
public partial class AnnouncementService
{
    
    private static readonly string[] _AllowScheme = ["http", "https", "minecraft" ];
    private static readonly string[] _AnnouncementServerList = Secrets.AnnouncementServerList;

    private static List<string> _ignored =
        JsonSerializer.Deserialize<string[]>(Config.System.HiddenAnnouncement)?.ToList() ?? [];
    
    [LifecycleStart]
    private static async Task _Start()
    {
        // 可能会出现公告服务比配置服务晚关闭的情况
        Lifecycle.StateChanged += state =>
        {
            if (state == LifecycleState.Closing) Config.System.HiddenAnnouncement = JsonSerializer.Serialize(_ignored);
        };
        try
        {
            foreach (var source in _AnnouncementServerList)
            {
                var response = await HttpRequest.GetJsonAsync<List<AnnouncementDetails>>(source)
                    .ConfigureAwait(false);
                if (response is null) continue;
                
                // 对忽略的公告进行检查1，以确保仍然处于公告列表内
                
                var invalid = _ignored.Except(response.Select(a => a.Id)).ToList();
                _ignored.RemoveAll(invalid.Contains);
                
                var announcements = response.OrderBy(a => a.Priority).Where(a =>
                {
                    var isNotAfterValid = DateTimeOffset.TryParse(a.SkipOn.NotAfter, out var notAfter);
                    var isNotBeforeValid = DateTimeOffset.TryParse(a.SkipOn.NotBefore, out var notBefore);
                    var localTime = DateTimeOffset.Now;
                    if (isNotAfterValid && localTime > notAfter) return false;
                    if (isNotBeforeValid && localTime < notBefore) return false;
                    var currentVersion = new Version(Basics.VersionName.Split("-")[0]);
                    var max = new Version(a.SkipOn.MaxVersion ?? "999.999.999");
                    var min = new Version(a.SkipOn.MinVersion ?? "0.0.0");
                    
                    // [min,max]
                    return currentVersion >= min && currentVersion <= max;

                });
                foreach (var detail in announcements)
                {
                    Context.Debug(MsgBoxWrapper.ShowWithCustomButtons(
                        detail.Details, $"{detail.Title} ({detail.ReleaseDate})", _GetSelectTheme(detail.Level),
                        false,
                        detail.Buttons.Select(operation => new MsgBoxButtonInfo(operation.ButtonText,
                            OnClick: _GetSelectCallback(operation.Operation, operation.Argument))).ToArray()).ToString());
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Context.Error("加载公告失败", ex, ActionLevel.HintErr);
        }
    }
    
    private static Action _GetSelectCallback(string operation, string arguments) => operation switch
    {
        "OpenWebSite" => () =>
        {
            if (arguments.Length == 0) throw new ArgumentException("Uri is missing");
            if (_AllowScheme.All(s => new Uri(arguments).Scheme != s))
                throw new InvalidOperationException("This uri contains a unsupported scheme.");
            Process.Start(new ProcessStartInfo(arguments){ UseShellExecute = true });

        },
        "StopShow" => () =>
        {
            _ignored.Add(arguments);
        },
        _ => static () => { }
    };

    private static MsgBoxTheme _GetSelectTheme(AnnouncementLevel level) => level switch
    {
        AnnouncementLevel.Medium => MsgBoxTheme.Warning,
        AnnouncementLevel.Highest => MsgBoxTheme.Error,
        _ => MsgBoxTheme.Info
    };
}