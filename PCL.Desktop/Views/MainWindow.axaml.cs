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
    public MainWindow()
        : this(DesktopCompositionRoot.CreateMainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        WindowDecorations = WindowDecorations.None;
        CanMaximize = false;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == WindowStateProperty &&
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

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseButton_OnClick(object? sender, RoutedEventArgs e) => Close();

    private void MinimizeButton_OnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void ResizeTop_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.North, e);

    private void ResizeBottom_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.South, e);

    private void ResizeLeft_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.West, e);

    private void ResizeRight_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.East, e);

    private void ResizeTopLeft_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.NorthWest, e);

    private void ResizeTopRight_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.NorthEast, e);

    private void ResizeBottomLeft_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.SouthWest, e);

    private void ResizeBottomRight_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs e) =>
        BeginResizeDrag(WindowEdge.SouthEast, e);
}
