using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using PCL.Core.Net;
using PCL.Core.Net.Http.Client;
using PCL.Core.Utils.OS;
using STUN.Client;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.Running)]
public class TelemetryService : GeneralService
{
    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    private TelemetryService() : base("Telemetry", "遥测") { _context = Lifecycle.GetContext(this); }

    // ReSharper disable UnusedAutoPropertyAccessor.Local

    private class TelemetryDeviceEnvironment
    {
        public required string Tag { get; set; }
        public required string Id { get; set; }
        [JsonPropertyName("OS")] public required int Os { get; set; }
        public required bool Is64Bit { get; set; }
        [JsonPropertyName("IsARM64")] public required bool IsArm64 { get; set; }
        public required string Launcher { get; set; }
        public required string LauncherBranch {get; set; }
        [JsonPropertyName("UsedOfficialPCL")] public required bool UsedOfficialPcl { get; set; }
        [JsonPropertyName("UsedHMCL")] public required bool UsedHmcl { get; set; }
        [JsonPropertyName("UsedBakaXL")] public required bool UsedBakaXl { get; set; }
        public required ulong Memory { get; set; }
        public required string NatMapBehaviour { get; set; }
        public required string NatFilterBehaviour { get; set; }
        [JsonPropertyName("IPv6Status")] public required string Ipv6Status { get; set; }
    }

    // ReSharper restore UnusedAutoPropertyAccessor.Local

    public override void Start()
    {
        if (!Config.System.Telemetry) return;
        var telemetryKey = EnvironmentInterop.GetSecret("TELEMETRY_KEY");
        if (string.IsNullOrWhiteSpace(telemetryKey)) return;
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var natTest = new StunClient5389UDP(new IPEndPoint(Dns.GetHostAddresses("stun.miwifi.com").First(), 3478),
            new IPEndPoint(IPAddress.Any, 0));
        natTest.QueryAsync().GetAwaiter().GetResult();
        var telemetry = new TelemetryDeviceEnvironment
        {
            Tag = "Telemetry",
            Id = Utils.Secret.Identify.LauncherId,
            Os = Environment.OSVersion.Version.Build,
            Is64Bit = Environment.Is64BitOperatingSystem,
            IsArm64 = RuntimeInformation.OSArchitecture.Equals(Architecture.Arm64),
            Launcher = Basics.VersionName,
            LauncherBranch = Config.System.Update.UpdateChannel switch
            {
                0 => "Release",
                1 => "Beta",
                2 => "Dev",
                _ => "Unknown"
            },
            UsedOfficialPcl =
                bool.TryParse(Registry.GetValue(@"HKEY_CURRENT_USER\Software\PCL", "SystemEula", "false") as string,
                    out var officialPcl) && officialPcl,
            UsedHmcl = Directory.Exists(Path.Combine(appDataFolder, ".hmcl")),
            UsedBakaXl = Directory.Exists(Path.Combine(appDataFolder, "BakaXL")),
            Memory = KernelInterop.GetPhysicalMemoryBytes().Total,
            NatMapBehaviour = natTest.State.MappingBehavior.ToString(),
            NatFilterBehaviour = natTest.State.FilteringBehavior.ToString(),
            Ipv6Status = NetworkInterfaceUtils.GetIPv6Status().ToString()
        };
        using var response = HttpRequestBuilder
            .Create("https://pcl2ce.pysio.online/post", HttpMethod.Post)
            .WithAuthentication(telemetryKey).WithJsonContent(telemetry)
            .SendAsync().Result;
        if (response.IsSuccess)
            Context.Info("已发送设备环境调查数据");
        else
            Context.Error("设备环境调查数据发送失败，请检查网络连接以及使用的版本");
        Context.DeclareStopped();
    }
}