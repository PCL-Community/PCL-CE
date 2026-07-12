// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Features.Launching.Views;

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
            skin.HasCape = Profile.Kind != LaunchLoginProfileKind.Offline;
            string address = Profile.Kind == LaunchLoginProfileKind.Offline
                ? Profile.DisplaySkinAddress
                : MySkin.ResolveSkinAddress(
                    Profile.SkinAddress,
                    Profile.Uuid,
                    Profile.Kind == LaunchLoginProfileKind.ThirdParty ? Profile.AuthServer : null);
            if (string.IsNullOrWhiteSpace(address))
                address = Profile.DisplaySkinAddress;
            skin.Address = address;
            skin.Load();
        }
        if (this.FindControl<MyIconButton>("BtnEdit") is { } edit)
            edit.IsVisible = true;
    }

    private void ShowPanel(object? sender, PointerEventArgs e) => SetButtonsOpacity(1d);

    private void HidePanel(object? sender, PointerEventArgs e) => SetButtonsOpacity(0d);

    private void BtnSkinClick(object? sender, EventArgs e) => OpenSkinMenu();

    private void BtnEditClick(object? sender, EventArgs e) => OpenEditMenu();

    /// <summary>
    /// WPF-style ContextMenu + MyMenuItem (fixed min width so text is not zero-width).
    /// </summary>
    private void OpenSkinMenu()
    {
        if (this.FindControl<MyIconButton>("BtnSkin") is not { } button)
            return;

        ContextMenu menu = new()
        {
            Placement = PlacementMode.Bottom,
            MinWidth = 160
        };
        menu.Items.Add(CreateMenuItem("更换皮肤", () => ChangeSkinRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("保存皮肤", () => SaveSkinRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("刷新皮肤", () => RefreshSkinRequested?.Invoke(this, EventArgs.Empty)));
        if (Profile?.Kind != LaunchLoginProfileKind.Offline)
            menu.Items.Add(CreateMenuItem("更换披风", () => ChangeCapeRequested?.Invoke(this, EventArgs.Empty)));

        ShowContextMenu(button, menu);
    }

    private void OpenEditMenu()
    {
        if (this.FindControl<MyIconButton>("BtnEdit") is not { } button)
            return;

        ContextMenu menu = new()
        {
            Placement = PlacementMode.Bottom,
            MinWidth = 160
        };
        menu.Items.Add(CreateMenuItem("修改密码", () => EditPasswordRequested?.Invoke(this, EventArgs.Empty)));
        menu.Items.Add(CreateMenuItem("修改用户名", () => EditNameRequested?.Invoke(this, EventArgs.Empty)));
        ShowContextMenu(button, menu);
    }

    private static MyMenuItem CreateMenuItem(string header, Action action)
    {
        MyMenuItem item = new()
        {
            Header = header,
            MinWidth = 150,
            MinHeight = 32,
            Padding = new Avalonia.Thickness(14, 7)
        };
        item.Click += (_, e) =>
        {
            e.Handled = true;
            action();
        };
        return item;
    }

    private static void ShowContextMenu(Control target, ContextMenu menu)
    {
        void Open()
        {
            menu.Open(target);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Open();
        else
            Dispatcher.UIThread.Post(Open);
    }

    private void ChangeProfile(object? sender, EventArgs e) => ChangeProfileRequested?.Invoke(this, EventArgs.Empty);

    private void SetButtonsOpacity(double opacity)
    {
        if (this.FindControl<MyCard>("PanButtons") is { } buttons)
            buttons.Opacity = opacity;
    }
}
