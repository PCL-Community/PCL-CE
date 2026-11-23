using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.OS.DeviceInfo;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.DeviceInfo;

public static class DeviceInfoSectionBuilder
{
    private const string UnknownKey = "Common.State.Unknown";
    private const string NoneKey = "Setup.Log.Device.Value.None";

    public static DeviceInfoSnapshot Build(
        DeviceInfoFacts facts,
        DeviceInfoSection? displaySection = null)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var launcherVersion = _GetLauncherVersion();
        var cpuName = _Text(facts.CpuName, UnknownKey);
        var gpuText = _GetGpuSummary(facts.Gpus);

        var sections = new List<DeviceInfoSection>
        {
            _BuildOperatingSystemSection(facts),
            _BuildDeviceSection(facts),
            _BuildProcessorSection(facts, cpuName),
            _BuildMemorySection(facts),
            _BuildGraphicsSection(facts.Gpus),
            _BuildStorageSection(facts),
            _BuildLauncherSection(launcherVersion),
            _BuildRuntimeSection(facts)
        };

        if (displaySection is not null)
            sections.Add(displaySection);

        sections.Sort((left, right) => left.Kind.CompareTo(right.Kind));

        var summaryLines = new List<string>
        {
            Lang.Text(
                "Setup.Log.Device.Summary.DeviceLine",
                _SafeGet("LauncherVersion", () => Basics.VersionName),
                _Text(facts.MachineName, UnknownKey)),
            Lang.Text(
                "Setup.Log.Device.Summary.HardwareLine",
                cpuName,
                DeviceInfoFormat.FormatArchitecture(facts.OsArchitecture)),
            _FormatMemory(facts.TotalMemoryBytes),
            gpuText
        };

        return new DeviceInfoSnapshot(
            _Text(facts.OperatingSystem.Name, UnknownKey),
            summaryLines,
            launcherVersion,
            sections,
            facts.CapturedAt);
    }

    private static DeviceInfoSection _BuildOperatingSystemSection(DeviceInfoFacts facts)
    {
        var osInfo = facts.OperatingSystem;
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.OperatingSystem",
                _Text(osInfo.Name, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.OsVersion",
                _Text(osInfo.Version, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.Kernel",
                string.IsNullOrWhiteSpace(osInfo.KernelVersion)
                    ? Lang.Text(UnknownKey)
                    : $"{osInfo.KernelName} {osInfo.KernelVersion}".Trim()),
            _Row(
                "Setup.Log.Device.Field.OsDescription",
                _Text(facts.OsDescription, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.Architecture",
                DeviceInfoFormat.FormatArchitecture(facts.OsArchitecture)),
            _Row(
                "Setup.Log.Device.Field.Is64Bit",
                _BoolText(facts.Is64BitOperatingSystem)),
            _Row(
                "Setup.Log.Device.Field.MachineName",
                _Text(facts.MachineName, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.SystemUptime",
                DeviceInfoFormat.FormatUptime(facts.SystemUptime)),
            _Row(
                "Setup.Log.Device.Field.Culture",
                _Text(facts.CultureName, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.UiLanguage",
                _SafeGet("UiLanguage", _GetUiLanguage)),
            _Row(
                "Setup.Log.Device.Field.TimeZone",
                DeviceInfoFormat.FormatTimeZone(facts.TimeZoneId, facts.UtcOffset)),
            _Row(
                "Setup.Log.Device.Field.Administrator",
                _BoolText(facts.IsAdministrator)),
            _Row(
                "Setup.Log.Device.Field.IPv6",
                _GetIpv6StatusText(facts.IPv6Status))
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

    private static DeviceInfoSection _BuildDeviceSection(DeviceInfoFacts facts)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.DeviceModel",
                _Text(facts.DeviceModel, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.BiosVersion",
                _Text(facts.BiosVersion, UnknownKey))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Device,
            "Setup.Log.Device.Section.Device",
            rows);
    }

    private static DeviceInfoSection _BuildProcessorSection(
        DeviceInfoFacts facts,
        string cpuName)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.CpuName",
                cpuName),
            _Row(
                "Setup.Log.Device.Field.PhysicalCores",
                facts.PhysicalCoreCount is > 0
                    ? Lang.Number(facts.PhysicalCoreCount.Value, "N0")
                    : Lang.Text(UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.LogicalProcessors",
                facts.LogicalProcessorCount > 0
                    ? facts.LogicalProcessorCount.ToString(CultureInfo.CurrentCulture)
                    : Lang.Text(UnknownKey))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Processor,
            "Setup.Log.Device.Section.Processor",
            rows);
    }

    private static DeviceInfoSection _BuildMemorySection(DeviceInfoFacts facts)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.TotalMemory",
                _FormatMemory(facts.TotalMemoryBytes)),
            _Row(
                "Setup.Log.Device.Field.AvailableMemory",
                _FormatMemory(facts.AvailableMemoryBytes)),
            _Row(
                "Setup.Log.Device.Field.MemoryUsage",
                _FormatMemoryUsage(
                    facts.TotalMemoryBytes,
                    facts.AvailableMemoryBytes))
        };

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Memory,
            "Setup.Log.Device.Section.Memory",
            rows);
    }

    private static DeviceInfoSection _BuildGraphicsSection(
        IReadOnlyList<HardwareInfo.GpuInfo> gpus)
    {
        var rows = new List<DeviceInfoRow>();

        if (gpus.Count == 0)
            rows.Add(
                _Row(
                    "Setup.Log.Device.Field.Graphics",
                    Lang.Text("Setup.Log.Device.Field.GpuNone")));
        else
            for (var i = 0; i < gpus.Count; i++)
            {
                var gpu = gpus[i];
                var name = _Text(gpu.Name, UnknownKey);
                var driver = _Text(gpu.DriverVersion, UnknownKey);
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

    private static DeviceInfoSection _BuildStorageSection(DeviceInfoFacts facts)
    {
        var rows = new List<DeviceInfoRow>
        {
            new(
                "Setup.Log.Device.Field.DataDisk",
                _FormatDiskSpace(facts.DataVolume))
        };

        if (string.IsNullOrWhiteSpace(facts.GamePath))
        {
            rows.Add(
                new DeviceInfoRow(
                    "Setup.Log.Device.Field.GameDirectory",
                    Lang.Text(UnknownKey)));
        }
        else
        {
            rows.Add(
                new DeviceInfoRow(
                    "Setup.Log.Device.Field.GameDirectory",
                    facts.GamePath));
            rows.Add(
                new DeviceInfoRow(
                    "Setup.Log.Device.Field.GameDisk",
                    _FormatDiskSpace(facts.GameVolume)));
        }

        return new DeviceInfoSection(
            DeviceInfoSectionKind.Storage,
            "Setup.Log.Device.Section.Storage",
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

    private static DeviceInfoSection _BuildRuntimeSection(DeviceInfoFacts facts)
    {
        var rows = new List<DeviceInfoRow>
        {
            _Row(
                "Setup.Log.Device.Field.DotNetRuntime",
                _Text(facts.FrameworkDescription, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.ClrVersion",
                _Text(facts.ClrVersion, UnknownKey)),
            _Row(
                "Setup.Log.Device.Field.ProcessArchitecture",
                DeviceInfoFormat.FormatArchitecture(facts.ProcessArchitecture)),
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

    private static string _Text(string? value, string fallbackKey)
    {
        return string.IsNullOrWhiteSpace(value) ? Lang.Text(fallbackKey) : value.Trim();
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

    private static string _GetGpuSummary(IReadOnlyList<HardwareInfo.GpuInfo> gpus)
    {
        if (gpus.Count == 0)
            return Lang.Text("Setup.Log.Device.Field.GpuNone");

        var name = _Text(gpus[0].Name, UnknownKey);
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

    private static string _GetUiLanguage()
    {
        var language = LocalizationService.CurrentLanguage;
        return $"{language.NativeName} ({language.Code})";
    }

    private static string _GetIpv6StatusText(NetworkInterfaceUtils.IPv6Status status)
    {
        return status switch
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
        return DeviceInfoFormat.FormatUptime(DateTime.Now - Basics.CurrentProcess.StartTime);
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

    private static string _FormatDiskSpace(DiskSpaceInfo.Volume? volume)
    {
        if (volume is null)
            return Lang.Text(UnknownKey);

        var free = volume.FreeBytes / 1024d / 1024d / 1024d;
        var total = volume.TotalBytes / 1024d / 1024d / 1024d;
        return Lang.Text(
            "Setup.Log.Device.Value.DiskFree",
            Lang.Number(free, "N1"),
            Lang.Number(total, "N1"));
    }
}