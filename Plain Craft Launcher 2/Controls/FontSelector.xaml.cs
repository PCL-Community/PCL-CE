using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class FontSelector
{
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    public static readonly DependencyProperty TooltipProperty = DependencyProperty.Register(nameof(Tooltip),
        typeof(string), typeof(FontSelector), new PropertyMetadata(null, OnTooltipChanged));

    private bool _isInitializing;
    private bool _isListeningLanguageChanged;
    private string? _pendingFontTag;

    public FontSelector()
    {
        InitializeComponent();
        Loaded += FontSelector_Loaded;
        Unloaded += FontSelector_Unloaded;
        ComboFont.SelectionChanged += ComboFont_SelectionChanged;
    }

    public string Tooltip
    {
        get => (string)GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = [];

    public string SelectedFontTag
    {
        get
        {
            if (ComboFont.SelectedItem is null) return "";
            return ComboFont.SelectedItem is not CustomFontProperties selectedFont ? "" : selectedFont.Tag;
        }
        set
        {
            // 如果字体还在加载中，延迟设置
            if (CustomFontCollection.Count == 0 ||
                (CustomFontCollection is [{ Name: var name }] && name == Lang.Text("Common.State.Loading")))
            {
                _pendingFontTag = value;
                return;
            }

            _isInitializing = true;

            var targetSelection = CustomFontCollection.FirstOrDefault(i => i.Tag == value);
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
        if (d is FontSelector control) control.ComboFont.ToolTip = e.NewValue;
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private void FontSelector_Loaded(object sender, RoutedEventArgs e)
    {
        StartListeningLanguageChanged();

        if (CustomFontCollection.Count == 0)
            LoadFonts();
        else
            RefreshDefaultFont();
    }

    private void FontSelector_Unloaded(object sender, RoutedEventArgs e)
    {
        StopListeningLanguageChanged();
    }

    private void StartListeningLanguageChanged()
    {
        if (_isListeningLanguageChanged) return;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = true;
    }

    private void StopListeningLanguageChanged()
    {
        if (!_isListeningLanguageChanged) return;
        LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = false;
    }

    private void LocalizationService_LanguageChanged()
    {
        RefreshDefaultFont();
    }

    private void RefreshDefaultFont()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RefreshDefaultFont);
            return;
        }

        GetDefaultFont()?.Font = LocalizationFontService.BuildLaunchFontFamily();
    }

    private void LoadFonts()
    {
        Dispatcher.BeginInvoke(async () =>
        {
            ComboFont.IsEnabled = false;
            _isInitializing = true;
            CustomFontCollection.Add(new CustomFontProperties { Name = Lang.Text("Common.State.Loading") });
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
                Name = Lang.Text("Common.Option.Default"),
                Font = LocalizationFontService.BuildLaunchFontFamily(),
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

    private CustomFontProperties? GetDefaultFont()
    {
        return CustomFontCollection.FirstOrDefault(i => string.IsNullOrEmpty(i.Tag));
    }

    public class CustomFontProperties : INotifyPropertyChanged
    {
        public string Name
        {
            get;
            init => SetField(ref field, value);
        } = string.Empty;

        public FontFamily Font
        {
            get;
            set => SetField(ref field, value);
        } = LocalizationFontService.BuildLaunchFontFamily();

        public string Tag
        {
            get;
            set => SetField(ref field, value);
        } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
