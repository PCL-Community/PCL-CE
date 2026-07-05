// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public class ExportOption : AvaloniaObject
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ExportOption, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ExportOption, string>(nameof(Description), string.Empty);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string? Rules { get; set; }

    public string? ShowRules { get; set; }

    public bool DefaultChecked { get; set; }

    public bool RequireModLoader { get; set; }

    public bool RequireOptiFine { get; set; }

    public bool RequireModLoaderOrOptiFine { get; set; }
}

public sealed record InstanceExportPageRequest(
    LaunchInstanceInfo Instance,
    string PackageName,
    string PackageVersion,
    IReadOnlyList<string> Rules,
    bool IncludeLauncherFiles,
    bool IncludeLauncherCustom,
    bool IncludeBundleFiles,
    bool ModrinthUploadMode);

public partial class PageInstanceExportRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private List<string>? _rulesOverrides;

    public PageInstanceExportRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        WireWpfCopiedControls();
        SyncRulesOverrideUi();
    }

    public event EventHandler<InstanceExportPageRequest>? ExportRequested;

    public event EventHandler? ImportConfigRequested;

    public event EventHandler<IReadOnlyList<string>>? ExportConfigRequested;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        bool changed = _instance is null ||
                       !string.Equals(_instance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase);
        _instance = instance;
        if (changed)
            RefreshAll();
    }

    public void RefreshAll()
    {
        if (_instance is null)
            return;

        if (this.FindControl<MyTextBox>("TextExportName") is { } nameBox)
        {
            nameBox.Text = string.Empty;
            nameBox.HintText = _instance.Name;
        }

        if (this.FindControl<MyTextBox>("TextExportVersion") is { } versionBox)
        {
            versionBox.Text = string.Empty;
            versionBox.HintText = "1.0.0";
        }

        if (this.FindControl<MyCheckBox>("CheckAdvancedInclude") is { } include)
            include.Checked = false;
        if (this.FindControl<MyCheckBox>("CheckAdvancedModrinth") is { } modrinth)
            modrinth.Checked = false;

        _rulesOverrides = null;
        SyncRulesOverrideUi();
        RefreshAllOptionsUI();
        PanScroll?.ScrollToHome();
    }

    public void ApplyRulesOverride(IEnumerable<string> rules)
    {
        _rulesOverrides = rules
            .Select(rule => rule.Trim())
            .Where(rule => !string.IsNullOrWhiteSpace(rule))
            .ToList();
        SyncRulesOverrideUi();
    }

    private void WireWpfCopiedControls()
    {
        if (this.FindControl<MyExtraTextButton>("BtnExport") is { } exportButton)
            exportButton.Click += (_, _) => StartExport();
        if (this.FindControl<MyButton>("BtnAdvancedImport") is { } importButton)
            importButton.Click += (_, _) => ImportConfigRequested?.Invoke(this, EventArgs.Empty);
        if (this.FindControl<MyButton>("BtnAdvancedExport") is { } exportConfigButton)
            exportConfigButton.Click += (_, _) => ExportConfigRequested?.Invoke(this, CollectRules(includeHidden: false));
        if (this.FindControl<MyTextBox>("TextExportName") is { } nameBox)
            nameBox.GotFocus += (_, _) => FillDefaultNameOnFocus();
        if (this.FindControl<MyIconTextButton>("BtnOverrideCancel") is { } overrideCancel)
            overrideCancel.Click += (_, _) => ResetConfigOverrides();
        WireVisibilityToggle("CheckOptionsMod", "PanOptionsMod");
        WireVisibilityToggle("CheckOptionsResourcePacks", "PanOptionsResourcePacks");
        WireVisibilityToggle("CheckOptionsShaderPacks", "PanOptionsShaderPacks");
        WireVisibilityToggle("CheckOptionsSaves", "PanOptionsSaves");
        WireVisibilityToggle("CheckOptionsPcl", "PanOptionsPcl");
        WireVisibilityToggle("CheckAdvancedInclude", "HintAdvancedInclude");
    }

    private void FillDefaultNameOnFocus()
    {
        if (this.FindControl<MyTextBox>("TextExportName") is not { } nameBox)
            return;

        if (!string.IsNullOrWhiteSpace(nameBox.Text))
            return;

        nameBox.Text = nameBox.HintText;
        nameBox.SelectionStart = nameBox.Text?.Length ?? 0;
    }

    private void StartExport()
    {
        if (_instance is null)
            return;

        string packageName = this.FindControl<MyTextBox>("TextExportName")?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = _instance.Name;

        string packageVersion = this.FindControl<MyTextBox>("TextExportVersion")?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(packageVersion))
            packageVersion = "1.0.0";

        ExportRequested?.Invoke(
            this,
            new InstanceExportPageRequest(
                _instance,
                packageName,
                packageVersion,
                CollectRules(includeHidden: false),
                this.FindControl<MyCheckBox>("CheckOptionsPcl")?.Checked == true,
                this.FindControl<MyCheckBox>("CheckOptionsPclCustom")?.Checked == true,
                this.FindControl<MyCheckBox>("CheckAdvancedInclude")?.Checked == true,
                this.FindControl<MyCheckBox>("CheckAdvancedModrinth")?.Checked == true));
    }

    private void CheckAdvancedModrinth_Change(object sender, bool user)
    {
        if (this.FindControl<MyCheckBox>("CheckAdvancedModrinth") is not { } modrinth ||
            this.FindControl<MyCheckBox>("CheckOptionsPcl") is not { } launcher)
        {
            return;
        }

        if (modrinth.Checked == true)
            launcher.Checked = false;
        launcher.IsEnabled = modrinth.Checked != true;
    }

    private void CheckAdvancedInclude_Change(object sender, bool user)
    {
        if (this.FindControl<MyCheckBox>("CheckAdvancedInclude") is not { } include ||
            this.FindControl<MyCheckBox>("CheckAdvancedModrinth") is not { } modrinth)
        {
            return;
        }

        if (include.Checked == true)
            modrinth.Checked = false;
        modrinth.IsEnabled = include.Checked != true;
    }

    private void RefreshAllOptionsUI()
    {
        foreach (MyCheckBox checkBox in GetAllOptions(includeHidden: true))
        {
            if (checkBox.Tag is not ExportOption option)
                continue;

            checkBox.Height = 26d;
            checkBox.Inlines.Clear();
            checkBox.Inlines.Add(new Run(option.Title));
            if (!string.IsNullOrWhiteSpace(option.Description))
            {
                checkBox.Inlines.Add(new Run("   " + option.Description)
                {
                    Foreground = LegacyResourceResolver.Brush(checkBox, "ColorBrushGray5", "#9aa0a6")
                });
            }

            bool visible = ShouldShowOption(option);
            checkBox.IsVisible = visible;
            checkBox.Checked = option.DefaultChecked && visible;
        }
        SyncDependentVisibility();
    }

    private void WireVisibilityToggle(string checkBoxName, string targetName)
    {
        if (this.FindControl<MyCheckBox>(checkBoxName) is not { } checkBox)
            return;

        checkBox.Change += (_, _) => SyncVisibility(checkBoxName, targetName);
        SyncVisibility(checkBoxName, targetName);
    }

    private void SyncDependentVisibility()
    {
        SyncVisibility("CheckOptionsMod", "PanOptionsMod");
        SyncVisibility("CheckOptionsResourcePacks", "PanOptionsResourcePacks");
        SyncVisibility("CheckOptionsShaderPacks", "PanOptionsShaderPacks");
        SyncVisibility("CheckOptionsSaves", "PanOptionsSaves");
        SyncVisibility("CheckOptionsPcl", "PanOptionsPcl");
        SyncVisibility("CheckAdvancedInclude", "HintAdvancedInclude");
    }

    private void SyncVisibility(string checkBoxName, string targetName)
    {
        if (this.FindControl<MyCheckBox>(checkBoxName) is not { } checkBox ||
            this.FindControl<Control>(targetName) is not { } target)
        {
            return;
        }

        target.IsVisible = checkBox.Checked == true;
    }

    private void ResetConfigOverrides()
    {
        _rulesOverrides = null;
        SyncRulesOverrideUi();
        RefreshAllOptionsUI();
    }

    private void SyncRulesOverrideUi()
    {
        bool hasOverride = _rulesOverrides is { Count: > 0 };
        if (this.FindControl<MyIconTextButton>("BtnOverrideCancel") is { } overrideCancel)
        {
            overrideCancel.IsVisible = hasOverride;
            overrideCancel.IsHitTestVisible = hasOverride;
            overrideCancel.Opacity = hasOverride ? 1d : 0d;
        }

        if (this.FindControl<Panel>("PanOptions") is { } options)
            options.IsVisible = !hasOverride;

        if (this.FindControl<MyCard>("CardOptions") is { } card)
        {
            card.Inlines.Clear();
            card.Inlines.Add(new Run(hasOverride ? "导出内容：来自配置文件" : "导出内容"));
        }
    }

    private bool ShouldShowOption(ExportOption option)
    {
        if (_instance is null)
            return false;

        if (option.RequireOptiFine && !HasFileNamePart(_instance.InstanceDirectory, "optifine"))
            return false;
        if (option.RequireModLoader && !HasModLoader(_instance))
            return false;
        if (option.RequireModLoaderOrOptiFine && !HasFileNamePart(_instance.InstanceDirectory, "optifine") && !HasModLoader(_instance))
            return false;

        string? showRules = option.Rules ?? option.ShowRules;
        if (string.IsNullOrWhiteSpace(showRules))
            return true;

        string gameDirectory = GetMinecraftRootFromInstance(_instance);
        foreach (string rule in SplitRules(showRules))
        {
            if (rule.StartsWith('!'))
                continue;

            string normalized = rule.TrimEnd('*').TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
            string target = Path.Combine(gameDirectory, normalized);
            if (Directory.Exists(target) || File.Exists(target))
                return true;
        }

        return false;
    }

    private List<string> CollectRules(bool includeHidden)
    {
        if (_rulesOverrides is { Count: > 0 })
            return [.. _rulesOverrides];

        List<string> rules = [];
        foreach (MyCheckBox checkBox in GetAllOptions(includeHidden))
        {
            if (checkBox.Checked != true || checkBox.Tag is not ExportOption option || string.IsNullOrWhiteSpace(option.Rules))
                continue;

            rules.AddRange(SplitRules(option.Rules));
        }

        return rules;
    }

    private IEnumerable<MyCheckBox> GetAllOptions(bool includeHidden)
    {
        if (this.FindControl<Panel>("PanOptions") is not { } panel)
            yield break;

        foreach (Control child in EnumerateOptionControls(panel))
        {
            if (child is MyCheckBox checkBox && (includeHidden || checkBox.IsVisible))
                yield return checkBox;
        }
    }

    private static IEnumerable<Control> EnumerateOptionControls(Panel panel)
    {
        foreach (Control child in panel.Children)
        {
            yield return child;
            if (child is Panel childPanel)
            {
                foreach (Control nested in EnumerateOptionControls(childPanel))
                    yield return nested;
            }
        }
    }

    private static IEnumerable<string> SplitRules(string rules)
    {
        foreach (string raw in rules.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(raw))
                yield return raw;
        }
    }

    private static bool HasModLoader(LaunchInstanceInfo instance) =>
        HasFileNamePart(instance.InstanceDirectory, "forge") ||
        HasFileNamePart(instance.InstanceDirectory, "fabric") ||
        HasFileNamePart(instance.InstanceDirectory, "quilt") ||
        HasFileNamePart(instance.InstanceDirectory, "neoforge");

    private static bool HasFileNamePart(string folder, string part)
    {
        if (!Directory.Exists(folder))
            return false;

        return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Any(file => Path.GetFileName(file).Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo? versionsDirectory = versionDirectory.Parent;
        if (versionsDirectory?.Parent is not null &&
            string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
        {
            return versionsDirectory.Parent.FullName;
        }

        return instance.InstanceDirectory;
    }
}
