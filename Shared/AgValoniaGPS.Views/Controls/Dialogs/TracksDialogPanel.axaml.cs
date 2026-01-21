// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class TracksDialogPanel : UserControl
{
    public TracksDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Close dialog when clicking the backdrop
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.State.UI.CloseDialog();
        }
    }

    private void TrackItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not AgValoniaGPS.ViewModels.MainViewModel vm)
            return;

        // Get the track from the clicked item's DataContext
        var grid = sender as Grid;
        var clickedTrack = grid?.DataContext as Track;

        if (clickedTrack == null)
            return;

        // Mark event as handled to prevent ListBox from overriding our changes
        e.Handled = true;

        Debug.WriteLine($"[Track] PointerReleased on: {clickedTrack.Name}, IsActive={clickedTrack.IsActive}, SelectedTrack={vm.SelectedTrack?.Name ?? "null"}");

        // Check if this track is the currently active one (compare by reference or name)
        bool isCurrentlyActive = vm.SelectedTrack == clickedTrack;

        if (isCurrentlyActive)
        {
            // Deactivate
            Debug.WriteLine($"[Track] Deactivating: {clickedTrack.Name}");
            vm.SelectedTrack = null;
        }
        else
        {
            // Activate
            Debug.WriteLine($"[Track] Activating: {clickedTrack.Name}");
            vm.SelectedTrack = clickedTrack;
        }
    }
}
