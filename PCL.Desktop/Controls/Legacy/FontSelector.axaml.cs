// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public partial class FontSelector : ContentControl
{
    private bool _fontsLoaded;
    private bool _isInitializing;
    private bool _hasPendingFontTag;
    private Task? _loadTask;
    private MyComboBox? _comboFont;
    private string _pendingFontTag = string.Empty;
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
            string normalized = value?.Trim() ?? string.Empty;
            if (!_fontsLoaded || CustomFontCollection.Any(static font => font.Tag == "__loading"))
            {
                _pendingFontTag = normalized;
                _hasPendingFontTag = true;
                return;
            }

            SelectFont(normalized);
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

    private Task LoadFontsAsync()
    {
        return EnsureFontsLoadedAsync();
    }

    private Task LoadFontsCoreAsync(FontFamily[]? suppliedFonts)
    {
        if (_fontsLoaded)
            return Task.CompletedTask;

        _fontsLoaded = true;
        _isInitializing = true;
        MyComboBox comboFont = ComboFontControl;
        comboFont.IsEnabled = false;
        CustomFontCollection.Clear();
        CustomFontCollection.Add(new CustomFontProperties("默认", FontFamily.Default, string.Empty));
        CustomFontCollection.Add(new CustomFontProperties("正在加载字体列表...", FontFamily.Default, "__loading"));
        comboFont.SelectedIndex = 0;

        FontFamily[] systemFonts = suppliedFonts ?? FontManager.Current.SystemFonts.ToArray();
        List<CustomFontProperties> fonts = systemFonts
            .Select(static font => new CustomFontProperties(
                string.IsNullOrWhiteSpace(font.Name) ? font.ToString() : font.Name,
                font,
                string.IsNullOrWhiteSpace(font.Name) ? font.ToString() : font.Name))
            .DistinctBy(static font => font.Tag, StringComparer.OrdinalIgnoreCase)
            .OrderBy(static font => font.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        CustomFontCollection.Clear();
        CustomFontCollection.Add(new CustomFontProperties("默认", FontFamily.Default, string.Empty));
        foreach (CustomFontProperties font in fonts)
            CustomFontCollection.Add(font);

        MyComboBox uiComboFont = ComboFontControl;
        SelectFont(_hasPendingFontTag ? _pendingFontTag : string.Empty);
        _hasPendingFontTag = false;
        uiComboFont.IsEnabled = base.IsEnabled;
        _isInitializing = false;
        return Task.CompletedTask;
    }

    public Task EnsureFontsLoadedAsync(IEnumerable<FontFamily>? suppliedFonts = null)
    {
        FontFamily[]? snapshot = suppliedFonts?.ToArray();
        return _loadTask ??= LoadFontsCoreAsync(snapshot);
    }

    private void SelectFont(string tag)
    {
        for (int i = 0; i < CustomFontCollection.Count; i++)
        {
            if (!string.Equals(CustomFontCollection[i].Tag, tag, StringComparison.OrdinalIgnoreCase))
                continue;

            ComboFontControl.SelectedIndex = i;
            return;
        }

        ComboFontControl.SelectedIndex = CustomFontCollection.Count == 0 ? -1 : 0;
    }

    private void ComboFontSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
            SelectionChanged?.Invoke(this, e);
    }

    public sealed class CustomFontProperties(string name, FontFamily font, string tag)
    {
        public string Name { get; } = name;

        public FontFamily Font { get; } = font;

        public string Tag { get; } = tag;

        public override string ToString() => Name;
    }
}
