// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PCL.Desktop.ViewModels.Log;

namespace PCL.Desktop.Controls.Log;

public sealed partial class LogViewer : UserControl
{
    private readonly ListBox _logList;
    private LogPageViewModel? _viewModel;

    public LogViewer()
    {
        AvaloniaXamlLoader.Load(this);
        _logList = this.FindControl<ListBox>("LogList")!;
    }

    protected override void OnAttachedToVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach(DataContext as LogPageViewModel);
    }

    protected override void OnDetachedFromVisualTree(
        VisualTreeAttachmentEventArgs e)
    {
        Attach(null);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (VisualRoot is not null)
            Attach(DataContext as LogPageViewModel);
    }

    private void Attach(LogPageViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        if (_viewModel is not null)
        {
            ((INotifyCollectionChanged)_viewModel.Lines).CollectionChanged -=
                OnLinesChanged;
        }
        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            ((INotifyCollectionChanged)_viewModel.Lines).CollectionChanged +=
                OnLinesChanged;
        }
    }

    private void OnLinesChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is not { AutoScroll: true } ||
            _viewModel.Lines.Count == 0)
        {
            return;
        }

        _logList.ScrollIntoView(
            _viewModel.Lines[^1]);
    }
}
