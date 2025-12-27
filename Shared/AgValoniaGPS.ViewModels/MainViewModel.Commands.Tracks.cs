using System;
using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing track and AB line guidance command initialization.
/// Most track commands delegate to TrackManagementViewModel.
/// </summary>
public partial class MainViewModel
{
    private void InitializeTrackCommands()
    {
        // AB Line Guidance Commands - Bottom Bar (delegate to Tracks)
        SnapLeftCommand = Tracks.SnapLeftCommand;
        SnapRightCommand = Tracks.SnapRightCommand;

        StopGuidanceCommand = new RelayCommand(() =>
        {
            StatusMessage = "Guidance Stopped";
        });

        UTurnCommand = new RelayCommand(() =>
        {
            StatusMessage = "U-Turn - not yet implemented";
        });

        // AB Line Guidance Commands - Flyout Menu (need State.UI access)
        ShowTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Tracks);
        });

        CloseTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Track management commands - delegate to Tracks
        DeleteSelectedTrackCommand = Tracks.DeleteSelectedTrackCommand;
        SwapABPointsCommand = Tracks.SwapABPointsCommand;
        SelectTrackAsActiveCommand = Tracks.SelectTrackAsActiveCommand;

        // Quick AB selector dialogs (need State.UI access)
        ShowQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.QuickABSelector);
        });

        CloseQuickABSelectorCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        ShowDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.DrawAB);
        });

        CloseDrawABDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // AB Line creation commands - delegate to Tracks
        StartNewABLineCommand = Tracks.StartNewABLineCommand;
        StartNewABCurveCommand = Tracks.StartNewABCurveCommand;
        StartAPlusLineCommand = Tracks.StartAPlusLineCommand;
        StartDriveABCommand = Tracks.StartDriveABCommand;
        StartCurveRecordingCommand = Tracks.StartCurveRecordingCommand;
        StartDrawABModeCommand = Tracks.StartDrawABModeCommand;
        SetABPointCommand = Tracks.SetABPointCommand;
        CancelABCreationCommand = Tracks.CancelABCreationCommand;

        // Track editing commands - delegate to Tracks
        CycleABLinesCommand = Tracks.CycleABLinesCommand;
        SmoothABLineCommand = Tracks.SmoothABLineCommand;

        // Nudge commands - delegate to Tracks
        NudgeLeftCommand = Tracks.NudgeLeftCommand;
        NudgeRightCommand = Tracks.NudgeRightCommand;
        FineNudgeLeftCommand = Tracks.FineNudgeLeftCommand;
        FineNudgeRightCommand = Tracks.FineNudgeRightCommand;

        // Bottom Strip Commands (matching AgOpenGPS panelBottom)
        ChangeMappingColorCommand = new RelayCommand(() =>
        {
            StatusMessage = "Section Mapping Color - not yet implemented";
        });

        SnapToPivotCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Pivot - not yet implemented";
        });

        ToggleYouSkipCommand = new RelayCommand(() =>
        {
            StatusMessage = "YouSkip Toggle - not yet implemented";
        });

        ToggleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            IsUTurnSkipRowsEnabled = !IsUTurnSkipRowsEnabled;
            StatusMessage = IsUTurnSkipRowsEnabled
                ? $"U-Turn skip rows: ON ({UTurnSkipRows} rows)"
                : "U-Turn skip rows: OFF";
        });

        CycleUTurnSkipRowsCommand = new RelayCommand(() =>
        {
            // Cycle through 0-9, wrap back to 0 after 9
            UTurnSkipRows = (UTurnSkipRows + 1) % 10;
            StatusMessage = $"Skip rows: {UTurnSkipRows}";
        });

        // Flags Commands
        PlaceRedFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Red Flag - not yet implemented";
        });

        PlaceGreenFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Green Flag - not yet implemented";
        });

        PlaceYellowFlagCommand = new RelayCommand(() =>
        {
            StatusMessage = "Place Yellow Flag - not yet implemented";
        });

        DeleteAllFlagsCommand = new RelayCommand(() =>
        {
            StatusMessage = "Delete All Flags - not yet implemented";
        });
    }
}
