using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public static class MinecraftCrashVisualFactory
{
    private const string IconOpen =
        "F1 M4 4h9v2H6v12h12v-7h2v9H4V4Zm12 0h4v4h-2V7.414l-7.293 7.293-1.414-1.414L16.586 6H16V4Z";

    private const string IconCopy =
        "M394.666667 106.666667h448a74.666667 74.666667 0 0 1 74.666666 74.666666v448a74.666667 74.666667 0 0 1-74.666666 74.666667H394.666667a74.666667 74.666667 0 0 1-74.666667-74.666667V181.333333a74.666667 74.666667 0 0 1 74.666667-74.666666z m0 64a10.666667 10.666667 0 0 0-10.666667 10.666666v448a10.666667 10.666667 0 0 0 10.666667 10.666667h448a10.666667 10.666667 0 0 0 10.666666-10.666667V181.333333a10.666667 10.666667 0 0 0-10.666666-10.666666H394.666667z m245.333333 597.333333a32 32 0 0 1 64 0v74.666667a74.666667 74.666667 0 0 1-74.666667 74.666666H181.333333a74.666667 74.666667 0 0 1-74.666666-74.666666V394.666667a74.666667 74.666667 0 0 1 74.666666-74.666667h74.666667a32 32 0 0 1 0 64h-74.666667a10.666667 10.666667 0 0 0-10.666666 10.666667v448a10.666667 10.666667 0 0 0 10.666666 10.666666h448a10.666667 10.666667 0 0 0 10.666667-10.666666v-74.666667z";

    private const string IconDownload =
        "M955 610h-59c-15 0-29 13-29 29v196c0 15-13 29-29 29h-649c-15 0-29-13-29-29v-196c0-15-13-29-29-29h-59c-15 0-29 13-29 29V905c0 43 35 78 78 78h787c43 0 78-35 78-78V640c0-15-13-29-29-29zM492 740c11 11 29 11 41 0l265-265c11-11 11-29 0-41l-41-41c-11-11-29-11-41 0l-110 110c-11 11-33 3-33-13V68C571 53 555 39 541 39h-59c-15 0-29 13-29 29v417c0 17-21 25-33 13l-110-110c-11-11-29-11-41 0L226 433c-11 11-11 29 0 41L492 740z";

    private const string IconSettings =
        "M940.4 463.7L773.3 174.2c-17.3-30-49.2-48.4-83.8-48.4H340.2c-34.6 0-66.5 18.5-83.8 48.4L89.2 463.7c-17.3 30-17.3 66.9 0 96.8L256.4 850c17.3 30 49.2 48.4 83.8 48.4h349.2c34.6 0 66.5-18.5 83.8-48.4l167.2-289.5c17.3-29.9 17.3-66.8 0-96.8z m-94.6 96.8L725.9 768.1c-17.3 30-49.2 48.4-83.8 48.4H387.5c-34.6 0-66.5-18.5-83.8-48.4L183.9 560.5c-17.3-30-17.3-66.9 0-96.8l119.8-207.5c17.3-30 49.2-48.4 83.8-48.4h254.6c34.6 0 66.5 18.5 83.8 48.4l119.8 207.5c17.3 30 17.3 66.9 0.1 96.8z M522.3 321.2c-2.5-0.1-5-0.2-7.5-0.2-119.9 0-214 110.3-186.3 235 15.8 70.9 71.5 126.6 142.4 142.4 17.5 3.9 34.7 5.4 51.4 4.7 102.1-3.9 183.6-87.9 183.6-191 0.1-103-81.5-187-183.6-190.9z m68.6 269.1c-18.5 18-43 28.9-68.6 30.7l-6 0.3c-30.2 0.4-58.6-11.4-79.7-33-19.5-20.1-30.7-47-30.9-75-0.3-29.6 11.1-57.4 32-78.3 20.6-20.6 48-32 77.2-32 2.5 0 5 0.1 7.5 0.3 26.7 1.8 51.5 13.2 70.5 32.5 19.6 20 30.8 46.9 31.2 74.9 0.2 30.2-11.5 58.6-33.2 79.6z";

    private const string IconEvidence =
        "M925.866667 669.866667 925.866667 669.866667l-298.666667 298.666667 0 0C620.8 977.066667 610.133333 981.333333 597.333333 981.333333L170.666667 981.333333c-46.933333 0-85.333333-38.4-85.333333-85.333333L85.333333 128c0-46.933333 38.4-85.333333 85.333333-85.333333l682.666667 0c46.933333 0 85.333333 38.4 85.333333 85.333333l0 512C938.666667 652.8 934.4 663.466667 925.866667 669.866667zM640 836.266667l153.6-153.6L661.333333 682.666667c-12.8 0-21.333333 8.533333-21.333333 21.333333L640 836.266667zM832 128 192 128C179.2 128 170.666667 136.533333 170.666667 149.333333l0 725.333333c0 12.8 8.533333 21.333333 21.333333 21.333333l362.666667 0L554.666667 682.666667c0-46.933333 38.4-85.333333 85.333333-85.333333l213.333333 0L853.333333 149.333333C853.333333 136.533333 844.8 128 832 128zM725.333333 469.333333 298.666667 469.333333c-23.466667 0-42.666667-19.2-42.666667-42.666667 0-23.466667 19.2-42.666667 42.666667-42.666667l426.666667 0c23.466667 0 42.666667 19.2 42.666667 42.666667C768 450.133333 748.8 469.333333 725.333333 469.333333zM725.333333 298.666667 298.666667 298.666667c-23.466667 0-42.666667-19.2-42.666667-42.666667s19.2-42.666667 42.666667-42.666667l426.666667 0c23.466667 0 42.666667 19.2 42.666667 42.666667S748.8 298.666667 725.333333 298.666667z";

    private const string IconLog =
        "M 4 2 C 3.27778 2 2.54212 2.23535 1.96094 2.75195 C 1.37976 3.26856 1 4.08333 1 5 v 2 c 0 1.09272 0.907275 2 2 2 h 2 v 10 c 0 0.916666 0.379756 1.73144 0.960938 2.24805 C 6.54212 21.7647 7.27778 22 8 22 h 12 c 1.64501 0 3 -1.35499 3 -3 v -1 c 0 -1.09272 -0.907275 -2 -2 -2 H 20 V 5 C 20 3.35499 18.645 2 17 2 Z M 4 4 C 4.27778 4 4.54212 4.09799 4.71094 4.24805 C 4.87976 4.39811 5 4.58333 5 5 V 7 H 3 V 5 C 3 4.58333 3.12024 4.39811 3.28906 4.24805 C 3.45788 4.09799 3.72222 4 4 4 Z M 6.79492 4 H 17 c 0.564129 0 1 0.435871 1 1 v 11 h -7 c -1.09272 0 -2 0.907275 -2 2 v 1 C 9 19.4167 8.87976 19.6019 8.71094 19.752 C 8.54212 19.902 8.27778 20 8 20 C 7.72222 20 7.45788 19.902 7.28906 19.752 C 7.12024 19.6019 7 19.4167 7 19 V 8 V 5 C 7 4.64011 6.90114 4.31648 6.79492 4 Z M 10 7 a 1 1 0 0 0 -1 1 a 1 1 0 0 0 1 1 h 5 A 1 1 0 0 0 16 8 A 1 1 0 0 0 15 7 Z m 0 4 a 1 1 0 0 0 -1 1 a 1 1 0 0 0 1 1 h 5 a 1 1 0 0 0 1 -1 a 1 1 0 0 0 -1 -1 z m 1 7 h 8 h 2 v 1 c 0 0.564129 -0.435871 1 -1 1 H 10.7949 C 10.9011 19.6835 11 19.3599 11 19 Z";

    public static MyCard CreateHeroCard(MinecraftCrashSession session)
    {
        var presentation = session.Presentation;
        var top = presentation.Diagnoses.FirstOrDefault();
        var root = new StackPanel();
        var hero = new Grid { Margin = new Thickness(0d, 0d, 0d, 10d) };

        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(Text(
            MinecraftCrashUi.Text(presentation.Summary.TitleKey, presentation.Summary.Parameters),
            20, FontWeights.SemiBold
        ));
        text.Children.Add(Text(
            MinecraftCrashUi.Text(presentation.Summary.DescriptionKey, presentation.Summary.Parameters),
            13.5
        ));
        if (!string.IsNullOrWhiteSpace(presentation.Summary.DetailKey))
            text.Children.Add(Text(
                MinecraftCrashUi.Text(presentation.Summary.DetailKey!, presentation.Summary.Parameters),
                13
            ));
        hero.Children.Add(text);

        if (top is not null)
        {
            var status = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            status.Children.Add(CreateConfidenceBadge(top.Confidence));
            status.Children.Add(CreateSeverityBadge(top.Severity));
            Grid.SetColumn(status, 1);
            hero.Children.Add(status);
        }

        root.Children.Add(hero);
        if (top is not null)
        {
            var strip = new WrapPanel { Margin = new Thickness(0d, 2d, 0d, 0d) };
            strip.Children.Add(Tag(
                MinecraftCrashUi.Text("Crash.Diagnoses.Score") + " " + top.Score, true)
            );
            strip.Children.Add(Tag(
                MinecraftCrashUi.Text("Crash.Diagnoses.Category") + " " +
                MinecraftCrashUi.Text("Crash.Category." + top.Category),
                false
            ));
            strip.Children.Add(Tag(
                MinecraftCrashUi.Text("Crash.Diagnoses.RelatedEvidence", new Dictionary<string, string>
                {
                    ["Count"] = top.Evidence.Count.ToString()
                }),
                false
            ));
            root.Children.Add(strip);

            root.Children.Add(CreateScoreBar(top.Score));
            root.Children.Add(CreateHint(
                MinecraftCrashUi.Text(top.RecommendationKey, top.Parameters),
                MyHint.Themes.Blue
            ));
        }

        return MinecraftCrashUi.CreateCard("Crash.Overview.Card.Hero", root);
    }

    public static MyCard CreateMetricGrid(IReadOnlyList<CrashPresentationMetric> metrics)
    {
        var grid = new UniformGrid
        {
            Columns = Math.Min(4, Math.Max(1, metrics.Count)),
            Margin = new Thickness(0d)
        };

        foreach (var metric in metrics)
        {
            var cell = new Border
            {
                CornerRadius = new CornerRadius(5d),
                Padding = new Thickness(13d, 12d, 13d, 11d),
                Margin = new Thickness(0d, 0d, 10d, 10d)
            };
            cell.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");

            var stack = new StackPanel();
            stack.Children.Add(Text(metric.Value, 20, FontWeights.SemiBold));
            stack.Children.Add(Text(MinecraftCrashUi.Text(metric.TitleKey), 12));
            if (!string.IsNullOrWhiteSpace(metric.DescriptionKey))
                stack.Children.Add(Text(MinecraftCrashUi.Text(metric.DescriptionKey), 12));
            cell.Child = stack;
            grid.Children.Add(cell);
        }

        return MinecraftCrashUi.CreateCard("Crash.Overview.Card.Metrics", grid);
    }

    public static MyCard CreateDiagnosisCard(CrashPresentationDiagnosis diagnosis, bool primary)
    {
        var root = new StackPanel();

        var titleRow = new Grid { Margin = new Thickness(0d, 0d, 0d, 4d) };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = CreateCategoryIcon(diagnosis.Category);
        icon.Margin = new Thickness(0d, 1d, 10d, 0d);
        titleRow.Children.Add(icon);

        var title = Text(
            MinecraftCrashUi.Text(diagnosis.TitleKey, diagnosis.Parameters),
            primary ? 17 : 15.5,
            FontWeights.SemiBold
        );
        Grid.SetColumn(title, 1);
        titleRow.Children.Add(title);
        var badgeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        badgeRow.Children.Add(CreateConfidenceBadge(diagnosis.Confidence));
        badgeRow.Children.Add(CreateSeverityBadge(diagnosis.Severity));
        Grid.SetColumn(badgeRow, 2);
        titleRow.Children.Add(badgeRow);
        root.Children.Add(titleRow);

        root.Children.Add(CreateScoreLine(diagnosis.Score));
        root.Children.Add(CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Cause",
            MinecraftCrashUi.Text(diagnosis.CauseKey, diagnosis.Parameters),
            MyHint.Themes.Blue
        ));
        root.Children.Add(CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Impact",
            MinecraftCrashUi.Text(diagnosis.ImpactKey, diagnosis.Parameters),
            MyHint.Themes.Yellow
        ));
        root.Children.Add(CreateDescriptionBlock(
            "Crash.Diagnosis.Part.Recommendation",
            MinecraftCrashUi.Text(diagnosis.RecommendationKey, diagnosis.Parameters),
            MyHint.Themes.Blue
        ));

        if (diagnosis.Parameters.Count > 0)
        {
            root.Children.Add(SectionTitle("Crash.Diagnoses.Parameter"));
            root.Children.Add(CreateParameterChips(diagnosis.Parameters));
        }

        foreach (var note in diagnosis.Notes)
            root.Children.Add(CreateNote(note));

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

    public static FrameworkElement CreateActionCard(CrashPresentationAction action, int index)
    {
        return CreateActionListItem(action, index);
    }

    public static FrameworkElement CreateActionListItem(CrashPresentationAction action, int index)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            IsScaleAnimationEnabled = false,
            Height = string.IsNullOrWhiteSpace(action.DescriptionKey) ? 42d : 56d,
            PaddingLeft = 4,
            MinPaddingRight = 35,
            Logo = IconForAction(action.Kind),
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
        root.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");

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
        root.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");

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
        tags.Children.Add(Tag(
            MinecraftCrashUi.Text(log.UsedForAnalysis ? "Crash.Logs.Used" : "Crash.Logs.NotUsed"),
            log.UsedForAnalysis
        ));
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
            IconOpen,
            CrashActionPriority.Primary
        );
        open.Click += (_, _) => MinecraftCrashUi.OpenLog(log);
        row.Children.Add(open);

        var copy = IconButton(
            MinecraftCrashUi.Text("Crash.Logs.CopyPreview"),
            IconCopy,
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
            Height = 48d,
            PaddingLeft = 4,
            MinPaddingRight = 35,
            Logo = IconLog,
            Title = log.Name,
            Info = log.Kind + " · " + FormatLogState(log) + " · " + MinecraftCrashUi.FormatBytes(log.Length ?? 0)
        };
        item.Click += (_, _) => MinecraftCrashUi.OpenLog(log);
        return item;
    }

    private static string FormatLogState(CrashPresentationLogSource log)
    {
        return MinecraftCrashUi.Text(log.UsedForAnalysis ? "Crash.Logs.Used" : "Crash.Logs.NotUsed");
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

    public static Border CreateConfidenceBadge(CrashDiagnosisConfidence confidence)
    {
        return Tag(MinecraftCrashUi.ConfidenceText(confidence),
            confidence is CrashDiagnosisConfidence.Certain or CrashDiagnosisConfidence.High);
    }

    public static Border CreateSeverityBadge(CrashDiagnosisSeverity severity)
    {
        return Tag(MinecraftCrashUi.Text("Crash.Severity." + severity), severity == CrashDiagnosisSeverity.Error);
    }

    public static FrameworkElement CreateScoreBar(int score)
    {
        var root = new Grid { Margin = new Thickness(0d, 6d, 0d, 10d) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var outer = new Border
        {
            Height = 7d,
            CornerRadius = new CornerRadius(3.5d),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        outer.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");

        var inner = new Border
        {
            Width = Math.Max(8d, Math.Min(100d, score)) * 2.1d,
            CornerRadius = new CornerRadius(3.5d),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        inner.SetResourceReference(Border.BackgroundProperty, "ColorBrush2");

        outer.Child = inner;
        root.Children.Add(outer);

        var scoreText = Text(score.ToString(), 12, FontWeights.SemiBold);
        scoreText.Margin = new Thickness(10d, 0d, 0d, 0d);
        Grid.SetColumn(scoreText, 1);
        root.Children.Add(scoreText);

        return root;
    }

    public static Border Tag(string text, bool highlight)
    {
        var label = Text(text, 11.5, highlight ? FontWeights.SemiBold : null);
        label.Margin = new Thickness(0d);

        var badge = new Border
        {
            CornerRadius = new CornerRadius(10d),
            Padding = new Thickness(8d, 2d, 8d, 2d),
            Margin = new Thickness(0d, 0d, 6d, 6d),
            Child = label
        };
        badge.SetResourceReference(Border.BackgroundProperty, highlight ? "ColorBrush6" : "ColorBrushHalfWhite");

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

    public static TextBox Code(string text)
    {
        var box = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            BorderThickness = new Thickness(0d),
            Padding = new Thickness(10d),
            MaxHeight = 220d,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0d, 4d, 0d, 8d)
        };
        box.SetResourceReference(Control.BackgroundProperty, "ColorBrushHalfWhite");
        box.SetResourceReference(Control.ForegroundProperty, "ColorBrush1");

        return box;
    }

    public static MyIconTextButton IconButton(string text, string logo, CrashActionPriority priority)
    {
        return new MyIconTextButton
        {
            Text = text,
            Logo = logo,
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
                IconForAction(action.Kind), action.Priority);
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

    public static FrameworkElement SectionTitle(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var title = Text(MinecraftCrashUi.Text(key, parameters), 13, FontWeights.SemiBold);
        title.Margin = new Thickness(0d, 6d, 0d, 5d);

        return title;
    }

    private static FrameworkElement CreateScoreLine(int score)
    {
        var root = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 2d) };
        var label = Text(MinecraftCrashUi.Text("Crash.Diagnoses.Score") + " " + score, 12);
        label.Opacity = 0.75d;
        root.Children.Add(label);
        root.Children.Add(CreateScoreBar(score));

        return root;
    }

    private static FrameworkElement CreateDescriptionBlock(string titleKey, string text, MyHint.Themes theme)
    {
        if (string.IsNullOrWhiteSpace(text) || text == titleKey) return new Grid();

        var root = new StackPanel { Margin = new Thickness(0d, 0d, 0d, 8d) };
        root.Children.Add(SectionTitle(titleKey));
        root.Children.Add(CreateHint(text, theme));

        return root;
    }

    private static FrameworkElement CreateParameterChips(IReadOnlyDictionary<string, string> parameters)
    {
        var panel = new WrapPanel { Margin = new Thickness(0d, 2d, 0d, 8d) };
        foreach (var pair in parameters)
            if (!string.IsNullOrWhiteSpace(pair.Value))
                panel.Children.Add(Tag(pair.Key + ": " + pair.Value, true));

        return panel;
    }

    private static FrameworkElement CreateNote(CrashDiagnosisNote note)
    {
        return CreateHint(MinecraftCrashUi.Text(note.Key, note.Parameters), MyHint.Themes.Yellow);
    }

    private static FrameworkElement CreateCategoryIcon(CrashDiagnosisCategory category)
    {
        var border = new Border
        {
            Width = 32d,
            Height = 32d,
            CornerRadius = new CornerRadius(16d),
            Padding = new Thickness(8d),
            VerticalAlignment = VerticalAlignment.Top
        };
        border.SetResourceReference(Border.BackgroundProperty, "ColorBrushHalfWhite");

        var path = new Path
        {
            Stretch = Stretch.Uniform,
            Data = (Geometry)new GeometryConverter().ConvertFromString(IconForCategory(category))!
        };
        path.SetResourceReference(Shape.FillProperty, "ColorBrush2");
        border.Child = path;

        return border;
    }

    private static string IconForCategory(CrashDiagnosisCategory category)
    {
        return category switch
        {
            CrashDiagnosisCategory.Runtime =>
                IconSettings,
            CrashDiagnosisCategory.Graphics =>
                "F1 M4 5h16v10H4V5Zm2 2v6h12V7H6Zm3 10h6v2h3v2H6v-2h3v-2Z",
            CrashDiagnosisCategory.ModLoader or CrashDiagnosisCategory.Mod =>
                "F1 M10 2h4v3h5v4h3v4h-3v5h-5v3h-4v-3H5v-5H2V9h3V5h5V2Z",
            CrashDiagnosisCategory.GameContent =>
                "F1 M4 4h16v16H4V4Zm2 2v12h12V6H6Zm2 2h3v3H8V8Zm5 0h3v3h-3V8Zm-5 5h3v3H8v-3Zm5 0h3v3h-3v-3Z",
            CrashDiagnosisCategory.Native =>
                "F1 M7 2h10v4h3v12h-3v4H7v-4H4V6h3V2Zm2 2v16h6V4H9Zm-3 4v8h12V8H6Z",
            _ => IconEvidence
        };
    }

    private static string IconForAction(CrashPresentationActionKind kind)
    {
        return kind switch
        {
            CrashPresentationActionKind.OpenLog =>
                IconLog,
            CrashPresentationActionKind.ExportMarkdown =>
                IconDownload,
            CrashPresentationActionKind.ExportReport =>
                IconDownload,
            CrashPresentationActionKind.OpenJavaSettings =>
                IconSettings,
            CrashPresentationActionKind.OpenMemorySettings =>
                IconSettings,
            CrashPresentationActionKind.OpenInstanceModsFolder =>
                "F1 M10 4l2 2h8v14H4V4h6Zm0 2H6v12h12V8h-7l-1-2Z",
            CrashPresentationActionKind.OpenInstanceSettings =>
                IconSettings,
            CrashPresentationActionKind.OpenResourcePackFolder =>
                "F1 M10 4l2 2h8v14H4V4h6Zm0 2H6v12h12V8h-7l-1-2Z",
            CrashPresentationActionKind.CopyDiagnosisSummary =>
                IconCopy,
            CrashPresentationActionKind.PreviewMarkdown =>
                "F1 M12 5c5.5 0 9.5 5 10 7-.5 2-4.5 7-10 7S2.5 14 2 12c.5-2 4.5-7 10-7Zm0 2c-3.9 0-6.9 3.1-7.8 5 .9 1.9 3.9 5 7.8 5s6.9-3.1 7.8-5c-.9-1.9-3.9-5-7.8-5Zm0 2.5A2.5 2.5 0 1 1 12 14a2.5 2.5 0 0 1 0-5Z",
            _ => IconOpen
        };
    }
}