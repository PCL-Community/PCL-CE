using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.UI.Controls.SvgIcon;

namespace PCL;

public static class MinecraftCrashVisualFactory
{
    public static MyCard CreateHeroCard(MinecraftCrashSession session)
    {
        var presentation = session.Presentation;
        var root = new StackPanel();

        var title = Text(
            MinecraftCrashUi.Text(presentation.Summary.TitleKey, presentation.Summary.Parameters),
            20, FontWeights.SemiBold
        );
        title.Margin = new Thickness(0d, 0d, 0d, 8d);
        root.Children.Add(title);

        var subtitle = Text(
            MinecraftCrashUi.Text(presentation.Summary.DescriptionKey, presentation.Summary.Parameters),
            13.5
        );
        subtitle.Margin = new Thickness(0d, 0d, 0d, 5d);
        root.Children.Add(subtitle);

        if (!string.IsNullOrWhiteSpace(presentation.Summary.DetailKey))
        {
            var detail = Text(
                MinecraftCrashUi.Text(presentation.Summary.DetailKey!, presentation.Summary.Parameters),
                13.5
            );
            detail.Margin = new Thickness(0d);
            root.Children.Add(detail);
        }

        return MinecraftCrashUi.CreateCard("Crash.Overview.Card.Hero", root);
    }

    public static MyCard CreateMetricGrid(IReadOnlyList<CrashPresentationMetric> metrics)
    {
        var panel = new ResponsiveMetricPanel
        {
            Gap = 10d,
            MinItemWidth = 160d
        };

        foreach (var metric in metrics)
        {
            var cell = new Border
            {
                CornerRadius = new CornerRadius(5d),
                Padding = new Thickness(13d, 12d, 13d, 11d),
                MinHeight = 88d
            };
            cell.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
            cell.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
            cell.BorderThickness = new Thickness(ModBase.GetWPFSize(1d));

            var stack = new StackPanel();
            stack.Children.Add(Text(metric.Value, 20, FontWeights.SemiBold));
            stack.Children.Add(Text(MinecraftCrashUi.Text(metric.TitleKey), 12));
            if (!string.IsNullOrWhiteSpace(metric.DescriptionKey))
                stack.Children.Add(Text(MinecraftCrashUi.Text(metric.DescriptionKey), 12));
            cell.Child = stack;
            panel.Children.Add(cell);
        }

        return MinecraftCrashUi.CreateCard("Crash.Overview.Card.Metrics", panel);
    }

    public static MyCard CreateDiagnosisCard(CrashPresentationDiagnosis diagnosis, bool primary)
    {
        var root = new StackPanel();

        var header = new Grid { Margin = new Thickness(0d, 0d, 0d, 12d) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var icon = _CreateCategoryIcon(diagnosis.Category);
        icon.Margin = new Thickness(0d, 1d, 14d, 0d);
        icon.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(icon, 0);
        header.Children.Add(icon);

        var content = new Grid { VerticalAlignment = VerticalAlignment.Center };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleRow = new Grid { VerticalAlignment = VerticalAlignment.Center };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Text(
            MinecraftCrashUi.Text(diagnosis.TitleKey, diagnosis.Parameters),
            primary ? 18 : 16,
            FontWeights.SemiBold
        );
        title.Margin = new Thickness(0d);
        title.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(title, 0);
        titleRow.Children.Add(title);

        var badgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12d, 0d, 0d, 0d)
        };
        badgeRow.Children.Add(CreateConfidenceBadge(diagnosis.Confidence, true));
        badgeRow.Children.Add(CreateSeverityBadge(diagnosis.Severity, true));
        Grid.SetColumn(badgeRow, 1);
        titleRow.Children.Add(badgeRow);

        Grid.SetRow(titleRow, 0);
        content.Children.Add(titleRow);

        var scoreLine = _CreateScoreLineInline(diagnosis.Score);
        Grid.SetRow(scoreLine, 1);
        content.Children.Add(scoreLine);

        Grid.SetColumn(content, 1);
        header.Children.Add(content);
        root.Children.Add(header);

        root.Children.Add(_CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Cause",
            MinecraftCrashUi.Text(diagnosis.CauseKey, diagnosis.Parameters),
            MyHint.Themes.Blue
        ));
        root.Children.Add(_CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Impact",
            MinecraftCrashUi.Text(diagnosis.ImpactKey, diagnosis.Parameters),
            MyHint.Themes.Yellow
        ));
        root.Children.Add(_CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Recommendation",
            MinecraftCrashUi.Text(diagnosis.RecommendationKey, diagnosis.Parameters),
            MyHint.Themes.Blue
        ));

        if (diagnosis.Parameters.Count > 0)
        {
            root.Children.Add(SectionTitle("Crash.Diagnoses.Parameter"));
            root.Children.Add(_CreateParameterChips(diagnosis.Parameters));
        }

        foreach (var note in diagnosis.Notes)
            root.Children.Add(_CreateNote(note));

        if (diagnosis.Evidence.Count > 0)
        {
            root.Children.Add(SectionTitle("Crash.Diagnoses.RelatedEvidence",
                new Dictionary<string, string>
                {
                    ["Count"] = diagnosis.Evidence.Count.ToString()
                }
            ));

            var evidenceStack = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 4d) };
            foreach (var evidence in diagnosis.Evidence.Take(primary ? 4 : 2))
                evidenceStack.Children.Add(CreateEvidenceItem(evidence, false));
            root.Children.Add(evidenceStack);
        }

        var actionRow = CreateActionButtonRow(diagnosis.Actions.Take(3));
        if (actionRow.Children.Count > 0) root.Children.Add(actionRow);
        return MinecraftCrashUi.CreateCard(primary
            ? "Crash.Diagnoses.Primary"
            : "Crash.Diagnoses.Secondary", root);
    }

    public static FrameworkElement CreateActionListItem(CrashPresentationAction action, int index)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            IsScaleAnimationEnabled = false,
            PaddingLeft = 4,
            MinPaddingRight = 35,
            SvgIcon = _SvgIconForAction(action.Kind),
            Title = index <= 0
                ? MinecraftCrashUi.Text(action.TitleKey, action.Parameters)
                : index + ". " + MinecraftCrashUi.Text(action.TitleKey, action.Parameters),
            Info = string.IsNullOrWhiteSpace(action.DescriptionKey)
                ? ""
                : MinecraftCrashUi.Text(action.DescriptionKey!, action.Parameters)
        };
        item.Click += (_, _) => MinecraftCrashUi.ExecuteAction(action);
        return item;
    }

    public static FrameworkElement CreateEvidenceItem(
        CrashPresentationEvidence evidence,
        bool showDetail = true)
    {
        var root = new Border
        {
            CornerRadius = new CornerRadius(5d),
            Padding = new Thickness(12d, 10d, 12d, 8d),
            Margin = new Thickness(0d, 0d, 0d, 10d)
        };
        root.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
        root.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
        root.BorderThickness = new Thickness(ModBase.GetWPFSize(1d));

        var stack = new StackPanel();
        var head = new WrapPanel { Margin = new Thickness(0d, 0d, 0d, 3d) };
        head.Children.Add(Tag("+" + evidence.Weight, true));
        head.Children.Add(Tag(evidence.SourceName ?? evidence.SourceKind.ToString(), false));
        if (evidence.LineNumber is not null)
            head.Children.Add(Tag(
                MinecraftCrashUi.Text("Crash.Evidence.Line") + " " + evidence.LineNumber,
                false
            ));
        head.Children.Add(Tag(MinecraftCrashUi.Text(evidence.TitleKey), false));
        stack.Children.Add(head);

        stack.Children.Add(Text(string.IsNullOrWhiteSpace(evidence.Summary)
                ? MinecraftCrashUi.Text(evidence.TitleKey)
                : evidence.Summary,
            13
        ));
        if (showDetail && !string.IsNullOrWhiteSpace(evidence.Detail))
            stack.Children.Add(Code(evidence.Detail));
        root.Child = stack;

        return root;
    }

    public static FrameworkElement CreateFactItem(CrashPresentationFact fact)
    {
        var root = new Border
        {
            CornerRadius = new CornerRadius(5d),
            Padding = new Thickness(12d, 9d, 12d, 8d),
            Margin = new Thickness(0d, 0d, 0d, 8d)
        };
        root.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
        root.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
        root.BorderThickness = new Thickness(ModBase.GetWPFSize(1d));

        var stack = new StackPanel();
        var tags = new WrapPanel { Margin = new Thickness(0d, 0d, 0d, 3d) };
        tags.Children.Add(Tag(MinecraftCrashUi.Text(fact.TitleKey), true));
        tags.Children.Add(Tag(fact.SourceName ?? fact.SourceKind.ToString(), false));
        if (fact.LineNumber is not null)
            tags.Children.Add(Tag("#" + fact.LineNumber, false));
        stack.Children.Add(tags);

        stack.Children.Add(Text(fact.Value, 13));
        if (!string.IsNullOrWhiteSpace(fact.Excerpt))
            stack.Children.Add(Code(CrashText.TrimPreview(fact.Excerpt, 5, 900)));
        root.Child = stack;

        return root;
    }

    public static MyCard CreateLogCard(CrashPresentationLogSource log)
    {
        var root = new StackPanel();
        var head = new Grid { Margin = new Thickness(0d, 0d, 0d, 5d) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = Text(log.Name, 15.5, FontWeights.SemiBold);
        head.Children.Add(title);

        var tags = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        tags.Children.Add(Tag(log.Kind.ToString(), false));
        tags.Children.Add(Tag(_LogRoleText(log), log.UsedForAnalysis));
        Grid.SetColumn(tags, 1);
        head.Children.Add(tags);
        root.Children.Add(head);

        root.Children.Add(CreateKeyValueGrid([
            (MinecraftCrashUi.Text("Crash.Logs.Kind"), log.Kind.ToString()),
            (MinecraftCrashUi.Text("Crash.Logs.Size"), MinecraftCrashUi.FormatBytes(log.Length ?? 0)),
            (MinecraftCrashUi.Text("Crash.Logs.Path"), string.IsNullOrWhiteSpace(log.FullPath) ? "-" : log.FullPath!)
        ]));

        if (!string.IsNullOrWhiteSpace(log.Preview))
        {
            root.Children.Add(SectionTitle("Crash.Logs.Preview"));
            root.Children.Add(Code(log.Preview));
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };
        var open = IconButton(
            MinecraftCrashUi.Text("Crash.Logs.OpenThisLog"),
            "lucide/external-link",
            CrashActionPriority.Primary
        );
        open.Click += (_, _) => MinecraftCrashUi.OpenLog(log);
        row.Children.Add(open);

        var copy = IconButton(
            MinecraftCrashUi.Text("Crash.Logs.CopyPreview"),
            "lucide/copy",
            CrashActionPriority.Secondary
        );
        copy.Margin = new Thickness(10d, 0d, 0d, 0d);
        copy.Click += (_, _) => MinecraftCrashUi.CopyLogPreview(log);
        row.Children.Add(copy);

        root.Children.Add(row);

        return MinecraftCrashUi.CreateCard("Crash.Logs.Card", root);
    }

    public static FrameworkElement CreateLogSummaryItem(CrashPresentationLogSource log)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            IsScaleAnimationEnabled = false,
            PaddingLeft = 4,
            MinPaddingRight = 35,
            SvgIcon = "lucide/scroll-text",
            Title = log.Name,
            Info = $"{log.Kind} · {_LogRoleText(log)} · {MinecraftCrashUi.FormatBytes(log.Length ?? 0)}"
        };
        item.Click += (_, _) => MinecraftCrashUi.OpenLog(log);
        return item;
    }

    private static string _LogRoleText(CrashPresentationLogSource log)
    {
        return log.AnalysisRole switch
        {
            CrashLogAnalysisRole.Primary => MinecraftCrashUi.Text("Crash.Logs.Primary"),
            CrashLogAnalysisRole.Supporting => MinecraftCrashUi.Text("Crash.Logs.Supporting"),
            _ => MinecraftCrashUi.Text("Crash.Logs.ReportOnly")
        };
    }

    public static MyCard CreateEnvironmentGroup(
        string titleKey,
        IEnumerable<CrashPresentationEnvironmentItem> items,
        bool showSensitive)
    {
        var materialized = items
            .Where(item => showSensitive || !item.IsSensitive)
            .ToList();
        var root = new StackPanel();
        root.Children.Add(CreateKeyValueGrid(materialized.Select(item => (
            MinecraftCrashUi.Text(item.NameKey),
            item.IsSensitive && !showSensitive
                ? MinecraftCrashUi.Text("Crash.Environment.Sensitive")
                : item.Value
        ))));
        if (materialized.Any(static item => item.IsSensitive))
            root.Children.Add(CreateHint(
                MinecraftCrashUi.Text("Crash.Environment.SensitiveHint"),
                MyHint.Themes.Yellow
            ));

        return MinecraftCrashUi.CreateCard(titleKey, root);
    }

    public static FrameworkElement CreateKeyValueGrid(IEnumerable<(string Key, string Value)> items)
    {
        var grid = new Grid { Margin = new Thickness(0d, 2d, 0d, 4d) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var row = 0;
        foreach (var item in items)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var key = Text(item.Key, 13);
            key.Opacity = 0.72d;
            key.Margin = new Thickness(0d, 0d, 24d, 7d);
            Grid.SetRow(key, row);
            grid.Children.Add(key);

            var value = Text(item.Value, 13);
            value.Margin = new Thickness(0d, 0d, 0d, 7d);
            Grid.SetColumn(value, 1);
            Grid.SetRow(value, row);
            grid.Children.Add(value);

            row++;
        }

        return grid;
    }

    public static Border CreateConfidenceBadge(CrashDiagnosisConfidence confidence, bool compact = false)
    {
        return Tag(MinecraftCrashUi.ConfidenceText(confidence),
            confidence is CrashDiagnosisConfidence.Certain or CrashDiagnosisConfidence.High,
            compact ? new Thickness(0d, 0d, 6d, 0d) : null);
    }

    public static Border CreateSeverityBadge(CrashDiagnosisSeverity severity, bool compact = false)
    {
        return Tag(MinecraftCrashUi.Text("Crash.Severity." + severity), severity == CrashDiagnosisSeverity.Error,
            compact ? new Thickness(0d) : null);
    }

    public static FrameworkElement CreateScoreBar(int score)
    {
        var clampedScore = Math.Max(0, Math.Min(100, score));
        var finishedWeight = Math.Max(0.001d, clampedScore);
        var unfinishedWeight = Math.Max(0.001d, 100 - clampedScore);

        var root = new Grid
        {
            Height = 4d,
            MinWidth = 120d,
            Margin = new Thickness(0d),
            SnapsToDevicePixels = true,
            ClipToBounds = true
        };
        root.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(finishedWeight, GridUnitType.Star)
        });
        root.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(unfinishedWeight, GridUnitType.Star)
        });

        var finished = new Rectangle
        {
            RadiusX = 2d,
            RadiusY = 2d
        };
        finished.SetResourceReference(Shape.FillProperty, "ColorBrush2");
        Grid.SetColumn(finished, 0);
        root.Children.Add(finished);

        var unfinished = new Rectangle
        {
            RadiusX = 2d,
            RadiusY = 2d,
            Opacity = 0.6d
        };
        unfinished.SetResourceReference(Shape.FillProperty, "ColorBrush6");
        Grid.SetColumn(unfinished, 1);
        root.Children.Add(unfinished);

        return root;
    }

    public static Border Tag(string text, bool highlight, Thickness? margin = null)
    {
        var label = Text(text, 11, highlight ? FontWeights.SemiBold : null);
        label.Margin = new Thickness(0d);
        label.LineHeight = 14d;
        label.SetResourceReference(TextBlock.ForegroundProperty, highlight ? "ColorBrush2" : "ColorBrushGray2");

        var badge = new Border
        {
            CornerRadius = new CornerRadius(3d),
            Padding = new Thickness(5d, 1.5d, 5d, 1.5d),
            Margin = margin ?? new Thickness(0d, 0d, 5d, 5d),
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Child = label,
            Background = new SolidColorBrush(ThemeManager.IsDarkMode
                ? Color.FromArgb(highlight ? (byte)34 : (byte)18, 255, 255, 255)
                : Color.FromArgb(highlight ? (byte)22 : (byte)14, 0, 0, 0))
        };

        return badge;
    }

    public static TextBlock Text(string text, double fontSize = 14, FontWeight? weight = null)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = weight ?? FontWeights.Normal,
            Margin = new Thickness(0d, 0d, 0d, 7d)
        };
    }

    public static FrameworkElement Code(string text)
    {
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10d),
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        box.SetResourceReference(Control.BackgroundProperty, "ColorBrush7");
        box.SetResourceReference(Control.ForegroundProperty, "ColorBrush1");

        var border = new Border
        {
            Child = new MyScrollViewer { Content = box, MaxHeight = 220d, Margin = new Thickness(0, 4, 0, 8) },
            BorderThickness = new Thickness(ModBase.GetWPFSize(1d)),
            CornerRadius = new CornerRadius(5d),
        };
        border.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
        border.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");

        return border;
    }

    public static MyIconTextButton IconButton(string text, string svgIcon, CrashActionPriority priority)
    {
        return new MyIconTextButton
        {
            Text = text,
            SvgIcon = svgIcon,
            ColorType = priority == CrashActionPriority.Primary
                ? MyIconTextButton.ColorState.Highlight
                : MyIconTextButton.ColorState.Black
        };
    }

    public static StackPanel CreateActionButtonRow(IEnumerable<CrashPresentationAction> actions)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0d, 8d, 0d, 0d)
        };

        foreach (var action in actions)
        {
            var button = IconButton(MinecraftCrashUi.Text(action.TitleKey, action.Parameters),
                _SvgIconForAction(action.Kind), action.Priority);
            button.Margin = new Thickness(0d, 0d, 10d, 0d);
            button.Click += (_, _) => MinecraftCrashUi.ExecuteAction(action);
            row.Children.Add(button);
        }

        return row;
    }

    public static MyHint CreateHint(string text, MyHint.Themes theme)
    {
        return new MyHint
        {
            Text = text,
            Theme = theme,
            HasBorder = true,
            Margin = new Thickness(0d, 4d, 0d, 10d)
        };
    }

    public static FrameworkElement CreateInlineNotice(string text, bool highlight)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(5d),
            Padding = new Thickness(11d, 8d, 11d, 8d),
            Margin = new Thickness(0d, 2d, 0d, 10d)
        };
        border.SetResourceReference(Border.BackgroundProperty, highlight ? "ColorBrushBg1" : "ColorBrush7");
        border.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
        border.BorderThickness = new Thickness(ModBase.GetWPFSize(1d));
        border.Child = Text(text, 13);
        return border;
    }

    public static FrameworkElement SectionTitle(
        string key,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        var title = Text(MinecraftCrashUi.Text(key, parameters), 13, FontWeights.SemiBold);
        title.Margin = new Thickness(0d, 6d, 0d, 5d);

        return title;
    }

    private static FrameworkElement _CreateScoreLineInline(int score)
    {
        var root = new Grid
        {
            Margin = new Thickness(0d, 8d, 0d, 0d),
            VerticalAlignment = VerticalAlignment.Center
        };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });

        var label = Text(MinecraftCrashUi.Text("Crash.Diagnoses.Score") + " " + score, 12);
        label.Opacity = 0.76d;
        label.Margin = new Thickness(0d, 0d, 12d, 0d);
        label.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 0);
        root.Children.Add(label);

        var bar = CreateScoreBar(score);
        bar.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(bar, 1);
        root.Children.Add(bar);

        return root;
    }

    private static FrameworkElement _CreateDescriptionBlock(string titleKey, string text, MyHint.Themes theme)
    {
        if (string.IsNullOrWhiteSpace(text) || text == titleKey) return new Grid();

        var root = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 9d) };
        root.Children.Add(SectionTitle(titleKey));
        if (titleKey == "Crash.Diagnosis.Part.Recommendation")
        {
            root.Children.Add(CreateHint(text, theme));
        }
        else
        {
            var paragraph = Text(text, 13.5);
            paragraph.LineHeight = 21d;
            paragraph.Margin = new Thickness(0d, 0d, 0d, 4d);
            root.Children.Add(paragraph);
        }

        return root;
    }

    private static FrameworkElement _CreateParameterChips(IReadOnlyDictionary<string, string> parameters)
    {
        var panel = new WrapPanel { Margin = new Thickness(0d, 2d, 0d, 8d) };
        foreach (var pair in parameters)
            if (!string.IsNullOrWhiteSpace(pair.Value))
                panel.Children.Add(Tag(pair.Key + ": " + pair.Value, true));

        return panel;
    }

    private static FrameworkElement _CreateNote(CrashDiagnosisNote note)
    {
        return CreateHint(MinecraftCrashUi.Text(note.Key, note.Parameters), MyHint.Themes.Yellow);
    }

    private static FrameworkElement _CreateCategoryIcon(CrashDiagnosisCategory category)
    {
        var border = new Border
        {
            Width = 32d,
            Height = 32d,
            CornerRadius = new CornerRadius(16d),
            Padding = new Thickness(8d),
            VerticalAlignment = VerticalAlignment.Top
        };
        border.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
        border.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
        border.BorderThickness = new Thickness(ModBase.GetWPFSize(1d));

        var icon = new SvgIcon
        {
            Icon = _SvgIconForCategory(category),
            Stretch = Stretch.Uniform,
            StrokeThickness = 2.2d
        };
        icon.SetResourceReference(SvgIcon.IconBrushProperty, "ColorBrush2");
        border.Child = icon;

        return border;
    }

    private static string _SvgIconForCategory(CrashDiagnosisCategory category)
    {
        return category switch
        {
            CrashDiagnosisCategory.Runtime =>
                "lucide/computer",
            CrashDiagnosisCategory.Graphics =>
                "lucide/monitor-cog",
            CrashDiagnosisCategory.ModLoader =>
                "lucide/puzzle",
            CrashDiagnosisCategory.Mod =>
                "lucide/puzzle",
            CrashDiagnosisCategory.GameContent =>
                "lucide/grid-2x2",
            CrashDiagnosisCategory.Native =>
                "lucide/coffee",
            CrashDiagnosisCategory.Launcher =>
                "lucide/rocket",
            _ => "lucide/info"
        };
    }

    private static string _SvgIconForAction(CrashPresentationActionKind kind)
    {
        return kind switch
        {
            CrashPresentationActionKind.OpenLog =>
                "lucide/scroll-text",
            CrashPresentationActionKind.ExportMarkdown =>
                "lucide/file-output",
            CrashPresentationActionKind.ExportReport =>
                "lucide/archive-restore",
            CrashPresentationActionKind.OpenJavaSettings =>
                "lucide/coffee",
            CrashPresentationActionKind.OpenMemorySettings =>
                "lucide/gauge",
            CrashPresentationActionKind.OpenInstanceModsFolder =>
                "lucide/folder-open",
            CrashPresentationActionKind.OpenInstanceSettings =>
                "lucide/settings",
            CrashPresentationActionKind.OpenResourcePackFolder =>
                "lucide/image",
            _ => "lucide/external-link"
        };
    }

    private sealed class ResponsiveMetricPanel : Panel
    {
        public double Gap { get; set; } = 10d;
        public double MinItemWidth { get; set; } = 160d;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count == 0) return new Size();

            var availableWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0d
                ? MinItemWidth
                : availableSize.Width;
            var columns = _CalculateColumns(availableWidth, InternalChildren.Count);
            var itemWidth = _CalculateItemWidth(availableWidth, columns);
            var rowHeights = _MeasureRows(itemWidth, columns);
            var height = rowHeights.Sum() + Math.Max(0, rowHeights.Count - 1) * Gap;

            return new Size(availableWidth, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count == 0) return finalSize;

            var columns = _CalculateColumns(finalSize.Width, InternalChildren.Count);
            var itemWidth = _CalculateItemWidth(finalSize.Width, columns);
            var rowHeights = _MeasureRows(itemWidth, columns);
            var y = 0d;

            for (var index = 0; index < InternalChildren.Count; index++)
            {
                var row = index / columns;
                var column = index % columns;
                var x = column * (itemWidth + Gap);
                InternalChildren[index].Arrange(new Rect(x, y, itemWidth, rowHeights[row]));

                if (column == columns - 1)
                    y += rowHeights[row] + Gap;
            }

            return finalSize;
        }

        private int _CalculateColumns(double width, int count)
        {
            if (count <= 0) return 1;
            if (width <= 0d) return 1;

            return Math.Max(1, Math.Min(count, (int)Math.Floor((width + Gap) / (MinItemWidth + Gap))));
        }

        private double _CalculateItemWidth(double width, int columns)
        {
            columns = Math.Max(1, columns);
            return Math.Max(MinItemWidth, Math.Floor((width - (columns - 1) * Gap) / columns));
        }

        private List<double> _MeasureRows(double itemWidth, int columns)
        {
            var rows = new List<double>();
            for (var index = 0; index < InternalChildren.Count; index++)
            {
                var child = InternalChildren[index];
                child.Measure(new Size(itemWidth, double.PositiveInfinity));
                var row = index / columns;
                while (rows.Count <= row) rows.Add(0d);
                rows[row] = Math.Max(rows[row], child.DesiredSize.Height);
            }

            return rows;
        }
    }
}