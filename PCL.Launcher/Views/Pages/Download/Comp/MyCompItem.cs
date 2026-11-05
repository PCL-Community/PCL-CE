using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using static PCL.UI.Controls.ContainerTheme;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.Views.Pages.Download;

public sealed class MyCompItem : Grid
{
    private readonly MyIconButton _btnDownload;
    private readonly MyIconButton _btnFavorite;
    private readonly Border _hoverBack;
    private readonly TextBlock _labDescription;
    private readonly TextBlock _labDownload;
    private readonly TextBlock _labSource;
    private readonly TextBlock _labSubTitle;
    private readonly TextBlock _labTime;
    private readonly TextBlock _labTitle;
    private readonly TextBlock _labVersion;
    private readonly MyImage _logo;
    private readonly StackPanel _panButtons;
    private readonly StackPanel _panTags;

    public MyCompItem(
        Comp.CompProject project,
        bool showMcVersionDesc = true,
        bool showLoaderDesc = true,
        bool showQuickDownload = false)
    {
        Project = project;
        Height = 64d;
        Margin = new Thickness(0d, 0d, 0d, 2d);
        ColumnDefinitions = new ColumnDefinitions("7,50,8,Auto,*,Auto");
        RowDefinitions = new RowDefinitions("*,20,18,16,1.1*");
        Cursor = new Cursor(StandardCursorType.Hand);
        BindDynamic(this, BackgroundProperty, "ColorBrushSemiTransparent");

        _hoverBack = new Border
        {
            CornerRadius = new CornerRadius(3d),
            IsHitTestVisible = false,
            Opacity = 0d
        };
        BindDynamic(_hoverBack, Border.BackgroundProperty, "ColorBrushBg1");
        SetColumnSpan(_hoverBack, 99);
        SetRowSpan(_hoverBack, 99);
        Children.Add(_hoverBack);

        _logo = new MyImage
        {
            Width = 50d,
            Height = 50d,
            CornerRadius = new CornerRadius(6d),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };
        ApplyLogo(project);
        SetColumn(_logo, 1);
        SetRow(_logo, 1);
        SetRowSpan(_logo, 3);
        Children.Add(_logo);

        var title = project.GetControlTitle(true);
        _labTitle = new TextBlock
        {
            Text = title.Key,
            FontSize = 14d,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        BindDynamic(_labTitle, TextBlock.ForegroundProperty, "ColorBrush1");
        _labSubTitle = new TextBlock
        {
            Text = title.Value,
            FontSize = 12d,
            Margin = new Thickness(6d, 0d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.4d,
            IsVisible = !string.IsNullOrEmpty(title.Value),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        BindDynamic(_labSubTitle, TextBlock.ForegroundProperty, "ColorBrushGray1");

        var panTitle = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        panTitle.Children.Add(_labTitle);
        panTitle.Children.Add(_labSubTitle);
        SetColumn(panTitle, 3);
        SetColumnSpan(panTitle, 2);
        SetRow(panTitle, 1);
        Children.Add(panTitle);

        var description = (project.Description ?? string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);
        _labDescription = new TextBlock
        {
            Text = description,
            FontSize = 12d,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0d, 0d, 3d, 0.5d)
        };
        BindDynamic(_labDescription, TextBlock.ForegroundProperty, "ColorBrushGray3");
        _labDescription.PointerEntered += (_, _) => UpdateDescriptionToolTip();
        SetColumn(_labDescription, 4);
        SetRow(_labDescription, 2);
        Children.Add(_labDescription);

        _panTags = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-1d, 0d, 1d, 0d),
            IsVisible = false
        };
        SetColumn(_panTags, 3);
        SetRow(_panTags, 2);
        Children.Add(_panTags);
        Tags = project.Tags;

        _labVersion = CreateStatText(
            project.BuildVersionDescription(showMcVersionDesc, showLoaderDesc));
        _labDownload = CreateStatText(Lang.CompactNumber(project.DownloadCount));
        _labTime = CreateStatText(
            project.LastUpdate is { } lastUpdate
                ? Lang.TimeSpan(lastUpdate - DateTime.Now, 1)
                : string.Empty);
        _labSource = CreateStatText(project.FromCurseForge ? "CurseForge" : "Modrinth");

        var statsColumns = new ColumnDefinitions();
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "CompItemVersion"
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = new GridLength(0.25d, GridUnitType.Star)
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "LabDownload"
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = new GridLength(0.25d, GridUnitType.Star)
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = 57d,
                SharedSizeGroup = "CompItemTime"
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = new GridLength(0.25d, GridUnitType.Star)
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "CompItemSource"
            });
        statsColumns.Add(
            new ColumnDefinition
            {
                Width = new GridLength(0.25d, GridUnitType.Star)
            });

        var panStats = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            ColumnDefinitions = statsColumns
        };
        if (showMcVersionDesc || showLoaderDesc)
            AddStat(panStats, 0, 1, "lucide/layout-list", _labVersion);
        if (!string.IsNullOrEmpty(_labDownload.Text))
            AddStat(panStats, 3, 4, "lucide/download", _labDownload);
        if (project.LastUpdate is not null)
            AddStat(panStats, 6, 7, "lucide/arrow-up-from-line", _labTime);
        AddStat(panStats, 9, 10, "lucide/globe", _labSource);
        SetColumn(panStats, 3);
        SetColumnSpan(panStats, 2);
        SetRow(panStats, 3);
        Children.Add(panStats);

        _btnDownload = new MyIconButton
        {
            Height = 25d,
            Width = 25d,
            SvgIcon = "lucide/download",
            Theme = MyIconButton.Themes.Custom,
            Margin = new Thickness(0d, 0d, 4d, 0d),
            IsVisible = showQuickDownload
        };
        ToolTip.SetTip(_btnDownload, Lang.Text("Download.Comp.QuickDownload.Btn.ToolTip"));
        _btnDownload.Click += (_, _) =>
        {
            if (CanInteraction && _panButtons is { Opacity: > 0d })
                Comp.QuickDownload(project);
        };

        _btnFavorite = new MyIconButton
        {
            Height = 25d,
            Width = 25d,
            SvgIcon = "lucide/heart",
            Theme = MyIconButton.Themes.Custom
        };
        ToolTip.SetTip(_btnFavorite, Lang.Text("Download.Comp.Detail.Favorite"));
        _btnFavorite.Click += (_, _) =>
        {
            if (CanInteraction && _panButtons is { Opacity: > 0d })
                CompFavoritesMenu.Show(project, _btnFavorite, RefreshFavoriteStatus);
        };

        _panButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            Opacity = 0d,
            Spacing = 0d
        };
        _panButtons.Children.Add(_btnDownload);
        _panButtons.Children.Add(_btnFavorite);
        SetColumn(_panButtons, 5);
        SetRow(_panButtons, 0);
        SetRowSpan(_panButtons, 5);
        Children.Add(_panButtons);

        RefreshFavoriteStatus();

        PointerEntered += (_, _) =>
        {
            _hoverBack.Opacity = 1d;
            if (ShowFavoriteBtn || ShowDownloadBtn)
                _panButtons.Opacity = 1d;
        };
        PointerExited += (_, _) =>
        {
            _hoverBack.Opacity = 0d;
            _panButtons.Opacity = 0d;
        };
        PointerReleased += (_, e) =>
        {
            if (!CanInteraction || e.InitialPressMouseButton != MouseButton.Left)
                return;
            if (IsPointerOnActionButton(e.Source as Visual))
                return;
            Click?.Invoke(this);
        };
    }

    public Comp.CompProject Project { get; }

    public bool CanInteraction { get; set; } = true;

    /// <summary>标签列表，内容为本地化资源键。</summary>
    public IEnumerable<string>? Tags
    {
        set =>
            TagPanelHelper.FillLocalized(
                _panTags,
                value,
                new SolidColorBrush(Color.FromRgb(134, 134, 134)));
    }

    public bool ShowDownloadBtn
    {
        get => _btnDownload.IsVisible;
        set
        {
            _btnDownload.IsVisible = value;
            UpdatePanButtons();
        }
    }

    public bool ShowFavoriteBtn
    {
        get => _btnFavorite.IsVisible;
        set
        {
            _btnFavorite.IsVisible = value;
            UpdatePanButtons();
        }
    }

    private void UpdatePanButtons()
    {
        _panButtons.IsVisible = ShowFavoriteBtn || ShowDownloadBtn;
    }

    private void UpdateDescriptionToolTip()
    {
        var text = _labDescription.Text;
        if (string.IsNullOrEmpty(text) || _labDescription.Bounds.Width <= 0d)
        {
            ToolTip.SetTip(_labDescription, null);
            return;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                _labDescription.FontFamily,
                _labDescription.FontStyle,
                _labDescription.FontWeight,
                _labDescription.FontStretch),
            _labDescription.FontSize,
            _labDescription.Foreground);
        ToolTip.SetTip(
            _labDescription,
            formatted.WidthIncludingTrailingWhitespace > _labDescription.Bounds.Width + 0.5d
                ? text
                : null);
    }

    public event Action<MyCompItem>? Click;

    public void RefreshFavoriteStatus()
    {
        _btnFavorite.SvgIcon = Comp.CompFavorites.IsFavourite(Project.Id)
            ? "lucide/heart-filled"
            : "lucide/heart";
    }

    private static void AddStat(
        Grid grid,
        int iconColumn,
        int textColumn,
        string icon,
        TextBlock text)
    {
        var iconControl = new SvgIconControl
        {
            Icon = icon,
            Width = 12d,
            Height = 12d,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        BindDynamic(iconControl, SvgIconControl.IconBrushProperty, "ColorBrushGray3");
        SetColumn(iconControl, iconColumn);
        grid.Children.Add(iconControl);

        text.Margin = new Thickness(4d, 0d, 2d, 0d);
        SetColumn(text, textColumn);
        grid.Children.Add(text);
    }

    private bool IsPointerOnActionButton(Visual? source)
    {
        for (var current = source;
             current is not null && current != this;
             current = current.GetVisualParent())
            if (current is MyIconButton)
                return true;
        return false;
    }

    private void ApplyLogo(Comp.CompProject project)
    {
        if (string.IsNullOrEmpty(project.LogoUrl))
        {
            _logo.Source = "avares://PCL.Launcher/Assets/Images/Icons/NoIcon.png";
            return;
        }

        _logo.Source = project.LogoUrl;
        try
        {
            var fallback = GameDownload.DlSourceModGet(project.LogoUrl);
            if (!string.IsNullOrEmpty(fallback))
                _logo.FallbackSource = fallback;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private static TextBlock CreateStatText(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 12d,
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = !string.IsNullOrEmpty(text),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        BindDynamic(block, TextBlock.ForegroundProperty, "ColorBrushGray4");
        return block;
    }
}