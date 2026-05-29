// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldToolsDialogPanel : UserControl
{
    public FieldToolsDialogPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Close the ring after a direct-action tool (Offset, Recorded Path, AgShare,
    /// etc.) that opens its own dialog — so the launched dialog isn't competing
    /// with the ring. Dialog-opening creation/boundary tools instead capture the
    /// ring as their return-to-parent and reopen it on Back/confirm; the bound
    /// Command still runs after this Click handler.
    /// </summary>
    private void ToolThenClose_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as AgValoniaGPS.ViewModels.MainViewModel)?.CloseFieldToolsDialogCommand?.Execute(null);
    }

    /// <summary>
    /// Launch a "field-work tool" (plan §3 archetype): a tool that takes over the
    /// live map — AB-line/curve creation or boundary recording. Arm the launcher
    /// return so the ring reopens when the tool finishes/cancels, then let the
    /// bound command run (it closes the ring itself once its guards pass). The
    /// Click event fires before the Command, so the arm captures Field Tools as
    /// the active dialog before it closes.
    /// </summary>
    private void LaunchFieldWorkTool_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as AgValoniaGPS.ViewModels.MainViewModel)?.ArmLauncherReturn();
    }
}
