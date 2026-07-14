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
/// Binds host surfaces/slots to live Avalonia controls and applies inject/modify/wrap/replace patches.
/// </summary>
internal sealed class DesktopPluginHostUiComposition : IPluginHostUiComposition
{
    public static DesktopPluginHostUiComposition Instance { get; } = new();

    private readonly ConcurrentDictionary<string, WeakReference> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, WeakReference> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<WrapRecord>> _wraps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ReplaceRecord> _replaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _generations = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterTarget(string surfaceId, Control control)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentNullException.ThrowIfNull(control);
        _targets[surfaceId] = new WeakReference(control);
        _generations.AddOrUpdate(surfaceId, 1, static (_, generation) => generation + 1);
    }

    public void RegisterSlot(string surfaceId, string slotId, Panel panel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(panel);
        _slots[SlotKey(surfaceId, slotId)] = new WeakReference(panel);
    }

    public void UnregisterTarget(string surfaceId)
    {
        ResetWrapAndReplace(surfaceId);
        _targets.TryRemove(surfaceId, out _);
        _generations.AddOrUpdate(surfaceId, 1, static (_, generation) => generation + 1);
    }

    public void UnregisterSlot(string surfaceId, string slotId) =>
        _slots.TryRemove(SlotKey(surfaceId, slotId), out _);

    public bool IsTargetRegistered(string surfaceId) =>
        _targets.TryGetValue(surfaceId, out WeakReference? wr) && wr.IsAlive;

    public object? ResolveTarget(string surfaceId) =>
        TryGetTarget(surfaceId, out Control? control) ? control : null;

    public long GetTargetGeneration(string surfaceId) =>
        _generations.TryGetValue(surfaceId, out long generation) ? generation : 0;

    public void ClearSlot(string surfaceId, string slotId)
    {
        if (!TryGetSlot(surfaceId, slotId, out Panel? panel) || panel is null)
            return;

        void Clear()
        {
            List<Control> remove = panel.Children
                .OfType<Control>()
                .Where(static c => c.Tag is string tag && tag.StartsWith("pcl.plugin.inject:", StringComparison.Ordinal))
                .ToList();
            foreach (Control child in remove)
                panel.Children.Remove(child);
        }

        RunOnUi(Clear);
    }

    public bool Inject(string surfaceId, string slotId, HostUiInjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetSlot(surfaceId, slotId, out Panel? panel) || panel is null)
            return false;

        void Add()
        {
            string tag = "pcl.plugin.inject:" + request.PluginId + ":" + request.ContributionId;
            Control? existing = panel.Children.OfType<Control>()
                .FirstOrDefault(c => string.Equals(c.Tag as string, tag, StringComparison.Ordinal));
            if (existing is not null)
                panel.Children.Remove(existing);

            Control content;
            if (request.CreateContent?.Invoke() is Control pluginContent)
            {
                content = new Border
                {
                    Child = pluginContent,
                    Margin = new Thickness(0, 2, 0, 2),
                    Tag = tag
                };
            }
            else
            {
                content = new MyButton
                {
                    Text = string.IsNullOrWhiteSpace(request.Title) ? request.ContributionId : request.Title,
                    Height = 32,
                    Margin = new Thickness(0, 2, 0, 2),
                    Tag = tag
                };
            }
            ToolTip.SetTip(content, $"{request.PluginId} · {request.ContributionId}");
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

            content.SetValue(InjectOrderProperty, request.Order);
            panel.Children.Insert(insertAt, content);
        }

        RunOnUi(Add);
        return true;
    }

    public bool TrySetProperty(string surfaceId, string? slotId, string propertyPath, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyPath);
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;

        return RunOnUi(() =>
        {
            string path = propertyPath.Trim();
            if (path.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("Content", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("Title", StringComparison.OrdinalIgnoreCase))
            {
                if (control is MyButton myButton)
                {
                    myButton.Text = value ?? string.Empty;
                    return true;
                }

                if (control is TextBlock textBlock)
                {
                    textBlock.Text = value ?? string.Empty;
                    return true;
                }

                if (control is ContentControl contentControl)
                {
                    contentControl.Content = value;
                    return true;
                }
            }
            else if (path.Equals("IsEnabled", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool enabled))
            {
                control.IsEnabled = enabled;
                return true;
            }
            else if (path.Equals("IsVisible", StringComparison.OrdinalIgnoreCase) &&
                     bool.TryParse(value, out bool visible))
            {
                control.IsVisible = visible;
                return true;
            }
            else if (path.Equals("Opacity", StringComparison.OrdinalIgnoreCase) &&
                     double.TryParse(value, System.Globalization.NumberStyles.Float,
                         System.Globalization.CultureInfo.InvariantCulture, out double opacity))
            {
                control.Opacity = opacity;
                return true;
            }
            return false;
        });
    }

    public bool TrySetVisible(string surfaceId, bool isVisible)
    {
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;

        RunOnUi(() => control.IsVisible = isVisible);
        return true;
    }

    public bool TryWrap(string surfaceId, HostUiWrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;
        return RunOnUi(() =>
        {
            if (control.Parent is not Panel parent)
                return false;
            int index = parent.Children.IndexOf(control);
            if (index < 0)
                return false;

            parent.Children.RemoveAt(index);
            Border wrapper = new()
            {
                Tag = "pcl.plugin.wrap:" + request.PluginId + ":" + request.OperationId,
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 80, 140, 220)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2),
                Margin = control.Margin
            };
            StackPanel stack = new() { Spacing = 2 };
            if (!string.IsNullOrWhiteSpace(request.Label))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = request.Label,
                    FontSize = 10,
                    Opacity = 0.75,
                    Margin = new Thickness(2, 0, 2, 0)
                });
            }

            control.Margin = new Thickness(0);
            stack.Children.Add(control);
            wrapper.Child = stack;
            parent.Children.Insert(index, wrapper);

            List<WrapRecord> list = _wraps.GetOrAdd(surfaceId, static _ => []);
            list.Add(new WrapRecord(control, wrapper, parent, index, request.PluginId, request.OperationId));
            // Target still points at original control (now nested).
            return true;
        });
    }

    public bool TryReplace(string surfaceId, HostUiReplaceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!TryGetTarget(surfaceId, out Control? control) || control is null)
            return false;
        return RunOnUi(() =>
        {
            if (control.Parent is not Panel parent)
                return false;
            // Only one exclusive replace per surface.
            if (_replaces.ContainsKey(surfaceId))
                return false;
            int index = parent.Children.IndexOf(control);
            if (index < 0)
                return false;

            bool wasVisible = control.IsVisible;
            control.IsVisible = false;

            Border replacement = new()
            {
                Tag = "pcl.plugin.replace:" + request.PluginId + ":" + request.OperationId,
                Background = new SolidColorBrush(Color.FromArgb(40, 120, 120, 140)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 160, 100, 40)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                MinHeight = Math.Max(32, control.Bounds.Height > 0 ? control.Bounds.Height : 40),
                Margin = control.Margin
            };
            replacement.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(request.Title)
                    ? $"[Replaced by {request.PluginId}]"
                    : request.Title,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            parent.Children.Insert(index + 1, replacement);
            _replaces[surfaceId] = new ReplaceRecord(control, replacement, parent, wasVisible, request.PluginId, request.OperationId);
            return true;
        });
    }

    public void ResetWrapAndReplace(string surfaceId)
    {
        void Reset()
        {
            if (_replaces.TryRemove(surfaceId, out ReplaceRecord? replace))
            {
                if (replace.Replacement.Parent is Panel rp)
                    rp.Children.Remove(replace.Replacement);
                replace.Original.IsVisible = replace.WasVisible;
            }

            if (_wraps.TryRemove(surfaceId, out List<WrapRecord>? wraps))
            {
                // Unwrap in reverse order (outermost first).
                for (int i = wraps.Count - 1; i >= 0; i--)
                {
                    WrapRecord wrap = wraps[i];
                    if (wrap.Wrapper.Parent is not Panel parent)
                        continue;
                    int index = parent.Children.IndexOf(wrap.Wrapper);
                    if (index < 0)
                        continue;
                    // Detach original from wrapper stack.
                    if (wrap.Wrapper.Child is Panel stack)
                        stack.Children.Remove(wrap.Original);
                    else if (wrap.Wrapper.Child == wrap.Original)
                        wrap.Wrapper.Child = null;
                    parent.Children.RemoveAt(index);
                    parent.Children.Insert(index, wrap.Original);
                }
            }
        }

        RunOnUi(Reset);
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

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    private static T RunOnUi<T>(Func<T> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return action();
        return Dispatcher.UIThread.Invoke(action);
    }

    private sealed record WrapRecord(
        Control Original,
        Border Wrapper,
        Panel Parent,
        int Index,
        string PluginId,
        string OperationId);

    private sealed record ReplaceRecord(
        Control Original,
        Control Replacement,
        Panel Parent,
        bool WasVisible,
        string PluginId,
        string OperationId);
}
