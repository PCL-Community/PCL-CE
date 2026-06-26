// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PCL.Desktop.Controls.Legacy;

public partial class MySearchBox : MyCard
{
    private readonly MyTextBox? _textBox;
    private readonly MyIconButton? _clearButton;
    private readonly MyButton? _searchButton;

    public MySearchBox()
    {
        AvaloniaXamlLoader.Load(this);
        _textBox = this.FindControl<MyTextBox>("TextBox");
        _clearButton = this.FindControl<MyIconButton>("BtnClear");
        _searchButton = this.FindControl<MyButton>("BtnSearch");
    }

    public event EventHandler? Search;

    public string Text
    {
        get => _textBox?.Text ?? string.Empty;
        set
        {
            if (_textBox is not null)
                _textBox.Text = value;
        }
    }

    private void Text_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_clearButton is not null)
        {
            var hasText = !string.IsNullOrEmpty(_textBox?.Text);
            _clearButton.Opacity = hasText ? 1 : 0;
            _clearButton.IsHitTestVisible = hasText;
        }
    }

    private void BtnClear_Click(object? sender, EventArgs e)
    {
        Text = string.Empty;
    }

    private void BtnSearch_Click(object? sender, EventArgs e)
    {
        Search?.Invoke(this, EventArgs.Empty);
    }
}
