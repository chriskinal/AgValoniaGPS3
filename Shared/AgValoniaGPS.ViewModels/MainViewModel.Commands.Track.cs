using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.GPS;
using AgValoniaGPS.Models.Ntrip;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Track management commands - AB lines, curves, track dialogs, nudging, guidance controls
/// </summary>
public partial class MainViewModel
{
    // Track Dialog Commands
    public ICommand? ShowTracksDialogCommand { get; private set; }
    public ICommand? CloseTracksDialogCommand { get; private set; }
    public ICommand? ShowQuickABSelectorCommand { get; private set; }
    public ICommand? CloseQuickABSelectorCommand { get; private set; }
    public ICommand? ShowDrawABDialogCommand { get; private set; }
    public ICommand? CloseDrawABDialogCommand { get; private set; }

    // Track Management Commands
    public ICommand? DeleteSelectedTrackCommand { get; private set; }
    public ICommand? SwapABPointsCommand { get; private set; }
    public ICommand? SelectTrackAsActiveCommand { get; private set; }

    // AB Line Creation Commands
    public ICommand? StartNewABLineCommand { get; private set; }
    public ICommand? StartNewABCurveCommand { get; private set; }
    public ICommand? StartAPlusLineCommand { get; private set; }
    public ICommand? StartDriveABCommand { get; private set; }
    public ICommand? StartCurveRecordingCommand { get; private set; }
    public ICommand? StartDrawABModeCommand { get; private set; }
    public ICommand? SetABPointCommand { get; private set; }
    public ICommand? CancelABCreationCommand { get; private set; }
    public ICommand? CycleABLinesCommand { get; private set; }
    public ICommand? SmoothABLineCommand { get; private set; }

    // Nudge Commands
    public ICommand? NudgeLeftCommand { get; private set; }
    public ICommand? NudgeRightCommand { get; private set; }
    public ICommand? FineNudgeLeftCommand { get; private set; }
    public ICommand? FineNudgeRightCommand { get; private set; }

    // Guidance Commands
    public ICommand? SnapLeftCommand { get; private set; }
    public ICommand? SnapRightCommand { get; private set; }
    public ICommand? StopGuidanceCommand { get; private set; }
    public ICommand? UTurnCommand { get; private set; }

    // Bottom Strip Commands
    public ICommand? ChangeMappingColorCommand { get; private set; }
    public ICommand? SnapToPivotCommand { get; private set; }
    public ICommand? ToggleYouSkipCommand { get; private set; }
    public ICommand? ToggleUTurnSkipRowsCommand { get; private set; }
    public ICommand? CycleUTurnSkipRowsCommand { get; private set; }

    // Flags Commands
    public ICommand? PlaceRedFlagCommand { get; private set; }
    public ICommand? PlaceGreenFlagCommand { get; private set; }
    public ICommand? PlaceYellowFlagCommand { get; private set; }
    public ICommand? DeleteAllFlagsCommand { get; private set; }

    // Section Control Commands
    public ICommand? ToggleContourModeCommand { get; private set; }
    public ICommand? DeleteContoursCommand { get; private set; }
    public ICommand? DeleteAppliedAreaCommand { get; private set; }
    public ICommand? ToggleManualModeCommand { get; private set; }
    public ICommand? ToggleSectionMasterCommand { get; private set; }
    public ICommand? ToggleSectionCommand { get; private set; }
    public ICommand? ToggleYouTurnCommand { get; private set; }
    public ICommand? ToggleAutoSteerCommand { get; private set; }

    // NTRIP Profile Commands
    public ICommand? ShowNtripProfilesDialogCommand { get; private set; }
    public ICommand? CloseNtripProfilesDialogCommand { get; private set; }
    public ICommand? AddNtripProfileCommand { get; private set; }
    public ICommand? EditNtripProfileCommand { get; private set; }
    public ICommand? DeleteNtripProfileCommand { get; private set; }
    public ICommand? SetDefaultNtripProfileCommand { get; private set; }
    public ICommand? SaveNtripProfileCommand { get; private set; }
    public ICommand? CancelNtripProfileEditCommand { get; private set; }
    public ICommand? TestNtripConnectionCommand { get; private set; }

    private void InitializeTrackCommands()
    {
        // Track Dialog Commands
        ShowTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.ShowDialog(DialogType.Tracks);
        });

        CloseTracksDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
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

        // Track Management Commands
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
            // Toggle: if track is already active, deactivate it; otherwise activate
            if (SelectedTrack != null)
            {
                if (SelectedTrack.IsActive)
                {
                    // Deactivate
                    SelectedTrack = null;
                    StatusMessage = "Track deactivated";
                }
                else
                {
                    // Activate (SelectedTrack setter handles IsActive sync)
                    StatusMessage = $"Activated track: {SelectedTrack.Name}";
                }
                State.UI.CloseDialog();
            }
        });

        // AB Line Creation Commands
        StartNewABLineCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Line - not yet implemented";
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            StatusMessage = "Starting new AB Curve - not yet implemented";
        });

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
            _logger.LogDebug($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                _logger.LogDebug("[SetABPointCommand] Mode is None, returning");
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
                _logger.LogDebug($"[SetABPointCommand] DriveAB - GPS position: E={Easting:F2}, N={Northing:F2}");
            }
            else if (CurrentABCreationMode == ABCreationMode.DrawAB && param is Position mapPos)
            {
                // Use the tapped map position
                pointToSet = mapPos;
                _logger.LogDebug($"[SetABPointCommand] DrawAB - Map position: E={mapPos.Easting:F2}, N={mapPos.Northing:F2}");
            }
            else
            {
                _logger.LogDebug($"[SetABPointCommand] Invalid state - returning");
                return; // Invalid state
            }

            if (CurrentABPointStep == ABPointStep.SettingPointA)
            {
                // Store Point A and move to Point B
                PendingPointA = pointToSet;
                CurrentABPointStep = ABPointStep.SettingPointB;
                StatusMessage = ABCreationInstructions;
                _logger.LogDebug($"[SetABPointCommand] Set Point A: E={pointToSet.Easting:F2}, N={pointToSet.Northing:F2}");
            }
            else if (CurrentABPointStep == ABPointStep.SettingPointB)
            {
                // Create the AB line with Point A and Point B
                if (PendingPointA != null)
                {
                    var heading = CalculateHeading(PendingPointA, pointToSet);
                    var headingRadians = heading * Math.PI / 180.0;
                    var newTrack = Track.FromABLine(
                        $"AB_{heading:F1} {DateTime.Now:HH:mm:ss}",
                        new Vec3(PendingPointA.Easting, PendingPointA.Northing, headingRadians),
                        new Vec3(pointToSet.Easting, pointToSet.Northing, headingRadians));
                    newTrack.IsActive = true;

                    SavedTracks.Add(newTrack);
                    SaveTracksToFile(); // Persist to disk
                    HasActiveTrack = true;
                    IsAutoSteerAvailable = true;
                    StatusMessage = $"Created AB line: {newTrack.Name} ({heading:F1})";
                    _logger.LogDebug($"[SetABPointCommand] Created AB Line: {newTrack.Name}, A=({PendingPointA.Easting:F2},{PendingPointA.Northing:F2}), B=({pointToSet.Easting:F2},{pointToSet.Northing:F2}), Heading={heading:F1}");

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

        // Nudge Commands
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

        // Guidance Commands
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

        // Bottom Strip Commands
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

        // Section Control Commands
        ToggleContourModeCommand = new RelayCommand(() =>
        {
            IsContourModeOn = !IsContourModeOn;
            StatusMessage = IsContourModeOn ? "Contour mode ON" : "Contour mode OFF";
        });

        DeleteContoursCommand = new RelayCommand(() =>
        {
            _coverageMapService.ClearAll();
            StatusMessage = "Coverage/contours cleared";
        });

        DeleteAppliedAreaCommand = new RelayCommand(async () =>
        {
            // Show confirmation dialog
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Applied Area",
                "Are you sure you want to delete all applied area coverage? This cannot be undone.");

            if (!confirmed)
                return;

            // Clear in-memory coverage
            _coverageMapService.ClearAll();

            // Delete Sections.txt file from current field
            if (State.Field.ActiveField != null)
            {
                var sectionsFile = System.IO.Path.Combine(State.Field.ActiveField.DirectoryPath, "Sections.txt");
                if (System.IO.File.Exists(sectionsFile))
                {
                    try
                    {
                        System.IO.File.Delete(sectionsFile);
                        _logger.LogDebug($"[Coverage] Deleted {sectionsFile}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug($"[Coverage] Error deleting Sections.txt: {ex.Message}");
                    }
                }
            }

            // Refresh statistics display
            RefreshCoverageStatistics();
            StatusMessage = "Applied area deleted";
        });

        // Manual All button: Toggle all sections between On (green) and Off (red)
        ToggleManualModeCommand = new RelayCommand(() =>
        {
            IsManualSectionMode = !IsManualSectionMode;

            // Turn off Auto button if turning on Manual
            if (IsManualSectionMode)
                IsSectionMasterOn = false;

            // Set all sections
            var newState = IsManualSectionMode ? SectionButtonState.On : SectionButtonState.Off;
            for (int i = 0; i < _sectionControlService.NumSections; i++)
            {
                _sectionControlService.SetSectionState(i, newState);
            }

            StatusMessage = IsManualSectionMode ? "All sections ON" : "All sections OFF";
        });

        // Auto All button: Toggle all sections between Auto (yellow) and Off (red)
        ToggleSectionMasterCommand = new RelayCommand(() =>
        {
            IsSectionMasterOn = !IsSectionMasterOn;

            // Turn off Manual button if turning on Auto
            if (IsSectionMasterOn)
                IsManualSectionMode = false;

            // Set all sections
            var newState = IsSectionMasterOn ? SectionButtonState.Auto : SectionButtonState.Off;
            for (int i = 0; i < _sectionControlService.NumSections; i++)
            {
                _sectionControlService.SetSectionState(i, newState);
            }

            StatusMessage = IsSectionMasterOn ? "All sections AUTO" : "All sections OFF";
        });

        // Parameterized command for toggling individual sections (0-based index)
        // Parameter comes as string from XAML CommandParameter, so we parse it
        ToggleSectionCommand = new RelayCommand<object>(param =>
        {
            if (param == null) return;

            int sectionIndex;
            if (param is int intVal)
                sectionIndex = intVal;
            else if (param is string strVal && int.TryParse(strVal, out var parsed))
                sectionIndex = parsed;
            else
                return;

            if (sectionIndex < 0 || sectionIndex >= _sectionControlService.NumSections)
                return;

            var currentState = _sectionControlService.SectionStates[sectionIndex].ButtonState;
            // Cycle through: Off -> Auto -> On -> Off
            var newState = currentState switch
            {
                SectionButtonState.Off => SectionButtonState.Auto,
                SectionButtonState.Auto => SectionButtonState.On,
                SectionButtonState.On => SectionButtonState.Off,
                _ => SectionButtonState.Off
            };

            _sectionControlService.SetSectionState(sectionIndex, newState);
            StatusMessage = $"Section {sectionIndex + 1}: {newState}";
        });

        ToggleYouTurnCommand = new RelayCommand(() =>
        {
            IsYouTurnEnabled = !IsYouTurnEnabled;
            StatusMessage = IsYouTurnEnabled ? "YouTurn enabled" : "YouTurn disabled";
        });

        ToggleAutoSteerCommand = new RelayCommand(() =>
        {
            if (!IsAutoSteerAvailable)
            {
                StatusMessage = "AutoSteer not available - no active track";
                return;
            }
            IsAutoSteerEngaged = !IsAutoSteerEngaged;
            StatusMessage = IsAutoSteerEngaged ? "AutoSteer ENGAGED" : "AutoSteer disengaged";
        });

        // NTRIP Profile Commands
        ShowNtripProfilesDialogCommand = new RelayCommand(() =>
        {
            RefreshNtripProfiles();
            State.UI.ShowDialog(DialogType.NtripProfiles);
        });

        CloseNtripProfilesDialogCommand = new RelayCommand(() =>
        {
            State.UI.CloseDialog();
            SelectedNtripProfile = null;
        });

        AddNtripProfileCommand = new RelayCommand(() =>
        {
            EditingNtripProfile = _ntripProfileService.CreateNewProfile("New Profile");
            PopulateAvailableFieldsForProfile(EditingNtripProfile);
            State.UI.ShowDialog(DialogType.NtripProfileEditor);
        });

        EditNtripProfileCommand = new RelayCommand(() =>
        {
            if (SelectedNtripProfile != null)
            {
                // Clone the profile for editing
                EditingNtripProfile = new NtripProfile
                {
                    Id = SelectedNtripProfile.Id,
                    Name = SelectedNtripProfile.Name,
                    CasterHost = SelectedNtripProfile.CasterHost,
                    CasterPort = SelectedNtripProfile.CasterPort,
                    MountPoint = SelectedNtripProfile.MountPoint,
                    Username = SelectedNtripProfile.Username,
                    Password = SelectedNtripProfile.Password,
                    AutoConnectOnFieldLoad = SelectedNtripProfile.AutoConnectOnFieldLoad,
                    IsDefault = SelectedNtripProfile.IsDefault,
                    AssociatedFields = new List<string>(SelectedNtripProfile.AssociatedFields),
                    FilePath = SelectedNtripProfile.FilePath
                };
                PopulateAvailableFieldsForProfile(EditingNtripProfile);
                State.UI.ShowDialog(DialogType.NtripProfileEditor);
            }
        });

        DeleteNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedNtripProfile != null)
            {
                var confirmed = await _dialogService.ShowConfirmationAsync(
                    "Delete NTRIP Profile",
                    $"Are you sure you want to delete the profile '{SelectedNtripProfile.Name}'?");
                if (!confirmed) return;

                await _ntripProfileService.DeleteProfileAsync(SelectedNtripProfile.Id);
                RefreshNtripProfiles();
                SelectedNtripProfile = null;
                StatusMessage = "NTRIP profile deleted";
            }
        });

        SetDefaultNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (SelectedNtripProfile != null)
            {
                await _ntripProfileService.SetDefaultProfileAsync(SelectedNtripProfile.Id);
                RefreshNtripProfiles();
                StatusMessage = $"Set '{SelectedNtripProfile.Name}' as default NTRIP profile";
            }
        });

        SaveNtripProfileCommand = new AsyncRelayCommand(async () =>
        {
            if (EditingNtripProfile != null)
            {
                // Update associated fields from the selection
                EditingNtripProfile.AssociatedFields = AvailableFieldsForProfile
                    .Where(f => f.IsSelected)
                    .Select(f => f.FieldName)
                    .ToList();

                await _ntripProfileService.SaveProfileAsync(EditingNtripProfile);
                RefreshNtripProfiles();
                EditingNtripProfile = null;
                AvailableFieldsForProfile.Clear();
                State.UI.ShowDialog(DialogType.NtripProfiles);
                StatusMessage = "NTRIP profile saved";
            }
        });

        CancelNtripProfileEditCommand = new RelayCommand(() =>
        {
            EditingNtripProfile = null;
            AvailableFieldsForProfile.Clear();
            NtripTestStatus = string.Empty;
            State.UI.ShowDialog(DialogType.NtripProfiles);
        });

        TestNtripConnectionCommand = new AsyncRelayCommand(async () =>
        {
            if (EditingNtripProfile == null) return;
            if (string.IsNullOrWhiteSpace(EditingNtripProfile.CasterHost))
            {
                NtripTestStatus = "Error: Caster host is required";
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingNtripProfile.MountPoint))
            {
                NtripTestStatus = "Error: Mount point is required";
                return;
            }

            IsTestingNtripConnection = true;
            NtripTestStatus = "Testing connection...";

            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(
                    EditingNtripProfile.CasterHost,
                    EditingNtripProfile.CasterPort);

                if (await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask)
                {
                    if (tcpClient.Connected)
                    {
                        // Try to get the mount point table to verify caster is responding
                        using var stream = tcpClient.GetStream();
                        var request = $"GET /{EditingNtripProfile.MountPoint} HTTP/1.1\r\n" +
                                    $"Host: {EditingNtripProfile.CasterHost}\r\n" +
                                    $"Ntrip-Version: Ntrip/2.0\r\n" +
                                    $"User-Agent: NTRIP AgValoniaGPS/Test\r\n";

                        if (!string.IsNullOrEmpty(EditingNtripProfile.Username))
                        {
                            var credentials = Convert.ToBase64String(
                                System.Text.Encoding.ASCII.GetBytes(
                                    $"{EditingNtripProfile.Username}:{EditingNtripProfile.Password}"));
                            request += $"Authorization: Basic {credentials}\r\n";
                        }
                        request += "\r\n";

                        var requestBytes = System.Text.Encoding.ASCII.GetBytes(request);
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                        // Read response with timeout
                        var buffer = new byte[1024];
                        stream.ReadTimeout = 3000;
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        var response = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (response.Contains("200 OK") || response.Contains("ICY 200"))
                        {
                            NtripTestStatus = "Success: Connected to caster and mount point";
                        }
                        else if (response.Contains("401"))
                        {
                            NtripTestStatus = "Error: Authentication failed (check username/password)";
                        }
                        else if (response.Contains("404"))
                        {
                            NtripTestStatus = "Error: Mount point not found";
                        }
                        else
                        {
                            NtripTestStatus = "Connected to caster (mount point status unknown)";
                        }
                    }
                    else
                    {
                        NtripTestStatus = "Error: Could not connect to caster";
                    }
                }
                else
                {
                    NtripTestStatus = "Error: Connection timed out";
                }
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                NtripTestStatus = $"Error: {ex.Message}";
            }
            catch (Exception ex)
            {
                NtripTestStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsTestingNtripConnection = false;
            }
        });
    }
}
