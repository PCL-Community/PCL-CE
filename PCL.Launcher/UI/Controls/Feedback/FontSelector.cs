using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL.UI.Controls;

/// <summary>
///     字体选择器：列出系统字体并附带一项语言默认字体。
///     默认项使用 <see cref="LaunchFonts" /> 生成的字体链，语言切换后自动刷新；
///     选择结果通过 <see cref="SelectedFontTag" /> 与 <see cref="SelectionChanged" /> 对外暴露。
/// </summary>
public class FontSelector : ContentControl
{
    public delegate void SelectionChangedEventHandler(object sender, SelectionChangedEventArgs e);

    private const string LoadingFontTag = "\0loading";

    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<FontSelector, string?>(nameof(Tooltip));

    private readonly MyComboBox _comboFont;
    private bool _isInitializing;
    private bool _isListeningLanguageChanged;
    private string? _pendingFontTag;

    public FontSelector()
    {
        _comboFont = new MyComboBox
        {
            Name = "ComboFont",
            ItemTemplate = BuildItemTemplate(),
            MaxDropDownHeight = 300d,
            ItemsSource = CustomFontCollection
        };
        _comboFont.SelectionChanged += ComboFont_SelectionChanged;

        Content = _comboFont;
        FeedbackTheme.AttachStyles(this);

        Loaded += (_, _) => FontSelector_Loaded();
        Unloaded += (_, _) => FontSelector_Unloaded();
    }

    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = [];

    public string SelectedFontTag
    {
        get =>
            _comboFont.SelectedItem is not CustomFontProperties selectedFont
                ? string.Empty
                : selectedFont.Tag;
        set
        {
            var tag = value ?? string.Empty;
            // 如果字体还在加载中，延迟设置
            if (CustomFontCollection.Count == 0
                || CustomFontCollection is [{ Tag: LoadingFontTag }])
            {
                _pendingFontTag = tag;
                return;
            }

            _isInitializing = true;
            var targetSelection = CustomFontCollection.FirstOrDefault(item => item.Tag == tag);
            if (targetSelection is null)
                _comboFont.SelectedIndex = 0;
            else
                _comboFont.SelectedItem = targetSelection;
            _isInitializing = false;
        }
    }

    public int SelectedIndex
    {
        get => _comboFont.SelectedIndex;
        set => _comboFont.SelectedIndex = value;
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TooltipProperty)
            ToolTip.SetTip(_comboFont, change.NewValue as string);
        else if (change.Property == IsEnabledProperty && change.NewValue is bool isEnabled)
            _comboFont.IsEnabled = isEnabled;
    }

    private static IDataTemplate BuildItemTemplate()
    {
        return new FuncDataTemplate<CustomFontProperties>((font, _) =>
        {
            var text = new TextBlock
            {
                TextAlignment = TextAlignment.Center
            };
            text.Bind(TextBlock.TextProperty, new Binding(nameof(CustomFontProperties.Name)));
            text.Bind(TextBlock.FontFamilyProperty, new Binding(nameof(CustomFontProperties.Font)));
            return text;
        });
    }

    private void FontSelector_Loaded()
    {
        StartListeningLanguageChanged();

        if (CustomFontCollection.Count == 0)
            LoadFonts();
        else
            RefreshDefaultFont();
    }

    private void FontSelector_Unloaded()
    {
        StopListeningLanguageChanged();
    }

    private void StartListeningLanguageChanged()
    {
        if (_isListeningLanguageChanged)
            return;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = true;
    }

    private void StopListeningLanguageChanged()
    {
        if (!_isListeningLanguageChanged)
            return;
        LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = false;
    }

    private void LocalizationService_LanguageChanged()
    {
        RefreshDefaultFont();
    }

    private void RefreshDefaultFont()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshDefaultFont);
            return;
        }

        var defaultFont = GetDefaultFont();
        defaultFont?.NotifyNameChanged();
        defaultFont?.Font = LaunchFonts.ForCurrentLanguage();
    }

    private void LoadFonts()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _comboFont.IsEnabled = false;
            _isInitializing = true;
            CustomFontCollection.Add(
                new CustomFontProperties
                {
                    NameKey = "Common.State.Loading",
                    Tag = LoadingFontTag
                });
            _comboFont.SelectedIndex = 0;

            var availableFonts = new List<(string Name, FontFamily Font)>();

            foreach (var font in FontManager.Current.SystemFonts)
                try
                {
                    var name = font.Name;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    if (name.StartsWith("Global ", StringComparison.Ordinal))
                        continue;

                    availableFonts.Add((name, font));
                }
                catch (Exception ex)
                {
                    LogWrapper.Debug(ex, "FontSelector", $"[FontSelector] 发现了一个无法加载的异常的字体：{font}");
                }

            availableFonts.Sort((left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.Ordinal));

            CustomFontCollection.Clear();
            CustomFontCollection.Add(
                new CustomFontProperties
                {
                    NameKey = "Common.Option.Default",
                    Font = LaunchFonts.ForCurrentLanguage(),
                    Tag = string.Empty
                });

            foreach (var font in availableFonts)
                CustomFontCollection.Add(
                    new CustomFontProperties
                    {
                        RawName = font.Name,
                        Font = font.Font,
                        Tag = font.Font.Name
                    });

            _comboFont.IsEnabled = IsEnabled;

            if (_pendingFontTag is not null)
            {
                var pendingTag = _pendingFontTag;
                _pendingFontTag = null;
                SelectedFontTag = pendingTag;
            }

            _isInitializing = false;
        });
    }

    private void ComboFont_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing)
            SelectionChanged?.Invoke(this, e);
    }

    private CustomFontProperties? GetDefaultFont()
    {
        return CustomFontCollection.FirstOrDefault(item => string.IsNullOrEmpty(item.Tag));
    }

    public class CustomFontProperties : INotifyPropertyChanged
    {
        public string Name =>
            string.IsNullOrEmpty(NameKey)
                ? RawName
                : Lang.Text(NameKey);

        public string RawName
        {
            get;
            init => SetField(ref field, value);
        } = string.Empty;

        public string? NameKey
        {
            get;
            set => SetField(ref field, value);
        }

        public FontFamily Font
        {
            get;
            set => SetField(ref field, value);
        } = LaunchFonts.Default;

        public string Tag
        {
            get;
            set => SetField(ref field, value);
        } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void NotifyNameChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }

        private void SetField<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}