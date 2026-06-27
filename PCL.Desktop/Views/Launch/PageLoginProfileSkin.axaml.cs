// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public partial class PageLoginProfileSkin : Grid, PageLaunchLeft.ILoginPage
{
    public PageLoginProfileSkin()
    {
        AvaloniaXamlLoader.Load(this);
        AttachedToVisualTree += (_, _) => Reload();
    }

    public LoginProfileInfo? Profile { get; private set; }

    public event EventHandler? ChangeProfileRequested;

    public event EventHandler? ChangeSkinRequested;

    public event EventHandler? SaveSkinRequested;

    public event EventHandler? RefreshSkinRequested;

    public event EventHandler? ChangeCapeRequested;

    public event EventHandler? EditPasswordRequested;

    public event EventHandler? EditNameRequested;

    public void SetProfile(LoginProfileInfo profile)
    {
        Profile = profile;
        Reload();
    }

    public void Reload()
    {
        if (Profile is null)
            return;

        if (this.FindControl<TextBlock>("TextName") is { } name)
            name.Text = Profile.Username;
        if (this.FindControl<TextBlock>("TextType") is { } type)
            type.Text = Profile.Info;
        if (this.FindControl<MySkin>("Skin") is { } skin)
        {
            skin.Clear();
            skin.HasCape = Profile.Kind != LaunchLoginProfileKind.Offline;
            skin.Address = Profile.SkinAddress ?? string.Empty;
        }
        if (this.FindControl<MyIconButton>("BtnEdit") is { } edit)
            edit.IsVisible = Profile.Kind != LaunchLoginProfileKind.Offline;
    }

    private void ShowPanel(object? sender, PointerEventArgs e) => SetButtonsOpacity(1d);

    private void HidePanel(object? sender, PointerEventArgs e) => SetButtonsOpacity(0d);

    private void BtnSkinClick(object? sender, EventArgs e)
    {
        if (sender is MyIconButton { ContextMenu: { } menu } button)
            menu.Open(button);
    }

    private void BtnEditClick(object? sender, EventArgs e)
    {
        if (sender is MyIconButton { ContextMenu: { } menu } button)
            menu.Open(button);
    }

    private void SkinClick(object? sender, RoutedEventArgs e) => ChangeSkinRequested?.Invoke(this, EventArgs.Empty);

    private void BtnSkinSaveClick(object? sender, RoutedEventArgs e) => SaveSkinRequested?.Invoke(this, EventArgs.Empty);

    private void BtnSkinRefreshClick(object? sender, RoutedEventArgs e) => RefreshSkinRequested?.Invoke(this, EventArgs.Empty);

    private void BtnSkinCapeClick(object? sender, RoutedEventArgs e) => ChangeCapeRequested?.Invoke(this, EventArgs.Empty);

    private void BtnEditPasswordClick(object? sender, RoutedEventArgs e) => EditPasswordRequested?.Invoke(this, EventArgs.Empty);

    private void BtnEditNameClick(object? sender, RoutedEventArgs e) => EditNameRequested?.Invoke(this, EventArgs.Empty);

    private void ChangeProfile(object? sender, EventArgs e) => ChangeProfileRequested?.Invoke(this, EventArgs.Empty);

    private void SetButtonsOpacity(double opacity)
    {
        if (this.FindControl<MyCard>("PanButtons") is { } buttons)
            buttons.Opacity = opacity;
    }
}
