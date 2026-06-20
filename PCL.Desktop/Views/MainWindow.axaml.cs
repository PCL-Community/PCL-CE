// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PCL.Desktop.Composition;
using PCL.Desktop.ViewModels;

namespace PCL.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private readonly bool _usesMacCustomTitleBar;

    public MainWindow()
        : this(DesktopCompositionRoot.CreateMainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;

        _usesMacCustomTitleBar = viewModel.Environment.IsMacOS;
        if (_usesMacCustomTitleBar)
        {
            WindowDecorations = WindowDecorations.None;
            CanMaximize = false;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (_usesMacCustomTitleBar &&
            change.Property == WindowStateProperty &&
            change.GetNewValue<WindowState>() == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        base.OnClosed(e);
    }

    private void MacTitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;
}
