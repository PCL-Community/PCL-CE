// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public partial class FontSelector : ContentControl
{
    private bool _fontsLoaded;
    private MyComboBox? _comboFont;
    private string? _tooltip;

    public FontSelector()
    {
        AvaloniaXamlLoader.Load(this);
        _comboFont = this.FindControl<MyComboBox>("ComboFont")
            ?? throw new InvalidOperationException("FontSelector 缺少 ComboFont。");
        _comboFont.ItemsSource = CustomFontCollection;
        _comboFont.ItemTemplate = new FuncDataTemplate<CustomFontProperties>(
            static (font, _) => new TextBlock
            {
                Text = font?.Name ?? string.Empty,
                FontFamily = font?.Font ?? FontFamily.Default
            });
        AttachedToVisualTree += (_, _) => _ = LoadFontsAsync();
    }

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = [];

    public string? Tooltip
    {
        get => _tooltip;
        set
        {
            _tooltip = value;
            ToolTip.SetTip(ComboFontControl, value);
        }
    }

    public string? SelectedFontTag
    {
        get => (ComboFontControl.SelectedItem as CustomFontProperties)?.Tag;
        set
        {
            for (int i = 0; i < CustomFontCollection.Count; i++)
            {
                if (string.Equals(CustomFontCollection[i].Tag, value, StringComparison.Ordinal))
                {
                    ComboFontControl.SelectedIndex = i;
                    return;
                }
            }

            ComboFontControl.SelectedIndex = 0;
        }
    }

    public int SelectedIndex
    {
        get => ComboFontControl.SelectedIndex;
        set => ComboFontControl.SelectedIndex = value;
    }

    public new bool IsEnabled
    {
        get => ComboFontControl.IsEnabled;
        set
        {
            base.IsEnabled = value;
            ComboFontControl.IsEnabled = value;
        }
    }

    private MyComboBox ComboFontControl =>
        _comboFont ??= this.FindControl<MyComboBox>("ComboFont")
            ?? throw new InvalidOperationException("FontSelector 缺少 ComboFont。");

    private async Task LoadFontsAsync()
    {
        if (_fontsLoaded)
            return;

        _fontsLoaded = true;
        MyComboBox comboFont = ComboFontControl;
        comboFont.IsEnabled = false;
        CustomFontCollection.Clear();
        CustomFontCollection.Add(new CustomFontProperties("默认", FontFamily.Default, string.Empty));
        CustomFontCollection.Add(new CustomFontProperties("正在加载字体列表...", FontFamily.Default, "__loading"));
        comboFont.SelectedIndex = 0;

        List<CustomFontProperties> fonts = await Task.Run(static () =>
            FontManager.Current.SystemFonts
                .Select(static font => new CustomFontProperties(
                    string.IsNullOrWhiteSpace(font.Name) ? font.ToString() : font.Name,
                    font,
                    string.IsNullOrWhiteSpace(font.Name) ? font.ToString() : font.Name))
                .DistinctBy(static font => font.Tag, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static font => font.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList()).ConfigureAwait(false);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            CustomFontCollection.Clear();
            CustomFontCollection.Add(new CustomFontProperties("默认", FontFamily.Default, string.Empty));
            foreach (CustomFontProperties font in fonts)
                CustomFontCollection.Add(font);

            MyComboBox uiComboFont = ComboFontControl;
            uiComboFont.SelectedIndex = 0;
            uiComboFont.IsEnabled = base.IsEnabled;
        });
    }

    private void ComboFontSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        SelectionChanged?.Invoke(this, e);

    public sealed class CustomFontProperties(string name, FontFamily font, string tag)
    {
        public string Name { get; } = name;

        public FontFamily Font { get; } = font;

        public string Tag { get; } = tag;
    }
}
