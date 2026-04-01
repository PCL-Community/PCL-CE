using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Win32;
using PCL.Core.App.IoC;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Dns;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using STUN.Client;
using Sentry;

namespace PCL.Core.App.Essentials;

[LifecycleScope("telemetry", "遥测")]
[LifecycleService(LifecycleState.Running)]
public sealed partial class TelemetryService
{
    private static void _InitSentry()
    {
        Context.Info("开始初始化 Sentry SDK");
        var dsn = EnvironmentInterop.GetSecret("SENTRY_DSN");
        if (dsn is null)
        {
            Context.Warn("未找到 Sentry DSN");
            return;
        }
        
        var release = $"{Basics.VersionName}";
        
#if DEBUG
        var environment = "Debug";
#else
        var environment = "Production";
#endif
        
        SentrySdk.Init(options =>
        {
            options.Dsn = dsn;
            #if DEBUG
            options.Debug = true;
            #else
            options.Debug = false;
            #endif
            options.SendDefaultPii = false;
            options.IsGlobalModeEnabled = true;
            options.AutoSessionTracking = true;
            options.Release = release;
            options.Environment = environment;
            options.SetBeforeSend(@event =>
            {
                if (@event.Exception is TimeoutException) return null;
                return @event.Level is SentryLevel.Debug ? null : @event;
            });
        });
        
        Context.Info("Sentry SDK 初始化完成");
    }

    // 错误上报
    public static void ReportException(Exception ex, string plain, LogLevel level)
    {
        var sentryEvent = new SentryEvent(ex)
        {
            Level = level.RealLevel() switch
            {
                LogLevel.Fatal => SentryLevel.Fatal,
                LogLevel.Error => SentryLevel.Error,
                LogLevel.Warning => SentryLevel.Warning,
                LogLevel.Info => SentryLevel.Info,
                LogLevel.Debug or LogLevel.Trace => SentryLevel.Debug,
                _ => throw new ArgumentOutOfRangeException(nameof(level))
            }
        };

        if (!string.IsNullOrWhiteSpace(plain))
        {
            sentryEvent.Message = new SentryMessage { Formatted = plain };
        }
        
        SentrySdk.CaptureEvent(sentryEvent);
    }

    // 设备环境上报
    private static void _ReportDeviceEnvironment(TelemetryDeviceEnvironment content)
    {
        Context.Info("正在上报设备环境调查数据");
        
        SentrySdk.ConfigureScope(scope =>
        {
            scope.Contexts["Telemetry"] = content;
        });

        try
        {
            SentrySdk.CaptureMessage("设备环境调查");
            Context.Info("已发送设备环境调查数据");
        }
        catch(Exception ex)
        {
            Context.Error("设备环境调查数据发送失败，请检查网络连接以及使用的版本", ex);
        }
    }

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
        public required string? NatMapBehaviour { get; set; }
        public required string? NatFilterBehaviour { get; set; }
        [JsonPropertyName("IPv6Status")] public required string Ipv6Status { get; set; }
    }

    // ReSharper disable once InconsistentNaming
    private const string STUN_SERVER_ADDR = "stun.miwifi.com";

    // ReSharper restore UnusedAutoPropertyAccessor.Local
    [LifecycleStart]
    private static async Task _StartAsync()
    {
        if (!Config.System.Telemetry) return;
        _InitSentry();
        
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // stun test
        StunClient5389UDP? natTest = null;
        var miWifiIps = await DnsQuery.Instance.QueryForIpAsync(STUN_SERVER_ADDR).ConfigureAwait(false);
        try
        {
            miWifiIps ??= await Dns.GetHostAddressesAsync(STUN_SERVER_ADDR).ConfigureAwait(false);
        } catch(Exception) { /* Ignore dns error */ }
        
        if (miWifiIps != null && miWifiIps.Length != 0)
        {
            natTest = new StunClient5389UDP(new IPEndPoint(miWifiIps.First(), 3478),
                new IPEndPoint(IPAddress.Any, 0));
            await natTest.QueryAsync().ConfigureAwait(false);
        }

        var telemetry = new TelemetryDeviceEnvironment
        {
            Tag = "Telemetry",
            Id = Utils.Secret.Identify.LauncherId,
            Os = Environment.OSVersion.Version.Build,
            Is64Bit = Environment.Is64BitOperatingSystem,
            IsArm64 = RuntimeInformation.OSArchitecture.Equals(Architecture.Arm64),
            Launcher = Basics.VersionName,
            LauncherBranch = Config.Update.UpdateChannel switch
            {
                UpdateChannel.Release => "Release",
                UpdateChannel.Beta => "Beta",
                UpdateChannel.Dev => "Dev",
                _ => "Unknown"
            },
            UsedOfficialPcl =
                bool.TryParse(Registry.GetValue(@"HKEY_CURRENT_USER\Software\PCL", "SystemEula", "false") as string,
                    out var officialPcl) && officialPcl,
            UsedHmcl = Directory.Exists(Path.Combine(appDataFolder, ".hmcl")),
            UsedBakaXl = Directory.Exists(Path.Combine(appDataFolder, "BakaXL")),
            Memory = KernelInterop.GetPhysicalMemoryBytes().Total,
            NatMapBehaviour = natTest?.State.MappingBehavior.ToString(),
            NatFilterBehaviour = natTest?.State.FilteringBehavior.ToString(),
            Ipv6Status = NetworkInterfaceUtils.GetIPv6Status().ToString()
        };
        
        _ReportDeviceEnvironment(telemetry);
    }
    
    [LifecycleStop]
    private static void _StopAsync()
    {
        SentrySdk.Close();
    }
}