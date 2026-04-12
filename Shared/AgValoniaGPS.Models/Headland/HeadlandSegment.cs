// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Models.Headland;

/// <summary>
/// Type of headland segment.
/// </summary>
public enum HeadlandSegmentType
{
    /// <summary>Straight line between two boundary points, offset inward.</summary>
    Line,

    /// <summary>Curve following boundary edge between two points, offset inward.</summary>
    Curve,

    /// <summary>Full boundary offset (entire boundary polygon offset inward).</summary>
    Boundary
}

/// <summary>
/// A single headland segment - a line or curve along a boundary edge
/// with an inward offset distance. Multiple segments form the headland.
/// </summary>
public class HeadlandSegment : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _name = "";
    public string Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    private HeadlandSegmentType _type;
    public HeadlandSegmentType Type
    {
        get => _type;
        set { if (_type != value) { _type = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Points along the boundary edge that define this segment.
    /// </summary>
    public List<Vec3> BoundaryPoints { get; set; } = new();

    /// <summary>
    /// The resulting offset points (the actual headland line).
    /// </summary>
    public List<Vec3> OffsetPoints { get; set; } = new();

    private double _offset = 12.0;
    /// <summary>
    /// Inward offset distance in meters from the boundary edge.
    /// </summary>
    public double Offset
    {
        get => _offset;
        set { if (_offset != value) { _offset = value; OnPropertyChanged(); } }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public int BoundaryStartIndex { get; set; } = -1;
    public int BoundaryEndIndex { get; set; } = -1;
    public int BoundaryIndex { get; set; }

    private double _startExtension = 50;
    public double StartExtension
    {
        get => _startExtension;
        set { if (_startExtension != value) { _startExtension = value; OnPropertyChanged(); } }
    }

    private double _endExtension = 50;
    public double EndExtension
    {
        get => _endExtension;
        set { if (_endExtension != value) { _endExtension = value; OnPropertyChanged(); } }
    }

    /// <summary>
    /// Whether this segment intersects the headland at both ends, forming a loop.
    /// Set by BuildHeadlandFromSegments. False = doesn't affect headland, shown as red.
    /// </summary>
    public bool IsEffective { get; set; }
}
