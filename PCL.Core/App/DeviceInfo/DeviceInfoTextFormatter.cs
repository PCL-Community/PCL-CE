using System;
using System.Text;
using PCL.Core.App.Localization;

namespace PCL.Core.App.DeviceInfo;

public static class DeviceInfoTextFormatter
{
    public static string Format(DeviceInfoSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var builder = new StringBuilder();
        builder.AppendLine(Lang.Text("Setup.Log.Device.Text.Header"));
        builder.AppendLine(
            Lang.Text(
                "Setup.Log.Device.Text.CapturedAt",
                Lang.Date(snapshot.CapturedAt)));
        builder.AppendLine(
            Lang.Text("Setup.Log.Device.Text.LauncherVersion", snapshot.LauncherVersion));
        builder.AppendLine();
        builder.AppendLine($"[{Lang.Text("Setup.Log.Device.Section.Overview")}]");
        _AppendLineIfNotEmpty(builder, snapshot.Title);
        foreach (var line in snapshot.SummaryLines)
            _AppendLineIfNotEmpty(builder, line);

        foreach (var section in snapshot.Sections)
        {
            builder.AppendLine();
            builder.AppendLine($"[{Lang.Text(section.TitleKey)}]");
            foreach (var row in section.Rows)
                _AppendRow(builder, row);
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void _AppendRow(StringBuilder builder, DeviceInfoRow row)
    {
        var label = row.LabelArgs is { Length: > 0 }
            ? Lang.Text(row.LabelKey, row.LabelArgs)
            : Lang.Text(row.LabelKey);
        var lines = row
            .Value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        builder.Append(label).Append(": ").AppendLine(lines[0]);
        for (var i = 1; i < lines.Length; i++)
            builder.Append("    ").AppendLine(lines[i]);
    }

    private static void _AppendLineIfNotEmpty(StringBuilder builder, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine(value);
    }
}