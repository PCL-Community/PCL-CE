// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Hosting;

/// <summary>
/// Binds host surfaces/slots to live Avalonia controls and applies inject/modify patches.
/// </summary>
internal sealed class DesktopPluginHostUiComposition : IPluginHostUiComposition
{
    public static DesktopPluginHostUiComposition Instance { get; } = new();

    private readonly ConcurrentDictionary<string, WeakReference> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WeakReference> _slots = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterTarget(string surfaceId, Control control)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentNullException.ThrowIfNull(control);
        _targets[surfaceId] = new WeakReference(control);
    }

    public void RegisterSlot(string surfaceId, string slotId, Panel panel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(panel);
        _slots[SlotKey(surfaceId, slotId)] = new WeakReference(panel);
    }

    public void UnregisterTarget(string surfaceId) => _targets.TryRemove(surfaceId, out _);

    public void UnregisterSlot(string surfaceId, string slotId) =>
        _slots.TryRemove(SlotKey(surfaceId, slotId), out _);

    public bool IsTargetRegistered(string surfaceId) =>
        _targets.TryGetValue(surfaceId, out WeakReference? wr) && wr.IsAlive;

    public void ClearSlot(string surfaceId, string slotId)
    {
        if (!TryGetSlot(surfaceId, slotId, out Panel? panel) || panel is null)
            return;

        void Clear()
        {
            // Only remove plugin-injected children tagged by us.
            List<Control> remove = panel.Children
                .OfType<Control>()
                .Where(static c => c.Tag is string tag && tag.StartsWith("pcl.plugin.inject:", StringComparison.Ordinal))
                .ToList();
            foreach (Control child in remove)
                panel.Children.Remove(child);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Clear();
        else
            Dispatcher.UIThread.Post(Clear);
    }

    public void Inject(string surfaceId, string slotId, HostUiInjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetSlot(surfaceId, slotId, out Panel? panel) || panel is null)
            return;

        void Add()
        {
            string tag = "pcl.plugin.inject:" + request.PluginId + ":" + request.ContributionId;
            // Replace existing contribution with same id.
            Control? existing = panel.Children.OfType<Control>()
                .FirstOrDefault(c => string.Equals(c.Tag as string, tag, StringComparison.Ordinal));
            if (existing is not null)
                panel.Children.Remove(existing);

            MyButton button = new()
            {
                Text = string.IsNullOrWhiteSpace(request.Title) ? request.ContributionId : request.Title,
                Height = 32,
                Margin = new Thickness(0, 2, 0, 2),
                Tag = tag
            };
            ToolTip.SetTip(button, $"{request.PluginId} · {request.ContributionId}");
            // Order: higher order later (append). Simple stable insert by order tag.
            int insertAt = panel.Children.Count;
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is Control c &&
                    c.Tag is string existingTag &&
                    existingTag.StartsWith("pcl.plugin.inject:", StringComparison.Ordinal) &&
                    TryReadOrder(c, out int existingOrder) &&
                    request.Order < existingOrder)
                {
                    insertAt = i;
                    break;
                }
            }

            button.SetValue(InjectOrderProperty, request.Order);
            panel.Children.Insert(insertAt, button);
        }

        if (Dispatcher.UIThread.CheckAccess())
            Add();
        else
            Dispatcher.UIThread.Post(Add);
    }

    public bool TrySetProperty(string surfaceId, string? slotId, string propertyPath, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;

        bool result = false;
        void Set()
        {
            string path = propertyPath.Trim();
            if (path.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("Title", StringComparison.OrdinalIgnoreCase))
            {
                if (control is MyButton myButton)
                {
                    myButton.Text = value ?? string.Empty;
                    result = true;
                    return;
                }

                if (control is TextBlock textBlock)
                {
                    textBlock.Text = value ?? string.Empty;
                    result = true;
                    return;
                }

                if (control is ContentControl contentControl)
                {
                    contentControl.Content = value;
                    result = true;
                }
            }
            else if (path.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool enabled))
            {
                control.IsEnabled = enabled;
                result = true;
            }
            else if (path.Equals("IsVisible", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool visible))
            {
                control.IsVisible = visible;
                result = true;
            }
            else if (path.Equals("Opacity", StringComparison.OrdinalIgnoreCase) &&
                     double.TryParse(value, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out double opacity))
            {
                control.Opacity = opacity;
                result = true;
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
            Set();
        else
            Dispatcher.UIThread.Invoke(Set);
        return result;
    }

    public bool TrySetVisible(string surfaceId, bool isVisible)
    {
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;

        void Set() => control.IsVisible = isVisible;
        if (Dispatcher.UIThread.CheckAccess())
            Set();
        else
            Dispatcher.UIThread.Post(Set);
        return true;
    }

    private bool TryGetTarget(string surfaceId, out Control? control)
    {
        control = null;
        if (!_targets.TryGetValue(surfaceId, out WeakReference? wr) || wr.Target is not Control c)
            return false;
        control = c;
        return true;
    }

    private bool TryGetSlot(string surfaceId, string slotId, out Panel? panel)
    {
        panel = null;
        if (!_slots.TryGetValue(SlotKey(surfaceId, slotId), out WeakReference? wr) || wr.Target is not Panel p)
            return false;
        panel = p;
        return true;
    }

    private static string SlotKey(string surfaceId, string slotId) => surfaceId + "\0" + slotId;

    private static readonly AttachedProperty<int> InjectOrderProperty =
        AvaloniaProperty.RegisterAttached<Control, int>("PluginInjectOrder", typeof(DesktopPluginHostUiComposition));

    private static bool TryReadOrder(Control control, out int order)
    {
        order = control.GetValue(InjectOrderProperty);
        return true;
    }
}
