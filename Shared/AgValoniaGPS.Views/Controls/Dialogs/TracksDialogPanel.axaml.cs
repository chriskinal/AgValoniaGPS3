using System;
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

        Console.WriteLine($"[Track] PointerReleased on: {clickedTrack.Name}, IsActive={clickedTrack.IsActive}, SelectedTrack={vm.SelectedTrack?.Name ?? "null"}");

        // Check if this track is the currently active one (compare by reference or name)
        bool isCurrentlyActive = vm.SelectedTrack == clickedTrack;

        if (isCurrentlyActive)
        {
            // Deactivate
            Console.WriteLine($"[Track] Deactivating: {clickedTrack.Name}");
            vm.SelectedTrack = null;
        }
        else
        {
            // Activate
            Console.WriteLine($"[Track] Activating: {clickedTrack.Name}");
            vm.SelectedTrack = clickedTrack;
        }
    }
}
