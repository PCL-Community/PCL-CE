using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace PCL.UI.Controls;

/// <summary>自定义可点击控件的自动化 Peer，向 UIA/AT-SPI 暴露按钮语义。</summary>
public sealed class PclButtonAutomationPeer(
    Control owner,
    Action invoke,
    Func<string?>? fallbackName = null,
    AutomationControlType controlType = AutomationControlType.Button)
    : ControlAutomationPeer(owner), IInvokeProvider
{
    public void Invoke()
    {
        EnsureEnabled();
        invoke();
    }

    protected override string? GetNameCore()
    {
        var name = AutomationProperties.GetName(Owner);
        return !string.IsNullOrWhiteSpace(name) ? name : fallbackName?.Invoke();
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return controlType;
    }

    protected override bool IsControlElementCore()
    {
        return true;
    }

    protected override bool IsContentElementCore()
    {
        return true;
    }
}

/// <summary>复选框/单选框的 Toggle 语义。</summary>
public sealed class PclToggleAutomationPeer(
    Control owner,
    Func<bool?> state,
    Action toggle,
    Func<string?>? fallbackName = null,
    AutomationControlType controlType = AutomationControlType.CheckBox)
    : ControlAutomationPeer(owner), IToggleProvider
{
    public ToggleState ToggleState =>
        state() switch
        {
            true => ToggleState.On,
            false => ToggleState.Off,
            _ => ToggleState.Indeterminate
        };

    public void Toggle()
    {
        EnsureEnabled();
        toggle();
    }

    protected override string? GetNameCore()
    {
        var name = AutomationProperties.GetName(Owner);
        return !string.IsNullOrWhiteSpace(name) ? name : fallbackName?.Invoke();
    }

    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return controlType;
    }
}