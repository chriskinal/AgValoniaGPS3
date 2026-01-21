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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.Models.Track;
using ReactiveUI;

namespace AgValoniaGPS.Models.State;

/// <summary>
/// Active field state - boundaries, tracks, headlands.
/// </summary>
public class FieldState : ReactiveObject
{
    private Field? _activeField;
    public Field? ActiveField
    {
        get => _activeField;
        set
        {
            this.RaiseAndSetIfChanged(ref _activeField, value);
            this.RaisePropertyChanged(nameof(HasActiveField));
            this.RaisePropertyChanged(nameof(FieldName));
        }
    }

    public bool HasActiveField => ActiveField != null;
    public string FieldName => ActiveField?.Name ?? "No Field";

    // Field directory
    private string _fieldsRootDirectory = string.Empty;
    public string FieldsRootDirectory
    {
        get => _fieldsRootDirectory;
        set => this.RaiseAndSetIfChanged(ref _fieldsRootDirectory, value);
    }

    // Boundaries
    public ObservableCollection<Boundary> Boundaries { get; } = new();

    private Boundary? _currentBoundary;
    public Boundary? CurrentBoundary
    {
        get => _currentBoundary;
        set => this.RaiseAndSetIfChanged(ref _currentBoundary, value);
    }

    public bool HasBoundary => Boundaries.Count > 0;

    // Tracks (unified Track model)
    public ObservableCollection<Track.Track> Tracks { get; } = new();

    private Track.Track? _activeTrack;
    public Track.Track? ActiveTrack
    {
        get => _activeTrack;
        set => this.RaiseAndSetIfChanged(ref _activeTrack, value);
    }

    private Track.Track? _selectedTrack;
    public Track.Track? SelectedTrack
    {
        get => _selectedTrack;
        set => this.RaiseAndSetIfChanged(ref _selectedTrack, value);
    }

    public bool HasActiveTrack => ActiveTrack != null;

    // Headlands
    private List<Vec3>? _headlandLine;
    public List<Vec3>? HeadlandLine
    {
        get => _headlandLine;
        set
        {
            this.RaiseAndSetIfChanged(ref _headlandLine, value);
            this.RaisePropertyChanged(nameof(HasHeadland));
        }
    }

    private double _headlandDistance;
    public double HeadlandDistance
    {
        get => _headlandDistance;
        set => this.RaiseAndSetIfChanged(ref _headlandDistance, value);
    }

    public bool HasHeadland => HeadlandLine != null && HeadlandLine.Count > 0;

    // Field origin (local plane reference)
    private double _originLatitude;
    public double OriginLatitude
    {
        get => _originLatitude;
        set => this.RaiseAndSetIfChanged(ref _originLatitude, value);
    }

    private double _originLongitude;
    public double OriginLongitude
    {
        get => _originLongitude;
        set => this.RaiseAndSetIfChanged(ref _originLongitude, value);
    }

    // Local plane for coordinate conversion
    private LocalPlane? _localPlane;
    public LocalPlane? LocalPlane
    {
        get => _localPlane;
        set => this.RaiseAndSetIfChanged(ref _localPlane, value);
    }

    public void Reset()
    {
        ActiveField = null;
        Boundaries.Clear();
        CurrentBoundary = null;
        Tracks.Clear();
        ActiveTrack = null;
        SelectedTrack = null;
        HeadlandLine = null;
        HeadlandDistance = 0;
        OriginLatitude = OriginLongitude = 0;
        LocalPlane = null;
    }
}
