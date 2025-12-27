using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ReactiveUI;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.State;
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// ViewModel for track and AB line management.
/// Handles track list, active track state, AB line/curve creation, and nudge commands.
/// </summary>
public class TrackManagementViewModel : ReactiveObject
{
    private readonly IFieldService _fieldService;
    private readonly ApplicationState _appState;

    // Events for MainViewModel to subscribe to
    public event EventHandler<string>? StatusMessageChanged;

    // Delegate to get current GPS position from MainViewModel
    public Func<(double Latitude, double Longitude, double Easting, double Northing, double Heading)>? GetCurrentPosition { get; set; }

    public TrackManagementViewModel(
        IFieldService fieldService,
        ApplicationState appState)
    {
        _fieldService = fieldService;
        _appState = appState;

        InitializeCommands();
    }

    #region Track Collection

    /// <summary>
    /// Collection of saved tracks for the current field.
    /// </summary>
    public ObservableCollection<Track> SavedTracks { get; } = new();

    private Track? _selectedTrack;
    /// <summary>
    /// Currently selected track in the tracks dialog.
    /// </summary>
    public Track? SelectedTrack
    {
        get => _selectedTrack;
        set
        {
            var oldValue = _selectedTrack;
            if (this.RaiseAndSetIfChanged(ref _selectedTrack, value) != oldValue)
            {
                var oldA = oldValue?.Points.FirstOrDefault();
                var oldB = oldValue?.Points.LastOrDefault();
                var newA = value?.Points.FirstOrDefault();
                var newB = value?.Points.LastOrDefault();
                Console.WriteLine($"[SelectedTrack] Changed from A({oldA?.Easting:F1},{oldA?.Northing:F1}) B({oldB?.Easting:F1},{oldB?.Northing:F1})");
                Console.WriteLine($"[SelectedTrack]       to A({newA?.Easting:F1},{newA?.Northing:F1}) B({newB?.Easting:F1},{newB?.Northing:F1})");
            }
        }
    }

    private bool _hasActiveTrack;
    /// <summary>
    /// True when an AB line or track is active for guidance.
    /// </summary>
    public bool HasActiveTrack
    {
        get => _hasActiveTrack;
        set => this.RaiseAndSetIfChanged(ref _hasActiveTrack, value);
    }

    private bool _isNudgeEnabled;
    /// <summary>
    /// True when AB line nudging is enabled.
    /// </summary>
    public bool IsNudgeEnabled
    {
        get => _isNudgeEnabled;
        set => this.RaiseAndSetIfChanged(ref _isNudgeEnabled, value);
    }

    #endregion

    #region AB Creation State

    private ABCreationMode _currentABCreationMode = ABCreationMode.None;
    /// <summary>
    /// Current AB line creation mode.
    /// </summary>
    public ABCreationMode CurrentABCreationMode
    {
        get => _currentABCreationMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentABCreationMode, value);
            this.RaisePropertyChanged(nameof(IsCreatingABLine));
            this.RaisePropertyChanged(nameof(EnableABClickSelection));
            this.RaisePropertyChanged(nameof(ABCreationInstructions));
        }
    }

    private ABPointStep _currentABPointStep = ABPointStep.None;
    /// <summary>
    /// Current step in AB point creation.
    /// </summary>
    public ABPointStep CurrentABPointStep
    {
        get => _currentABPointStep;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentABPointStep, value);
            this.RaisePropertyChanged(nameof(ABCreationInstructions));
        }
    }

    private Position? _pendingPointA;
    /// <summary>
    /// Temporary storage for Point A during AB creation.
    /// </summary>
    public Position? PendingPointA
    {
        get => _pendingPointA;
        set => this.RaiseAndSetIfChanged(ref _pendingPointA, value);
    }

    /// <summary>
    /// True when in any AB line creation mode.
    /// </summary>
    public bool IsCreatingABLine => CurrentABCreationMode != ABCreationMode.None;

    /// <summary>
    /// True when click/tap selection is enabled for AB creation.
    /// </summary>
    public bool EnableABClickSelection => CurrentABCreationMode == ABCreationMode.DrawAB ||
                                          CurrentABCreationMode == ABCreationMode.DriveAB;

    /// <summary>
    /// Instructions text for current AB creation step.
    /// </summary>
    public string ABCreationInstructions
    {
        get
        {
            return (CurrentABCreationMode, CurrentABPointStep) switch
            {
                (ABCreationMode.DriveAB, ABPointStep.SettingPointA) => "Tap screen to set Point A at current position",
                (ABCreationMode.DriveAB, ABPointStep.SettingPointB) => "Drive to B, then tap screen to set Point B",
                (ABCreationMode.DrawAB, ABPointStep.SettingPointA) => "Tap on map to place Point A",
                (ABCreationMode.DrawAB, ABPointStep.SettingPointB) => "Tap on map to place Point B",
                (ABCreationMode.Curve, _) => "Drive along curve path, then tap to finish",
                _ => string.Empty
            };
        }
    }

    #endregion

    #region Commands

    // Track management commands
    public ICommand? DeleteSelectedTrackCommand { get; private set; }
    public ICommand? SwapABPointsCommand { get; private set; }
    public ICommand? SelectTrackAsActiveCommand { get; private set; }

    // AB Line creation commands
    public ICommand? StartNewABLineCommand { get; private set; }
    public ICommand? StartNewABCurveCommand { get; private set; }
    public ICommand? StartAPlusLineCommand { get; private set; }
    public ICommand? StartDriveABCommand { get; private set; }
    public ICommand? StartCurveRecordingCommand { get; private set; }
    public ICommand? StartDrawABModeCommand { get; private set; }
    public ICommand? SetABPointCommand { get; private set; }
    public ICommand? CancelABCreationCommand { get; private set; }

    // Track editing commands
    public ICommand? CycleABLinesCommand { get; private set; }
    public ICommand? SmoothABLineCommand { get; private set; }

    // Nudge commands
    public ICommand? NudgeLeftCommand { get; private set; }
    public ICommand? NudgeRightCommand { get; private set; }
    public ICommand? FineNudgeLeftCommand { get; private set; }
    public ICommand? FineNudgeRightCommand { get; private set; }

    // Snap commands
    public ICommand? SnapLeftCommand { get; private set; }
    public ICommand? SnapRightCommand { get; private set; }

    // Dialog events (delegate to State.UI from MainViewModel)
    public event EventHandler? ShowTracksDialogRequested;
    public event EventHandler? CloseDialogRequested;
    public event EventHandler? ShowQuickABSelectorRequested;
    public event EventHandler? ShowDrawABDialogRequested;

    // Event for when a track is activated (MainViewModel can enable autosteer)
    public event EventHandler? TrackActivated;

    private void InitializeCommands()
    {
        // Track management commands
        DeleteSelectedTrackCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null)
            {
                SavedTracks.Remove(SelectedTrack);
                SelectedTrack = null;
                SaveTracksToFile();
                StatusMessageChanged?.Invoke(this, "Track deleted");
            }
        });

        SwapABPointsCommand = new RelayCommand(() =>
        {
            if (SelectedTrack != null && SelectedTrack.Points.Count >= 2)
            {
                SelectedTrack.Points.Reverse();
                StatusMessageChanged?.Invoke(this, $"Swapped A/B points for {SelectedTrack.Name}");
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
                StatusMessageChanged?.Invoke(this, $"Activated track: {SelectedTrack.Name}");
                TrackActivated?.Invoke(this, EventArgs.Empty);
                CloseDialogRequested?.Invoke(this, EventArgs.Empty);
            }
        });

        // AB Line creation commands
        StartNewABLineCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Starting new AB Line - not yet implemented");
        });

        StartNewABCurveCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Starting new AB Curve - not yet implemented");
        });

        StartAPlusLineCommand = new RelayCommand(() =>
        {
            CloseDialogRequested?.Invoke(this, EventArgs.Empty);
            StatusMessageChanged?.Invoke(this, "A+ Line mode: Line created from current position and heading");
            // TODO: Create AB line from current position using current heading
        });

        StartDriveABCommand = new RelayCommand(() =>
        {
            CloseDialogRequested?.Invoke(this, EventArgs.Empty);
            CurrentABCreationMode = ABCreationMode.DriveAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessageChanged?.Invoke(this, ABCreationInstructions);
        });

        StartCurveRecordingCommand = new RelayCommand(() =>
        {
            CloseDialogRequested?.Invoke(this, EventArgs.Empty);
            StatusMessageChanged?.Invoke(this, "Curve mode: Start driving to record curve path");
            // TODO: Start curve recording mode
        });

        StartDrawABModeCommand = new RelayCommand(() =>
        {
            CloseDialogRequested?.Invoke(this, EventArgs.Empty);
            CurrentABCreationMode = ABCreationMode.DrawAB;
            CurrentABPointStep = ABPointStep.SettingPointA;
            PendingPointA = null;
            StatusMessageChanged?.Invoke(this, ABCreationInstructions);
        });

        SetABPointCommand = new RelayCommand<object?>(param =>
        {
            Console.WriteLine($"[SetABPointCommand] Called with param={param?.GetType().Name ?? "null"}, Mode={CurrentABCreationMode}, Step={CurrentABPointStep}");

            if (CurrentABCreationMode == ABCreationMode.None)
            {
                Console.WriteLine("[SetABPointCommand] Mode is None, returning");
                return;
            }

            Position pointToSet;

            if (CurrentABCreationMode == ABCreationMode.DriveAB)
            {
                // Use current GPS position via delegate
                var pos = GetCurrentPosition?.Invoke() ?? (0, 0, 0, 0, 0);
                pointToSet = new Position
                {
                    Latitude = pos.Latitude,
                    Longitude = pos.Longitude,
                    Easting = pos.Easting,
                    Northing = pos.Northing,
                    Heading = pos.Heading
                };
                Console.WriteLine($"[SetABPointCommand] DriveAB - GPS position: E={pos.Easting:F2}, N={pos.Northing:F2}");
            }
            else if (CurrentABCreationMode == ABCreationMode.DrawAB && param is Position mapPos)
            {
                pointToSet = mapPos;
                Console.WriteLine($"[SetABPointCommand] DrawAB - Map position: E={mapPos.Easting:F2}, N={mapPos.Northing:F2}");
            }
            else
            {
                Console.WriteLine($"[SetABPointCommand] Invalid state - returning");
                return;
            }

            if (CurrentABPointStep == ABPointStep.SettingPointA)
            {
                PendingPointA = pointToSet;
                CurrentABPointStep = ABPointStep.SettingPointB;
                StatusMessageChanged?.Invoke(this, ABCreationInstructions);
                Console.WriteLine($"[SetABPointCommand] Set Point A: E={pointToSet.Easting:F2}, N={pointToSet.Northing:F2}");
            }
            else if (CurrentABPointStep == ABPointStep.SettingPointB)
            {
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
                    SaveTracksToFile();
                    HasActiveTrack = true;
                    StatusMessageChanged?.Invoke(this, $"Created AB line: {newTrack.Name} ({heading:F1}°)");
                    TrackActivated?.Invoke(this, EventArgs.Empty);
                    Console.WriteLine($"[SetABPointCommand] Created AB Line: {newTrack.Name}, A=({PendingPointA.Easting:F2},{PendingPointA.Northing:F2}), B=({pointToSet.Easting:F2},{pointToSet.Northing:F2}), Heading={heading:F1}°");

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
            StatusMessageChanged?.Invoke(this, "AB line creation cancelled");
        });

        // Track editing commands
        CycleABLinesCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Cycle AB Lines - not yet implemented");
        });

        SmoothABLineCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Smooth AB Line - not yet implemented");
        });

        // Nudge commands
        NudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Nudge Left - not yet implemented");
        });

        NudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Nudge Right - not yet implemented");
        });

        FineNudgeLeftCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Fine Nudge Left - not yet implemented");
        });

        FineNudgeRightCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Fine Nudge Right - not yet implemented");
        });

        // Snap commands
        SnapLeftCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Snap to Left Track - not yet implemented");
        });

        SnapRightCommand = new RelayCommand(() =>
        {
            StatusMessageChanged?.Invoke(this, "Snap to Right Track - not yet implemented");
        });
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Save tracks to TrackLines.txt in the active field directory.
    /// </summary>
    public void SaveTracksToFile()
    {
        var activeField = _fieldService.ActiveField;
        if (activeField == null || string.IsNullOrEmpty(activeField.DirectoryPath))
        {
            return;
        }

        try
        {
            Services.TrackFilesService.SaveTracks(activeField.DirectoryPath, SavedTracks.ToList());
            Console.WriteLine($"[TrackFiles] Saved {SavedTracks.Count} tracks to TrackLines.txt");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrackFiles] Failed to save tracks: {ex.Message}");
        }
    }

    /// <summary>
    /// Load tracks from field directory.
    /// </summary>
    public void LoadTracksFromField(Field? field)
    {
        // Clear existing tracks
        _appState.Field.Tracks.Clear();
        SavedTracks.Clear();

        if (field == null || string.IsNullOrEmpty(field.DirectoryPath))
        {
            Console.WriteLine("[TrackFiles] No field directory to load from");
            return;
        }

        try
        {
            // Try TrackLines.txt first (WinForms format)
            if (Services.TrackFilesService.Exists(field.DirectoryPath))
            {
                var tracks = Services.TrackFilesService.LoadTracks(field.DirectoryPath);
                int loadedCount = 0;

                foreach (var track in tracks)
                {
                    if (loadedCount == 0)
                    {
                        track.IsActive = true;
                        _appState.Field.ActiveTrack = track;
                    }
                    _appState.Field.Tracks.Add(track);
                    SavedTracks.Add(track);
                    loadedCount++;
                }

                Console.WriteLine($"[TrackFiles] Loaded {loadedCount} tracks from TrackLines.txt");

                if (loadedCount > 0)
                {
                    HasActiveTrack = true;
                    TrackActivated?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            // Fallback to legacy ABLines.txt format
            var legacyFilePath = System.IO.Path.Combine(field.DirectoryPath, "ABLines.txt");
            if (System.IO.File.Exists(legacyFilePath))
            {
                Console.WriteLine($"[TrackFiles] TrackLines.txt not found, trying legacy ABLines.txt");
                var lines = System.IO.File.ReadAllLines(legacyFilePath);
                int loadedCount = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(',');
                    if (parts.Length >= 5)
                    {
                        try
                        {
                            string name = parts[0].Trim();
                            double easting = double.Parse(parts[1]);
                            double northing = double.Parse(parts[2]);
                            double heading = double.Parse(parts[3]) * Math.PI / 180.0;

                            var pointA = new Vec3(easting, northing, heading);
                            var pointB = new Vec3(easting + Math.Sin(heading) * 1000,
                                                 northing + Math.Cos(heading) * 1000, heading);

                            var track = Track.FromABLine(name, pointA, pointB);
                            if (loadedCount == 0)
                            {
                                track.IsActive = true;
                                _appState.Field.ActiveTrack = track;
                            }
                            _appState.Field.Tracks.Add(track);
                            SavedTracks.Add(track);
                            loadedCount++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[TrackFiles] Failed to parse legacy AB line: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"[TrackFiles] Loaded {loadedCount} tracks from legacy ABLines.txt");

                if (loadedCount > 0)
                {
                    HasActiveTrack = true;
                    TrackActivated?.Invoke(this, EventArgs.Empty);
                }
            }
            else
            {
                Console.WriteLine("[TrackFiles] No track files found");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TrackFiles] Failed to load tracks: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the active track for guidance calculations.
    /// </summary>
    public Track? GetActiveTrack()
    {
        return SavedTracks.FirstOrDefault(t => t.IsActive);
    }

    /// <summary>
    /// Deactivate all tracks.
    /// </summary>
    public void DeactivateAllTracks()
    {
        foreach (var track in SavedTracks)
        {
            track.IsActive = false;
        }
        HasActiveTrack = false;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Calculate heading (in degrees) from point A to point B using Easting/Northing.
    /// </summary>
    private static double CalculateHeading(Position pointA, Position pointB)
    {
        double dx = pointB.Easting - pointA.Easting;
        double dy = pointB.Northing - pointA.Northing;
        double headingRadians = Math.Atan2(dx, dy);
        double headingDegrees = headingRadians * 180.0 / Math.PI;
        if (headingDegrees < 0) headingDegrees += 360.0;
        return headingDegrees;
    }

    #endregion
}
