// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class BoundaryManagementDialogPanel : UserControl
{
    public BoundaryManagementDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Backdrop = abandon to the map; the Back button returns to the launcher.
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
            vm.State.UI.CloseDialog();
        e.Handled = true;
    }
}
