using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.DeviceInfo;

public static class DeviceInfoCollector
{
    private const string UnknownKey = "Common.State.Unknown";
    private const string NoneKey = "Setup.Log.Device.Value.None";

    public static DeviceInfoSnapshot Collect()
    {
        var osInfo = _GetOperatingSystemInfo();
        var machineName = _SafeGet("MachineName", () => Environment.MachineName);
        var architecture = _FormatArchitecture(RuntimeInformation.OSArchitecture);
        var cpuName = _GetCpuName();
        var memory = _GetMemoryInfo();
        var memoryText = _FormatMemory(
            memory.TotalBytes > 0
                ? memory.TotalBytes
                : _ToBytes(HardwareInfo.SystemMemorySize));
        var gpuText = _GetGpuSummary();
        var launcherVersion = _GetLauncherVersion();

        var sections = new List<DeviceInfoSection>
        {
            _BuildOperatingSystemSection(osInfo),
            _BuildDeviceSection(),
            _BuildProcessorSection(cpuName),
            _BuildMemorySection(memory),
            _BuildGraphicsSection(),
            _BuildLauncherSection(launcherVersion),
            _BuildRuntimeSection()
        };

        var summaryLines = new List<string>
        {
            Lang.Text(
                "Setup.Log.Device.Summary.DeviceLine",
                _SafeGet("LauncherVersion", () => Basics.VersionName),
                machineName),
            Lang.Text(
                "Setup.Log.Device.Summary.HardwareLine",
                cpuName,
                architecture),
            memoryText,
            gpuText
        };

        return new DeviceInfoSnapshot(
            osInfo.Name,
            summaryLines,
            launcherVersion,
            sections,
            DateTimeOffset.Now);
    }

    private static DeviceInfoSection _BuildOperatingSystemSection(OperatingSystemInfo osInfo)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.OperatingSystem",
                osInfo.Name),
            _Row(
                "Setup.Log.Device.Field.OsVersion",
                string.IsNullOrWhiteSpace(osInfo.Version)
                    ? Lang.Text(UnknownKey)
                    : osInfo.Version),
            _Row(
                "Setup.Log.Device.Field.Kernel",
                string.IsNullOrWhiteSpace(osInfo.KernelVersion)
                    ? Lang.Text(UnknownKey)
                    : $"{osInfo.KernelName} {osInfo.KernelVersion}".Trim()),
            _Row(
                "Setup.Log.Device.Field.OsDescription",
                _SafeGet("OsDescription", () => RuntimeInformation.OSDescription)),
            _Row(
                "Setup.Log.Device.Field.Architecture",
                _FormatArchitecture(RuntimeInformation.OSArchitecture)),
            _Row(
                "Setup.Log.Device.Field.Is64Bit",
                _BoolText(Environment.Is64BitOperatingSystem)),
            _Row(
                "Setup.Log.Device.Field.MachineName",
                _SafeGet("MachineName", () => Environment.MachineName)),
            _Row(
                "Setup.Log.Device.Field.SystemUptime",
                _FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64))),
            _Row(
                "Setup.Log.Device.Field.Culture",
                _SafeGet("Culture", _GetCultureName)),
            _Row(
                "Setup.Log.Device.Field.UiLanguage",
                _SafeGet("UiLanguage", _GetUiLanguage)),
            _Row(
                "Setup.Log.Device.Field.TimeZone",
                _SafeGet("TimeZone", _GetTimeZone)),
            _Row(
                "Setup.Log.Device.Field.Administrator",
                _SafeGet("Administrator", _GetAdministratorText)),
            _Row(
                "Setup.Log.Device.Field.IPv6",
                _SafeGet("IPv6", _GetIpv6StatusText))
        };

        if (!string.IsNullOrWhiteSpace(osInfo.Build))
            rows.Insert(
                2,
                _Row("Setup.Log.Device.Field.OsBuild", osInfo.Build));

        return new DeviceInfoSection(
            DeviceInfoSectionKind.OperatingSystem,
            "Setup.Log.Device.Section.OperatingSystem",
            rows);
    }

    private static DeviceInfoSection _BuildDeviceSection()
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.DeviceModel",
                _SafeGet("DeviceModel", _GetDeviceModel)),
            _Row(
                "Setup.Log.Device.Field.BiosVersion",
                _SafeGet("BiosVersion", _GetBiosVersion))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Device,
            "Setup.Log.Device.Section.Device",
            rows);
    }

    private static DeviceInfoSection _BuildProcessorSection(string cpuName)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.CpuName",
                cpuName),
            _Row(
                "Setup.Log.Device.Field.PhysicalCores",
                HardwareInfo.PhysicalCoreCount is > 0
                    ? Lang.Number(HardwareInfo.PhysicalCoreCount.Value, "N0")
                    : Lang.Text(UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.LogicalProcessors",
                _SafeGet(
                    "LogicalProcessors",
                    () => Environment.ProcessorCount.ToString(CultureInfo.CurrentCulture)))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Processor,
            "Setup.Log.Device.Section.Processor",
            rows);
    }

    private static DeviceInfoSection _BuildMemorySection(SystemMemoryInfo memory)
    {
        var total = memory.TotalBytes > 0
            ? memory.TotalBytes
            : _ToBytes(HardwareInfo.SystemMemorySize);
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.TotalMemory",
                _FormatMemory(total)),
            _Row(
                "Setup.Log.Device.Field.AvailableMemory",
                _FormatMemory(memory.AvailableBytes)),
            _Row(
                "Setup.Log.Device.Field.MemoryUsage",
                _FormatMemoryUsage(total, memory.AvailableBytes))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Memory,
            "Setup.Log.Device.Section.Memory",
            rows);
    }

    private static DeviceInfoSection _BuildGraphicsSection()
    {
        var rows = new List<DeviceInfoRow>();
        IReadOnlyList<HardwareInfo.GpuInfo> gpus;
        try
        {
            gpus = HardwareInfo.Gpus;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取图形设备信息失败");
            gpus = [];
        }

        if (gpus.Count == 0)
            rows.Add(
                _Row(
                    "Setup.Log.Device.Field.Graphics",
                    Lang.Text("Setup.Log.Device.Field.GpuNone")));
        else
            for (var i = 0; i < gpus.Count; i++)
            {
                var gpu = gpus[i];
                var name = string.IsNullOrWhiteSpace(gpu.Name)
                    ? Lang.Text(UnknownKey)
                    : gpu.Name.Trim();
                var driver = string.IsNullOrWhiteSpace(gpu.DriverVersion)
                    ? Lang.Text(UnknownKey)
                    : gpu.DriverVersion.Trim();
                var value = new StringBuilder(name);
                if (!string.IsNullOrWhiteSpace(gpu.Codename))
                    value
                        .Append('\n')
                        .Append(Lang.Text("Setup.Log.Device.Field.GpuCodename"))
                        .Append(": ")
                        .Append(gpu.Codename.Trim());
                value
                    .Append('\n')
                    .Append(Lang.Text("Setup.Log.Device.Field.GpuDriver"))
                    .Append(": ")
                    .Append(driver)
                    .Append('\n')
                    .Append(Lang.Text("Setup.Log.Device.Field.GpuMemory"))
                    .Append(": ")
                    .Append(_FormatGraphicsMemory(gpu.Memory, gpu.MemoryIsLowerBound));
                rows.Add(
                    new DeviceInfoRow(
                        "Setup.Log.Device.Field.GpuName",
                        value.ToString(),
                        [i + 1]));
            }

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Graphics,
            "Setup.Log.Device.Section.Graphics",
            rows);
    }

    private static DeviceInfoSection _BuildLauncherSection(string launcherVersion)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.LauncherVersion",
                launcherVersion),
            _Row(
                "Setup.Log.Device.Field.LauncherId",
                _SafeGet("LauncherId", () => Identify.LauncherId)),
            _Row(
                "Setup.Log.Device.Field.ExecutablePath",
                _SafeGet("ExecutablePath", () => Basics.ExecutablePath)),
            _Row(
                "Setup.Log.Device.Field.DataDirectory",
                _SafeGet("DataDirectory", () => Paths.Data)),
            _Row(
                "Setup.Log.Device.Field.LogDirectory",
                _SafeGet("LogDirectory", () => LogService.Logger.Configuration.StoreFolder)),
            _Row(
                "Setup.Log.Device.Field.PackageKind",
                _SafeGet("PackageKind", _GetPackageKind)),
            _Row(
                "Setup.Log.Device.Field.Uptime",
                _SafeGet("Uptime", _GetUptime)),
            _Row(
                "Setup.Log.Device.Field.ProcessMemory",
                _SafeGet("ProcessMemory", _GetProcessMemory)),
            _Row(
                "Setup.Log.Device.Field.CommandLine",
                _GetCommandLine())
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Launcher,
            "Setup.Log.Device.Section.Launcher",
            rows);
    }

    private static DeviceInfoSection _BuildRuntimeSection()
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.DotNetRuntime",
                _SafeGet("DotNetRuntime", () => RuntimeInformation.FrameworkDescription)),
            _Row(
                "Setup.Log.Device.Field.ClrVersion",
                _SafeGet("ClrVersion", () => Environment.Version.ToString())),
            _Row(
                "Setup.Log.Device.Field.ProcessArchitecture",
                _FormatArchitecture(RuntimeInformation.ProcessArchitecture)),
            _Row(
                "Setup.Log.Device.Field.CodePage",
                _SafeGet("CodePage", _GetCodePage))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Runtime,
            "Setup.Log.Device.Section.Runtime",
            rows);
    }

    private static DeviceInfoRow _Row(string labelKey, string value, params object[] args)
    {
        return new DeviceInfoRow(labelKey, value, args.Length == 0 ? null : args);
    }

    private static string _SafeGet(string name, Func<string?> getter)
    {
        try
        {
            var value = getter();
            return string.IsNullOrWhiteSpace(value) ? Lang.Text(UnknownKey) : value.Trim();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", $"读取 {name} 失败");
            return Lang.Text(UnknownKey);
        }
    }

    private static OperatingSystemInfo _GetOperatingSystemInfo()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetOperatingSystemInfo();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取系统信息失败");
            return new OperatingSystemInfo(
                _RuntimeExceptionFallback(),
                null,
                null,
                "Unknown",
                Environment.OSVersion.Version.ToString());
        }
    }

    private static string _RuntimeExceptionFallback()
    {
        return RuntimeInformation.OSDescription;
    }

    private static string? _GetDeviceModel()
    {
        return ApplicationRuntime.Current.Platform.SystemFacts.GetDeviceModel();
    }

    private static string? _GetBiosVersion()
    {
        return ApplicationRuntime.Current.Platform.SystemFacts.GetBiosVersion();
    }

    private static string _GetCpuName()
    {
        var name = HardwareInfo.CpuName;
        return string.IsNullOrWhiteSpace(name)
            || string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase)
                ? Lang.Text(UnknownKey)
                : name.Trim();
    }

    private static SystemMemoryInfo _GetMemoryInfo()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetMemoryInfo();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取内存信息失败");
            return default;
        }
    }

    private static string _GetGpuSummary()
    {
        IReadOnlyList<HardwareInfo.GpuInfo> gpus;
        try
        {
            gpus = HardwareInfo.Gpus;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取图形设备信息失败");
            return Lang.Text("Setup.Log.Device.Field.GpuNone");
        }

        if (gpus.Count == 0)
            return Lang.Text("Setup.Log.Device.Field.GpuNone");

        var name = string.IsNullOrWhiteSpace(gpus[0].Name)
            ? Lang.Text(UnknownKey)
            : gpus[0].Name.Trim();
        return gpus.Count == 1 ? name : $"{name} +{gpus.Count - 1}";
    }

    private static string _GetLauncherVersion()
    {
        try
        {
            return Basics.Metadata.Version.ToString();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取启动器版本失败");
            return Lang.Text(UnknownKey);
        }
    }

    private static string _GetCultureName()
    {
        return CultureInfo.CurrentCulture.Name;
    }

    private static string _GetUiLanguage()
    {
        var language = LocalizationService.CurrentLanguage;
        return $"{language.NativeName} ({language.Code})";
    }

    private static string _GetTimeZone()
    {
        var timeZone = TimeZoneInfo.Local;
        var offset = timeZone.GetUtcOffset(DateTime.Now);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"{timeZone.Id} (UTC{sign}{absolute.Hours:00}:{absolute.Minutes:00})";
    }

    private static string _GetAdministratorText()
    {
        return _BoolText(ApplicationRuntime.Current.Platform.SystemFacts.IsAdministrator);
    }

    private static string _GetIpv6StatusText()
    {
        return NetworkInterfaceUtils.GetIPv6Status() switch
        {
            NetworkInterfaceUtils.IPv6Status.Public => Lang.Text(
                "Setup.Log.Device.Value.IPv6.Public"),
            NetworkInterfaceUtils.IPv6Status.RFC4193 => Lang.Text(
                "Setup.Log.Device.Value.IPv6.Rfc4193"),
            NetworkInterfaceUtils.IPv6Status.Unavailable => Lang.Text(
                "Setup.Log.Device.Value.IPv6.Unavailable"),
            _ => Lang.Text(UnknownKey)
        };
    }

    private static string _GetPackageKind()
    {
        return string.IsNullOrWhiteSpace(Paths.PackageKind)
            ? Lang.Text("Setup.Log.Device.Field.PackageKindPortable")
            : Paths.PackageKind;
    }

    private static string _GetUptime()
    {
        return _FormatUptime(DateTime.Now - Basics.CurrentProcess.StartTime);
    }

    private static string _GetProcessMemory()
    {
        var workingSet = Basics.CurrentProcess.WorkingSet64;
        return workingSet <= 0
            ? Lang.Text(UnknownKey)
            : _FormatMemory((ulong)workingSet);
    }

    private static string _GetCommandLine()
    {
        var arguments = Basics.CommandLineArguments;
        return arguments.Length == 0
            ? Lang.Text(NoneKey)
            : string.Join('\n', arguments);
    }

    private static string _GetCodePage()
    {
        var encoding = Encoding.GetEncoding(0);
        return $"{encoding.EncodingName} ({encoding.CodePage})";
    }

    private static string _BoolText(bool value)
    {
        return Lang.Text(value ? "Common.Option.Yes" : "Common.Option.No");
    }

    private static string _FormatArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "ARM",
            Architecture.Arm64 => "ARM64",
            Architecture.Wasm => "Wasm",
            _ => architecture.ToString()
        };
    }

    private static string _FormatMemory(ulong bytes)
    {
        if (bytes == 0UL)
            return Lang.Text(UnknownKey);

        var mib = bytes / 1024d / 1024d;
        return $"{Lang.Number(mib / 1024d, "N2")} GiB ({Lang.Number(mib, "N0")} MiB)";
    }

    private static string _FormatMemoryUsage(ulong total, ulong available)
    {
        if (total == 0UL)
            return Lang.Text(UnknownKey);

        var used = total > available ? total - available : 0UL;
        var percent = used * 100d / total;
        return $"{Lang.Number(percent, "N1")}%";
    }

    private static string _FormatGraphicsMemory(long memory, bool isLowerBound)
    {
        if (memory <= 0L)
            return Lang.Text(UnknownKey);

        var mib = $"{Lang.Number(memory, "N0")} MiB";
        var text = memory >= 1024L
            ? $"{Lang.Number(memory / 1024d, "N2")} GiB ({mib})"
            : mib;
        return isLowerBound ? $">= {text}" : text;
    }

    private static string _FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;
        return uptime.TotalDays >= 1d
            ? uptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
            : uptime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private static ulong _ToBytes(long mib)
    {
        return mib <= 0L ? 0UL : (ulong)mib * 1024UL * 1024UL;
    }
}