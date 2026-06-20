// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;

namespace PCL.Desktop.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    private bool _isSelected;

    public NavigationItemViewModel(
        string title,
        string description,
        string iconKey,
        object page,
        bool isComingSoon,
        Action<NavigationItemViewModel> select)
    {
        Title = title;
        Description = description;
        IconKey = iconKey;
        Page = page;
        IsComingSoon = isComingSoon;
        OpenCommand = new DelegateCommand(() => select(this));
    }

    public string Title { get; }

    public string Description { get; }

    public string IconKey { get; }

    public object Page { get; }

    public bool IsComingSoon { get; }

    public ICommand OpenCommand { get; }

    public bool IsSelected
    {
        get => _isSelected;
        internal set => SetProperty(ref _isSelected, value);
    }
}
