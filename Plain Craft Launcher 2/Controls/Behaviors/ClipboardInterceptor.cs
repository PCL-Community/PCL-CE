using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Clipboard = System.Windows.Forms.Clipboard;

// Author: uye (owner of the MaaAssistantArknights team)
// Original Source: MaaAssistantArknights project - https://github.com/MaaAssistantArknights/MaaAssistantArknights
// License: Apache License 2.0 (this file only)
// 
// This file is based on work originally developed in the MaaAssistantArknights project,
// which is licensed under the GNU AGPL v3.0 only.
// 
// As the original author of this code, I am re-licensing this specific file under
// the Apache License 2.0 for use in PCL2-CE.
// 
// Description:
// Implements a WPF clipboard fix to handle OpenClipboard failures in TextBox,
// RichTextBox, and DataGrid, typically caused by focus issues or external hooks.
// 
// Date: 2025-07-03

namespace PCL.Controls.Behaviors;

public sealed class ClipboardInterceptor
{
    public static readonly DependencyProperty EnableSafeClipboardProperty =
        DependencyProperty.RegisterAttached("EnableSafeClipboard", typeof(bool), typeof(ClipboardInterceptor),
            new PropertyMetadata(false, OnEnableSafeClipboardChanged));

    private ClipboardInterceptor()
    {
    }

    public static void SetEnableSafeClipboard(DependencyObject element, bool value)
    {
        element.SetValue(EnableSafeClipboardProperty, value);
    }

    public static bool GetEnableSafeClipboard(DependencyObject element)
    {
        return (bool)element.GetValue(EnableSafeClipboardProperty);
    }

    private static void OnEnableSafeClipboardChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        switch (d)
        {
            case TextBox box when (bool)e.NewValue:
                AddCommandBindingsToTextBox(box);
                break;
            case RichTextBox box when (bool)e.NewValue:
                AddCommandBindingsToRichTextBox(box);
                break;
            case DataGrid grid when (bool)e.NewValue:
                AddCommandBindingsToDataGrid(grid);
                break;
        }
    }

    private static void AddCommandBindingsToTextBox(TextBox tb)
    {
        tb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyTextBox));
        tb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnCutTextBox));
        tb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteTextBox));
    }

    private static void AddCommandBindingsToRichTextBox(RichTextBox rtb)
    {
        rtb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyRichTextBox));
        rtb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnCutRichTextBox));
        rtb.CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteRichTextBox));
    }

    private static void AddCommandBindingsToDataGrid(DataGrid dg)
    {
        dg.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyDataGrid));
    }

    private static void OnCopyTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var tb = sender as TextBox;
        if (tb is null || tb.SelectionLength <= 0)
            return;

        try
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(tb.SelectedText, true);
        }
        catch
        {
        }

        e.Handled = true;
    }

    private static void OnCutTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var tb = sender as TextBox;
        if (tb is null || tb.SelectionLength <= 0)
            return;

        try
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(tb.SelectedText, true);
        }
        catch
        {
        }

        tb.SelectedText = string.Empty;
        e.Handled = true;
    }

    private static void OnPasteTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var tb = sender as TextBox;
        if (tb is null)
            return;

        if (Clipboard.ContainsText())
        {
            var pasteText = Clipboard.GetText();

            if (!tb.AcceptsReturn)
                pasteText = pasteText.Replace("\r\n", " ").Replace("\r", " ")
                    .Replace("\n", " ");

            var start = tb.SelectionStart;

            tb.SelectedText = pasteText;
            tb.CaretIndex = start + pasteText.Length;
            tb.SelectionLength = 0;
        }

        e.Handled = true;
    }

    private static void OnCopyRichTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var rtb = sender as RichTextBox;
        if (rtb is null)
            return;

        var textRange = new TextRange(rtb.Selection.Start, rtb.Selection.End);
        if (string.IsNullOrEmpty(textRange.Text))
            return;

        try
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(textRange.Text, true);
        }
        catch
        {
        }

        e.Handled = true;
    }

    private static void OnCutRichTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var rtb = sender as RichTextBox;
        if (rtb is null)
            return;

        var selection = new TextRange(rtb.Selection.Start, rtb.Selection.End);
        if (string.IsNullOrEmpty(selection.Text))
            return;

        try
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(selection.Text, true);
        }
        catch
        {
        }

        selection.Text = string.Empty;
        e.Handled = true;
    }

    private static void OnPasteRichTextBox(object sender, ExecutedRoutedEventArgs e)
    {
        var rtb = sender as RichTextBox;
        if (rtb is null)
            return;

        if (!Clipboard.ContainsText())
            return;

        var pasteText = Clipboard.GetText();
        var selection = rtb.Selection;

        selection.Text = pasteText;

        var caretPos = selection.End;
        rtb.CaretPosition = caretPos;
        rtb.Selection.Select(caretPos, caretPos);

        e.Handled = true;
    }

    private static void OnCopyDataGrid(object sender, ExecutedRoutedEventArgs e)
    {
        var dg = sender as DataGrid;
        if (dg is null || dg.SelectedCells is null || dg.SelectedCells.Count == 0)
            return;

        var sb = new StringBuilder();
        var rowGroups = dg.SelectedCells.GroupBy(c => c.Item);

        foreach (var row in rowGroups)
        {
            var rowText = string.Join("\t", row.Select(cell =>
            {
                var tb = cell.Column.GetCellContent(cell.Item) as TextBlock;
                return tb is not null ? tb.Text : "";
            }));
            sb.AppendLine(rowText);
        }

        var sbStr = sb.ToString().TrimEnd('\r', '\n');

        try
        {
            Clipboard.Clear();
            Clipboard.SetDataObject(sbStr, true);
        }
        catch
        {
        }

        e.Handled = true;
    }
}