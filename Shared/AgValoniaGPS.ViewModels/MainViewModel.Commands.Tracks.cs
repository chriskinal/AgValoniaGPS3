using System;
using System.Windows.Input;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Partial class containing track and AB line guidance command initialization.
/// </summary>
public partial class MainViewModel
{
    private void InitializeTrackCommands()
    {
        // AB Line Guidance Commands - Bottom Bar
        SnapLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Left Track - not yet implemented";
        });

        SnapRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Snap to Right Track - not yet implemented";
        });

        StopGuidanceCommand = new RelayCommand(() =>
        {
            StatusMessage = "Guidance Stopped";
        });

        UTurnCommand = new RelayCommand(() =>
        {
            StatusMessage = "U-Turn - not yet implemented";
        });

        // AB Line Guidance Commands - Flyout Menu
        ShowTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Tracks);
        });

        CloseTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
        });

        // Track management commands
        DeleteSelectedTrackCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                SavedTracks.Remove(SelectedTrack);
                SelectedTrack = null;
                SaveTracksToFile(); // Persist deletion to disk
                StatusMessage = "Track deleted";
            }
        });

        SwapABPointsCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null && SelectedTrack.Points.Count >= 2)
            {
                // Reverse the points list to swap A and B
                SelectedTrack.Points.Reverse();
                StatusMessage = $"Swapped A/B points for {SelectedTrack.Name}";
            }
        });

        SelectTrackAsActiveCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                // Deactivate all tracks first
                foreach (var track in SavedTracks)
                {
                    track.IsActive = false;
                }
                // Activate the selected track
                SelectedTrack.IsActive = true;
                HasActiveTrack = true;
                IsAutoSteerAvailable = true;
                StatusMessage = $"Activated track: {SelectedTrack.Name}";
                State.UI.CloseDialog();
            }
        });

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

        StartNewABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Line - not yet implemented";
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Curve - not yet implemented";
        });

        // Quick AB Mode Commands
        StartAPlusLineCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            StatusMessage = "A+ Line mode: Line created from current position and heading";
            // TODO: Create AB line from current position using current heading
        });

        StartDriveABCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DriveAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        StartCurveRecordingCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            StatusMessage = "Curve mode: Start driving to record curve path";
            // TODO: Start curve recording mode
        });

        StartDrawABModeCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            CurrentABCreationMode = ABCreationMode.DrawAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessage = ABCreationInstructions;
        });

        // SetABPointCommand is called when user taps during AB creation mode
        // For DriveAB mode: uses current GPS position
        // For DrawAB mode: uses the tapped map coordinates (passed as parameter)
        SetABPointCommand = new RelayCommand<object?>(param =>
        {
            System.Console.WriteLine($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                System.Console.WriteLine("[SetABPointCommand] Mode is None, returning");
                return;
            }

            Position pointToSet;

            if (CurrentABCreationMode == ABCreationMode.DriveAB)
            {
                // Use current GPS position
                pointToSet = new Position
                {
                    Latitude = Latitude,
                    Longitude = Longitude,
                    Easting = Easting,
                    Northing = Northing,
                    Heading = Heading
                };
                System.Console.WriteLine($"[SetABPointCommand] DriveAB - GPS position: E={Easting:F2}, N={Northing:F2}");
            }
            else if (CurrentABCreationMode == ABCreationMode.DrawAB && param is Position mapPos)
            {
                // Use the tapped map position
                pointToSet = mapPos;
                System.Console.WriteLine($"[SetABPointCommand] DrawAB - Map position: E={mapPos.Easting:F2}, N={mapPos.Northing:F2}");
            }
            else
            {
                System.Console.WriteLine($"[SetABPointCommand] Invalid state - returning");
                return; // Invalid state
            }

            if (CurrentABPointStep == ABPointStep.SettingPointA)
            {
                // Store Point A and move to Point B
                PendingPointA = pointToSet;
                CurrentABPointStep = ABPointStep.SettingPointB;
                StatusMessage = ABCreationInstructions;
                System.Console.WriteLine($"[SetABPointCommand] Set Point A: E={pointToSet.Easting:F2}, N={pointToSet.Northing:F2}");
            }
            else if (CurrentABPointStep == ABPointStep.SettingPointB)
            {
                // Create the AB line with Point A and Point B
                if (PendingPointA != null)
                {
                    var heading = CalculateHeading(PendingPointA, pointToSet);
                    var headingRadians = heading * Math.PI / 180.0;
                    var newTrack = Track.FromABLine(
                        $"AB_{heading:F1}° {DateTime.Now:HH:mm:ss}",
                        new Vec3(PendingPointA.Easting, PendingPointA.Northing, headingRadians),
                        new Vec3(pointToSet.Easting, pointToSet.Northing, headingRadians));
                    newTrack.IsActive = true;

                    SavedTracks.Add(newTrack);
                    SaveTracksToFile(); // Persist to disk
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
                    StatusMessage = $"Created AB line: {newTrack.Name} ({heading:F1}°)";
                    System.Console.WriteLine($"[SetABPointCommand] Created AB Line: {newTrack.Name}, A=({PendingPointA.Easting:F2},{PendingPointA.Northing:F2}), B=({pointToSet.Easting:F2},{pointToSet.Northing:F2}), Heading={heading:F1}°");

                    // Reset state
                    CurrentABCreationMode = ABCreationMode.None;
                    CurrentABPointStep = ABPointStep.None;
                    PendingPointA = null;
                }
            }
        });

        CancelABCreationCommand = new RelayCommand(() =>
        {
            CurrentABCreationMode = ABCreationMode.None;
            CurrentABPointStep = ABPointStep.None;
            PendingPointA = null;
            StatusMessage = "AB line creation cancelled";
        });

        CycleABLinesCommand = new RelayCommand(() =>
        {
            StatusMessage = "Cycle AB Lines - not yet implemented";
        });

        SmoothABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Smooth AB Line - not yet implemented";
        });

        NudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Left - not yet implemented";
        });

        NudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Nudge Right - not yet implemented";
        });

        FineNudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Left - not yet implemented";
        });

        FineNudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessage = "Fine Nudge Right - not yet implemented";
        });

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
