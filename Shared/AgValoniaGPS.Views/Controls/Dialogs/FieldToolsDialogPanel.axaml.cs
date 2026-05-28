// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldToolsDialogPanel : UserControl
{
    public FieldToolsDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        (DataContext as AgValoniaGPS.ViewModels.MainViewModel)?.CloseFieldToolsDialogCommand?.Execute(null);
    }

    /// <summary>
    /// Close the launcher after a direct-action tool (Create-from-boundary,
    /// Smooth, Recorded Path) that doesn't open its own dialog — so the result
    /// is visible on the map. Dialog-opening tools don't use this; their
    /// ShowDialog already replaces this dialog. The bound Command still runs.
    /// </summary>
    private void ToolThenClose_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as AgValoniaGPS.ViewModels.MainViewModel)?.CloseFieldToolsDialogCommand?.Execute(null);
    }
}
