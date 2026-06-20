// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;

namespace PCL.Desktop.ViewModels;

public sealed class NavigationSectionViewModel : ObservableObject
{
    private bool _isSelected;

    public NavigationSectionViewModel(
        string title,
        string iconKey,
        IReadOnlyList<NavigationItemViewModel> items,
        Action<NavigationSectionViewModel> select)
    {
        Title = title;
        IconKey = iconKey;
        Items = items;
        OpenCommand = new DelegateCommand(() => select(this));
    }

    public string Title { get; }

    public string IconKey { get; }

    public IReadOnlyList<NavigationItemViewModel> Items { get; }

    public ICommand OpenCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}
