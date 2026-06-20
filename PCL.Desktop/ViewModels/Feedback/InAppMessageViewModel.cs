// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using PCL.UI.Abstractions;

namespace PCL.Desktop.ViewModels.Feedback;

public sealed class InAppMessageViewModel
{
    public InAppMessageViewModel(
        string title,
        string message,
        HintSeverity severity,
        Action dismiss)
    {
        Title = title;
        Message = message;
        Severity = severity;
        IconKey = severity switch
        {
            HintSeverity.Success => "lucide/circle-check",
            HintSeverity.Warning => "lucide/triangle-alert",
            HintSeverity.Error => "lucide/circle-x",
            _ => "lucide/info"
        };
        DismissCommand = new DelegateCommand(dismiss);
    }

    public string Title { get; }

    public string Message { get; }

    public HintSeverity Severity { get; }

    public string IconKey { get; }

    public bool IsSuccess => Severity == HintSeverity.Success;

    public bool IsWarning => Severity == HintSeverity.Warning;

    public bool IsError => Severity == HintSeverity.Error;

    public ICommand DismissCommand { get; }
}
