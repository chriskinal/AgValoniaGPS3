// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class ScreenAlertsDialogPanel : UserControl
{
    public ScreenAlertsDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CloseScreenAlertsDialogCommand?.Execute(null);
        }
    }
}
