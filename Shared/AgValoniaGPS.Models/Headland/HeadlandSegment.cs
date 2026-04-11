// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Collections.Generic;
using AgValoniaGPS.Models.Base;
using ReactiveUI;

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
public class HeadlandSegment : ReactiveObject
{
    private string _name = "";
    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    private HeadlandSegmentType _type;
    public HeadlandSegmentType Type
    {
        get => _type;
        set => this.RaiseAndSetIfChanged(ref _type, value);
    }

    /// <summary>
    /// Points along the boundary edge that define this segment.
    /// For Line: 2 points (start/end on boundary).
    /// For Curve: N points extracted from boundary between start/end.
    /// For Boundary: all points of the boundary polygon.
    /// </summary>
    public List<Vec3> BoundaryPoints { get; set; } = new();

    /// <summary>
    /// The resulting offset points (the actual headland line).
    /// Computed from BoundaryPoints + Offset.
    /// </summary>
    public List<Vec3> OffsetPoints { get; set; } = new();

    private double _offset = 12.0;
    /// <summary>
    /// Inward offset distance in meters from the boundary edge.
    /// </summary>
    public double Offset
    {
        get => _offset;
        set => this.RaiseAndSetIfChanged(ref _offset, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    /// <summary>
    /// Index of first boundary point (for re-extraction after drag).
    /// </summary>
    public int BoundaryStartIndex { get; set; } = -1;

    /// <summary>
    /// Index of last boundary point (for re-extraction after drag).
    /// </summary>
    public int BoundaryEndIndex { get; set; } = -1;

    /// <summary>
    /// Which boundary polygon this segment is from (0 = outer, 1+ = inner).
    /// </summary>
    public int BoundaryIndex { get; set; }
}
