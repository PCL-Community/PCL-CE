// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using PCL.Application.Instances;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceSetupRight : MyPageRight
{
    private LaunchInstanceInfo? _instance;
    private InstanceMetadata _metadata = new();
    private bool _isLoading;

    public PageInstanceSetupRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        WireControls();
    }

    public event EventHandler? OpenGlobalSettingsRequested;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        _metadata = InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult();
        ApplyMetadata();
    }

    private void WireControls()
    {
        foreach (MyTextBox textBox in this.GetVisualDescendants().OfType<MyTextBox>())
            textBox.TextChanged += TextBox_TextChanged;

        foreach (MyComboBox comboBox in this.GetVisualDescendants().OfType<MyComboBox>())
            comboBox.SelectionChanged += ComboBox_SelectionChanged;

        foreach (MyCheckBox checkBox in this.GetVisualDescendants().OfType<MyCheckBox>())
            checkBox.Change += CheckBox_Change;

        foreach (MyRadioBox radioBox in this.GetVisualDescendants().OfType<MyRadioBox>())
            radioBox.Check += RadioBox_Check;

        if (this.FindControl<MySlider>("SliderRamCustom") is { } slider)
            slider.Change += Slider_Change;

        if (this.FindControl<MyExtraTextButton>("BtnSwitch") is { } switchButton)
            switchButton.Click += (_, _) => OpenGlobalSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyMetadata()
    {
        _isLoading = true;
        try
        {
            SetComboIndex("ComboArgumentIndieV2", _metadata.InstanceIsolation ? 0 : 1);
            SetEditableComboText("TextArgumentTitle", _metadata.WindowTitle);
            SetChecked("CheckArgumentTitleEmpty", _metadata.UseGlobalWindowTitle);
            SetText("TextArgumentInfo", _metadata.CustomInfo);
            SetRadio("RadioRamType" + _metadata.MemorySolution);
            SetSliderValue("SliderRamCustom", _metadata.CustomMemorySize);
            SetComboIndex("ComboServerLoginRequire", _metadata.ServerLoginRequirement);
            SetText("TextServerAuthServer", _metadata.AuthServerAddress);
            SetText("TextServerAuthRegister", _metadata.AuthRegisterAddress);
            SetText("TextServerAuthName", _metadata.AuthServerDisplayName);
            SetText("TextServerEnter", _metadata.ServerToEnter);
            SetComboIndex("ComboAdvanceRenderer", _metadata.Renderer);
            SetText("TextAdvanceJvm", _metadata.JvmArguments);
            SetText("TextAdvanceGame", _metadata.GameArguments);
            SetText("TextAdvanceClasspathHead", _metadata.ClasspathHead);
            SetText("TextAdvanceRun", _metadata.PreLaunchCommand);
            SetChecked("CheckAdvanceRunWait", _metadata.WaitForPreLaunchCommand);
            SetChecked("CheckAdvanceJava", _metadata.IgnoreJavaCompatibility);
            SetChecked("CheckAdvanceAssetsV2", _metadata.DisableAssetVerification);
            SetChecked("CheckAdvanceUseProxyV2", _metadata.UseProxy);
            SetChecked("CheckAdvanceDisableJLW", _metadata.DisableJlw);
            SetChecked("CheckAdvanceDisableRW", _metadata.DisableRw);
            SetChecked("CheckUseDebugLog4j2Config", _metadata.UseDebugLog4j2Config);
            SetChecked("CheckAdvanceDisableLwjglUnsafeAgent", _metadata.DisableLwjglUnsafeAgent);
            ApplyRamMode();
            ApplyServerLoginMode();
            ApplyPreLaunchCommandVisibility();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || sender is not MyTextBox textBox)
            return;

        UpdateMetadata(metadata => (textBox.Tag?.ToString()) switch
        {
            "VersionArgumentInfo" => metadata with { CustomInfo = textBox.Text ?? string.Empty },
            "VersionServerAuthServer" => metadata with { AuthServerAddress = textBox.Text ?? string.Empty },
            "VersionServerAuthRegister" => metadata with { AuthRegisterAddress = textBox.Text ?? string.Empty },
            "VersionServerAuthName" => metadata with { AuthServerDisplayName = textBox.Text ?? string.Empty },
            "VersionServerEnter" => metadata with { ServerToEnter = textBox.Text ?? string.Empty },
            "VersionAdvanceJvm" => metadata with { JvmArguments = textBox.Text ?? string.Empty },
            "VersionAdvanceGame" => metadata with { GameArguments = textBox.Text ?? string.Empty },
            "VersionAdvanceClasspathHead" => metadata with { ClasspathHead = textBox.Text ?? string.Empty },
            "VersionAdvanceRun" => metadata with { PreLaunchCommand = textBox.Text ?? string.Empty },
            _ => metadata
        });
        ApplyPreLaunchCommandVisibility();
    }

    private void ComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || sender is not MyComboBox comboBox)
            return;

        string? tag = comboBox.Tag?.ToString();
        if (ReferenceEquals(comboBox, this.FindControl<MyComboBox>("TextArgumentTitle")))
        {
            UpdateMetadata(metadata => metadata with { WindowTitle = comboBox.Text ?? string.Empty });
            return;
        }

        UpdateMetadata(metadata => tag switch
        {
            "VersionArgumentIndieV2" => metadata with { InstanceIsolation = comboBox.SelectedIndex == 0 },
            "VersionServerLoginRequire" => metadata with { ServerLoginRequirement = Math.Max(0, comboBox.SelectedIndex) },
            "VersionAdvanceRenderer" => metadata with { Renderer = Math.Max(0, comboBox.SelectedIndex) },
            _ => metadata
        });
        ApplyServerLoginMode();
    }

    private void CheckBox_Change(object sender, bool user)
    {
        if (_isLoading || sender is not MyCheckBox checkBox)
            return;

        bool value = checkBox.Checked == true;
        UpdateMetadata(metadata => (checkBox.Tag?.ToString()) switch
        {
            "VersionArgumentTitleEmpty" => metadata with { UseGlobalWindowTitle = value },
            "VersionAdvanceRunWait" => metadata with { WaitForPreLaunchCommand = value },
            "VersionAdvanceJava" => metadata with { IgnoreJavaCompatibility = value },
            "VersionAdvanceAssetsV2" => metadata with { DisableAssetVerification = value },
            "VersionAdvanceUseProxyV2" => metadata with { UseProxy = value },
            "VersionAdvanceDisableJLW" => metadata with { DisableJlw = value },
            "VersionAdvanceDisableRW" => metadata with { DisableRw = value },
            "VersionUseDebugLog4j2Config" => metadata with { UseDebugLog4j2Config = value },
            "VersionAdvanceDisableLwjglUnsafeAgent" => metadata with { DisableLwjglUnsafeAgent = value },
            _ => metadata
        });
    }

    private void RadioBox_Check(object sender, RouteEventArgs e)
    {
        if (_isLoading || sender is not MyRadioBox radioBox)
            return;

        if (radioBox.Name is not { } name || !name.StartsWith("RadioRamType", StringComparison.Ordinal))
            return;

        if (int.TryParse(name["RadioRamType".Length..], out int value))
            UpdateMetadata(metadata => metadata with { MemorySolution = value });
        ApplyRamMode();
    }

    private void Slider_Change(object sender, bool user)
    {
        if (_isLoading || sender is not MySlider slider)
            return;

        UpdateMetadata(metadata => metadata with { CustomMemorySize = slider.Value });
    }

    private void UpdateMetadata(Func<InstanceMetadata, InstanceMetadata> update)
    {
        if (_instance is null)
            return;

        _metadata = update(_metadata);
        _ = InstanceMetadataStore.SaveAsync(_instance.InstanceDirectory, _metadata);
    }

    private void ApplyRamMode()
    {
        if (this.FindControl<MySlider>("SliderRamCustom") is { } slider)
            slider.IsEnabled = _metadata.MemorySolution == 1;
    }

    private void ApplyServerLoginMode()
    {
        bool showAuth = _metadata.ServerLoginRequirement is 2 or 3;
        SetVisible("LabServerAuthServer", showAuth);
        SetVisible("TextServerAuthServer", showAuth);
        SetVisible("LabServerAuthRegister", showAuth);
        SetVisible("TextServerAuthRegister", showAuth);
        SetVisible("LabServerAuthName", showAuth);
        SetVisible("TextServerAuthName", showAuth);
    }

    private void ApplyPreLaunchCommandVisibility()
    {
        SetVisible(
            "CheckAdvanceRunWait",
            !string.IsNullOrWhiteSpace(this.FindControl<MyTextBox>("TextAdvanceRun")?.Text));
    }

    private void SetText(string name, string value)
    {
        if (this.FindControl<MyTextBox>(name) is { } textBox)
            textBox.Text = value;
    }

    private void SetEditableComboText(string name, string value)
    {
        if (this.FindControl<MyComboBox>(name) is { } comboBox)
            comboBox.Text = value;
    }

    private void SetChecked(string name, bool value)
    {
        if (this.FindControl<MyCheckBox>(name) is { } checkBox)
            checkBox.Checked = value;
    }

    private void SetRadio(string name)
    {
        if (this.FindControl<MyRadioBox>(name) is { } radioBox)
            radioBox.Checked = true;
    }

    private void SetSliderValue(string name, int value)
    {
        if (this.FindControl<MySlider>(name) is { } slider)
            slider.Value = value;
    }

    private void SetComboIndex(string name, int index)
    {
        if (this.FindControl<MyComboBox>(name) is { } comboBox && comboBox.ItemCount > 0)
            comboBox.SelectedIndex = Math.Clamp(index, 0, comboBox.ItemCount - 1);
    }

    private void SetVisible(string name, bool visible)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.IsVisible = visible;
    }
}
