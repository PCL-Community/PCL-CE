// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views.Launch;

public partial class PageLoginProfile : Grid, PageLaunchLeft.ILoginPage
{
    public PageLoginProfile()
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = this;
        if (this.FindControl<MyIconButton>("BtnNew") is { } create)
            create.Click += (_, _) => CreateProfileRequested?.Invoke(this, EventArgs.Empty);
        if (this.FindControl<MyIconButton>("BtnPort") is { } port)
            port.Click += (_, _) => ImportExportRequested?.Invoke(this, EventArgs.Empty);
        AttachedToVisualTree += (_, _) => Reload();
    }

    public ObservableCollection<LoginProfileInfo> ProfileCollection { get; } = [];

    public LoginProfileInfo? SelectedProfile { get; private set; }

    public event EventHandler<LoginProfileInfo>? ProfileSelected;

    public event EventHandler? CreateProfileRequested;

    public event EventHandler? ImportExportRequested;

    public void SetProfiles(IEnumerable<LoginProfileInfo> profiles, LoginProfileInfo? selectedProfile = null)
    {
        ProfileCollection.Clear();
        foreach (LoginProfileInfo profile in profiles)
            ProfileCollection.Add(profile);

        SelectedProfile = selectedProfile;
        RefreshHints();
    }

    public void Reload() => RefreshHints();

    private void SelectProfile(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem item)
            return;

        LoginProfileInfo? profile = item.Tag as LoginProfileInfo ??
                                    item.DataContext as LoginProfileInfo;
        if (profile is null)
            return;

        SelectedProfile = profile;
        ProfileSelected?.Invoke(this, profile);
    }

    private void RefreshHints()
    {
        bool hasProfiles = ProfileCollection.Count > 0;
        if (this.FindControl<MyHint>("HintCreate") is { } create)
            create.IsVisible = !hasProfiles;
        if (this.FindControl<MyHint>("HintSelect") is { } select)
            select.IsVisible = hasProfiles;
    }
}
