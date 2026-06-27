// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

public sealed class MyMsgSelectClosedEventArgs(int? selectedIndex) : EventArgs
{
    public int? SelectedIndex { get; } = selectedIndex;
}

public partial class MyMsgSelect : Grid
{
    private readonly List<MyListItem> _items = [];
    private int _selectedIndex = -1;

    public MyMsgSelect()
    {
        AvaloniaXamlLoader.Load(this);
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
            confirm.IsEnabled = false;
    }

    public event EventHandler<MyMsgSelectClosedEventArgs>? Closed;

    public IReadOnlyList<MyListItem> Items => _items;

    public int SelectedIndex => _selectedIndex;

    public void Configure(
        string title,
        IEnumerable<MyListItem> items,
        string primaryButton = "继续",
        string secondaryButton = "取消")
    {
        if (this.FindControl<TextBlock>("LabTitle") is { } titleBlock)
            titleBlock.Text = title;
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
        {
            confirm.Text = primaryButton;
            confirm.IsEnabled = false;
        }
        if (this.FindControl<MyButton>("Btn2") is { } cancel)
        {
            cancel.Text = secondaryButton;
            cancel.IsVisible = !string.IsNullOrWhiteSpace(secondaryButton);
        }

        _selectedIndex = -1;
        _items.Clear();
        if (this.FindControl<StackPanel>("PanSelection") is not { } panel)
            return;

        panel.Children.Clear();
        foreach (MyListItem item in items)
        {
            item.Type = MyListItemType.RadioBox;
            item.MinHeight = 24d;
            item.Click += SelectionClick;
            _items.Add(item);
            panel.Children.Add(item);
        }
    }

    private void SelectionClick(object? sender, EventArgs e)
    {
        if (sender is not MyListItem item)
            return;

        _selectedIndex = _items.IndexOf(item);
        if (this.FindControl<MyButton>("Btn1") is { } confirm)
            confirm.IsEnabled = _selectedIndex >= 0;
    }

    private void Btn1Click(object? sender, EventArgs e)
    {
        if (_selectedIndex < 0)
            return;

        Closed?.Invoke(this, new MyMsgSelectClosedEventArgs(_selectedIndex));
    }

    private void Btn2Click(object? sender, EventArgs e) =>
        Closed?.Invoke(this, new MyMsgSelectClosedEventArgs(null));
}
