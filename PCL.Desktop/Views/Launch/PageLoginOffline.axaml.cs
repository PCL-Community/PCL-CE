// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public sealed record OfflineProfileCreateRequest(
    string Username,
    string Uuid,
    string SkinSourceUuid,
    string SkinSourceName);

public partial class PageLoginOffline : Grid, PageLaunchLeft.ILoginPage
{
    private static readonly Regex UsernameRegex = new("^[A-Za-z0-9_]{3,16}$", RegexOptions.Compiled);
    private static readonly Regex UuidRegex = new("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    private readonly List<LoginProfileInfo> _skinSources = [];

    public PageLoginOffline()
    {
        AvaloniaXamlLoader.Load(this);
        Reload();
    }

    public event EventHandler? BackRequested;

    public event EventHandler<OfflineProfileCreateRequest>? ProfileCreateRequested;

    public event EventHandler<string>? ValidationFailed;

    public void SetSkinSources(IEnumerable<LoginProfileInfo> profiles)
    {
        _skinSources.Clear();
        _skinSources.AddRange(profiles.Where(static profile => profile.Kind == LaunchLoginProfileKind.Microsoft));
        Reload();
    }

    public void Reload()
    {
        if (this.FindControl<MyComboBox>("ComboSkinSource") is { } combo)
        {
            combo.Items.Clear();
            combo.Items.Add(new MyComboBoxItem { Content = "不借用", Tag = string.Empty });
            foreach (LoginProfileInfo source in _skinSources)
                combo.Items.Add(new MyComboBoxItem { Content = source.Username, Tag = source.Uuid });
            combo.SelectedIndex = 0;
        }

        RadioUuidChecked(this, new RadioBoxChangedEventArgs(user: false));
    }

    private void BtnBackClick(object? sender, EventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void RadioUuidChecked(object? sender, RadioBoxChangedEventArgs e)
    {
        bool custom = this.FindControl<MyRadioBox>("RadioUuidCustom")?.Checked == true;
        if (this.FindControl<Control>("TextUuidTitle") is { } title)
            title.IsVisible = custom;
        if (this.FindControl<Control>("TextUuid") is { } uuid)
            uuid.IsVisible = custom;
    }

    private void BtnLoginClick(object? sender, EventArgs e)
    {
        string username = this.FindControl<MyTextBox>("TextName")?.Text?.Trim() ?? string.Empty;
        if (!UsernameRegex.IsMatch(username))
        {
            ValidationFailed?.Invoke(this, "玩家 ID 应为 3-16 位字母、数字或下划线。");
            return;
        }

        string uuid;
        if (this.FindControl<MyRadioBox>("RadioUuidCustom")?.Checked == true)
        {
            uuid = (this.FindControl<MyTextBox>("TextUuid")?.Text ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal);
            if (!UuidRegex.IsMatch(uuid))
            {
                ValidationFailed?.Invoke(this, "自定义 UUID 应为 32 位十六进制字符。");
                return;
            }
        }
        else
        {
            bool legacy = this.FindControl<MyRadioBox>("RadioUuidLegacy")?.Checked == true;
            uuid = CreateOfflineUuid(username, legacy);
        }

        string skinSourceUuid = string.Empty;
        string skinSourceName = string.Empty;
        if (this.FindControl<MyComboBox>("ComboSkinSource")?.SelectedItem is MyComboBoxItem item)
        {
            skinSourceUuid = item.Tag?.ToString() ?? string.Empty;
            skinSourceName = item.Content?.ToString() ?? string.Empty;
        }

        ProfileCreateRequested?.Invoke(
            this,
            new OfflineProfileCreateRequest(username, uuid, skinSourceUuid, skinSourceName));
    }

    private static string CreateOfflineUuid(string username, bool legacy)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(legacy ? username : "OfflinePlayer:" + username);
#pragma warning disable CA5351
        // Minecraft's offline UUID format is defined as an MD5 name-based UUID.
        byte[] hash = MD5.HashData(bytes);
#pragma warning restore CA5351
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
        return new Guid(hash).ToString("N");
    }
}
