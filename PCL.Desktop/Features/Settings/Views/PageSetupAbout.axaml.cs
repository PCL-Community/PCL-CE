// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupAbout : MyPageRight, ISettingsPageInteractionSource
{
    private int _logoClickCount;

    public PageSetupAbout()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = PanBack;
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    private void ImgPCLCommunity_Click(object? sender, PointerPressedEventArgs e)
    {
        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("PCL N Edition", "这是由社区维护的 PCL N Edition。"));
    }

    private void ImgPCLLogo_Click(object? sender, PointerPressedEventArgs e)
    {
        _logoClickCount++;
        if (_logoClickCount == 5)
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("还挺执着", "你发现了一个还在迁移中的小彩蛋。"));
    }

    private void BtnSponsorOriginal_Click(object? sender, EventArgs e)
    {
        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "赞助说明",
                "PCL N 保留对上游作者与社区贡献者的致谢。具体赞助入口请以对应上游项目页面为准。"));
    }

    private void BtnCommunityHome_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://github.com/MuXue1230-owo/PCL-N"));
    }

    private void BtnSourceCode_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://github.com/MuXue1230-owo/PCL-N"));
    }

    private void BtnSponsorMirror_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://bmclapidoc.bangbang93.com/"));
    }

    private void BtnMcmod_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://www.mcmod.cn/"));
    }

    private void BtnUpstreamLicense_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://github.com/Meloong-Git/PCL/blob/main/LICENCE"));
    }

    private void BtnUpstreamSource_Click(object? sender, EventArgs e)
    {
        OpenUrlRequested?.Invoke(this, new SettingsUrlRequestedEventArgs("https://github.com/PCL-Community/PCL-CE"));
    }
}
