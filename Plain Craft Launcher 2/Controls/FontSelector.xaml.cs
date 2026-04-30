using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class FontSelector
{
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    public new static readonly DependencyProperty TooltipProperty = DependencyProperty.Register("Tooltip",
        typeof(string), typeof(FontSelector), new PropertyMetadata(null, OnTooltipChanged));

    private bool _isInitializing;
    private string _pendingFontTag;

    public FontSelector()
    {
        InitializeComponent();
        Loaded += FontSelector_Loaded;
        ComboFont.SelectionChanged += ComboFont_SelectionChanged;
    }


    public new string Tooltip
    {
        get => (string)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = new();

    public string SelectedFontTag
    {
        get
        {
            if (ComboFont.SelectedItem is null)
                return "";
            var selectedFont = ComboFont.SelectedItem as CustomFontProperties;
            if (selectedFont is null)
                return "";
            return selectedFont.Tag;
        }
        set
        {
            // 如果字体还在加载中，延迟设置
            if (CustomFontCollection.Count == 0 ||
                (CustomFontCollection.Count == 1 && CustomFontCollection[0].Name == "加载中..."))
            {
                _pendingFontTag = value;
                return;
            }

            _isInitializing = true;

            var targetSelection = CustomFontCollection.FirstOrDefault(i => (i.Tag ?? "") == (value ?? ""));
            if (targetSelection is null)
                ComboFont.SelectedIndex = 0;
            else
                ComboFont.SelectedItem = targetSelection;

            _isInitializing = false;
        }
    }

    public int SelectedIndex
    {
        get => ComboFont.SelectedIndex;
        set => ComboFont.SelectedIndex = value;
    }

    public new bool IsEnabled
    {
        get => ComboFont.IsEnabled;
        set => ComboFont.IsEnabled = value;
    }

    private static void OnTooltipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = d as FontSelector;
        if (control is not null) control.ComboFont.ToolTip = e.NewValue;
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private void FontSelector_Loaded(object sender, RoutedEventArgs e)
    {
        if (CustomFontCollection.Count == 0) LoadFonts();
    }

    private void LoadFonts()
    {
        Dispatcher.BeginInvoke(async () =>
        {
            ComboFont.IsEnabled = false;
            _isInitializing = true;
            CustomFontCollection.Add(new CustomFontProperties { Name = "加载中..." });
            ComboFont.SelectedIndex = 0;

            var availableFonts = new List<(string Name, FontFamily Font)>();

            await Task.Run(() =>
            {
                foreach (var font in Fonts.SystemFontFamilies)
                    try
                    {
                        if (font.Source.StartsWith("Global ")) continue;

                        foreach (var typeface in font.GetTypefaces())
                        {
                            if (!typeface.TryGetGlyphTypeface(out var glyph))
                                throw new NullReferenceException(
                                    $"字形 {typeface.FaceNames.GetForCurrentUiCulture("(unknown)")} 无法加载");

                            _ = new GlyphTypeface(glyph.FontUri);
                        }

                        availableFonts.Add((font.FamilyNames.GetForCurrentUiCulture(), font));
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Error(ex, $"发现了一个无法加载的异常的字体：{font.Source}");
                    }

                availableFonts.Sort((l, r) => string.Compare(l.Name, r.Name, StringComparison.Ordinal));
            });

            CustomFontCollection.Clear();
            CustomFontCollection.Add(new CustomFontProperties
            {
                Name = "默认",
                Font = new FontFamily(new Uri("pack://application:,,,/"),
                    "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI"),
                Tag = ""
            });

            foreach (var font in availableFonts)
                CustomFontCollection.Add(new CustomFontProperties
                    { Name = font.Name, Font = font.Font, Tag = font.Font.Source });

            ComboFont.IsEnabled = true;

            if (_pendingFontTag != null)
            {
                var pendingTag = _pendingFontTag;
                _pendingFontTag = null;
                SelectedFontTag = pendingTag;
            }

            _isInitializing = false;
        });
    }

    private void ComboFont_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing) SelectionChanged?.Invoke(sender, e);
    }

    public class CustomFontProperties
    {
        public string Name { get; set; }
        public FontFamily Font { get; set; }
        public string Tag { get; set; }
    }
}