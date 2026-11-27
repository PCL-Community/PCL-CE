using System;
using System.Collections.Generic;

namespace PCL.Core.App.DeviceInfo;

public enum DeviceInfoSectionKind
{
    OperatingSystem,
    Device,
    Processor,
    Memory,
    Graphics,
    Display,
    Storage,
    Launcher,
    Runtime
}

public sealed record DeviceInfoRow(
    string LabelKey,
    string Value,
    object[]? LabelArgs = null);

public sealed record DeviceInfoSection(
    DeviceInfoSectionKind Kind,
    string TitleKey,
    IReadOnlyList<DeviceInfoRow> Rows);

public sealed record DeviceInfoSnapshot(
    string Title,
    IReadOnlyList<string> SummaryLines,
    string LauncherVersion,
    IReadOnlyList<DeviceInfoSection> Sections,
    DateTimeOffset CapturedAt);