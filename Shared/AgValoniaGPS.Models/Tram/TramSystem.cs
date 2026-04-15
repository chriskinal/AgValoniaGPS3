// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AgValoniaGPS.Models.Tram;

/// <summary>
/// A tram system defines a set of tram lines generated from a reference track or boundary.
/// Multiple systems can coexist (e.g., sprayer wheel tracks + fertilizer tracks).
/// </summary>
public class TramSystem : INotifyPropertyChanged
{
    private string _name = "";
    private string? _referenceTrackName;
    private int _referenceBoundaryIndex = -1; // -1 = use track, 0 = outer boundary, 1+ = inner
    private double _tramWidth = 24.0;
    private TramSystemMode _mode = TramSystemMode.TrackLine;
    private double _offset;
    private TramDirection _direction = TramDirection.Symmetric;
    private int _passCount; // 0 = unlimited
    private bool _isEnabled = true;

    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Name of the reference track. Null = use boundary.
    /// </summary>
    public string? ReferenceTrackName
    {
        get => _referenceTrackName;
        set { if (_referenceTrackName != value) { _referenceTrackName = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Boundary index when using boundary as reference.
    /// -1 = use track (ReferenceTrackName), 0 = outer boundary, 1+ = inner boundary.
    /// </summary>
    public int ReferenceBoundaryIndex
    {
        get => _referenceBoundaryIndex;
        set { if (_referenceBoundaryIndex != value) { _referenceBoundaryIndex = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Sprayer/spreader boom width in meters. Determines spacing between tram line pairs.
    /// </summary>
    public double TramWidth
    {
        get => _tramWidth;
        set { if (Math.Abs(_tramWidth - value) > 0.001) { _tramWidth = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Whether tram lines represent the center of wheel tracks or the edge of the implement.
    /// </summary>
    public TramSystemMode Mode
    {
        get => _mode;
        set { if (_mode != value) { _mode = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Lateral offset in meters from the reference line.
    /// </summary>
    public double Offset
    {
        get => _offset;
        set { if (Math.Abs(_offset - value) > 0.001) { _offset = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Which side of the reference to generate tram lines.
    /// </summary>
    public TramDirection Direction
    {
        get => _direction;
        set { if (_direction != value) { _direction = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Number of passes to generate. 0 = unlimited (fills field width).
    /// </summary>
    public int PassCount
    {
        get => _passCount;
        set { if (_passCount != value) { _passCount = Math.Max(0, value); OnPropertyChanged(); } }
    }

    /// <summary>
    /// Whether this system is active (generates lines).
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// How tram lines are positioned relative to wheel tracks.
/// </summary>
public enum TramSystemMode
{
    /// <summary>Two lines per pass at wheel center positions (+/- halfWheelTrack from pass center)</summary>
    TrackLine = 0,
    /// <summary>Single line at implement edge position</summary>
    Edge = 1
}

/// <summary>
/// Which side of the reference to generate tram lines.
/// </summary>
public enum TramDirection
{
    /// <summary>Generate on both sides of reference</summary>
    Symmetric = 0,
    /// <summary>Generate only on left side</summary>
    Left = 1,
    /// <summary>Generate only on right side</summary>
    Right = 2
}
