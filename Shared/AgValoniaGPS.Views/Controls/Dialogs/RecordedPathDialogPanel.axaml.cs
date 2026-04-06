// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3.

using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class RecordedPathDialogPanel : UserControl
{
    public RecordedPathDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Close dialog when clicking backdrop
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CloseRecordedPathDialogCommand?.Execute(null);
        }
    }
}
