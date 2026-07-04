// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public sealed class MyMarkdownViewer : TextBlock
{
    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MyMarkdownViewer, string>(nameof(Markdown), string.Empty);

    public MyMarkdownViewer()
    {
        TextWrapping = TextWrapping.Wrap;
        FontSize = 15d;
        LineHeight = 20d;
        this.GetObservable(MarkdownProperty).Subscribe(markdown => Text = markdown);
    }

    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }
}
