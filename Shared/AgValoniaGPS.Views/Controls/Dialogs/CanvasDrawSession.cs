// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;

namespace AgValoniaGPS.Views.Controls.Dialogs;

/// <summary>
/// What kind of thing we're drawing/editing on the canvas.
/// </summary>
public enum DrawTarget
{
    None,
    TrackABLine,
    TrackCurve,
    TrackAPlus,
    TrackBoundaryLine,
    TrackBoundaryCurve,
    HeadlandLine,
    HeadlandCurve,
    HeadlandBoundary
}

/// <summary>
/// Phase of the drawing operation.
/// </summary>
public enum DrawPhase
{
    /// <summary>Not drawing.</summary>
    Idle,
    /// <summary>Picking first point.</summary>
    PickingA,
    /// <summary>Picking second point.</summary>
    PickingB,
    /// <summary>Picking additional points (curve).</summary>
    PickingMore,
    /// <summary>Preview mode - can drag points, adjust offset/heading, create.</summary>
    Preview
}

/// <summary>
/// Encapsulates all shared state for drawing tracks and headland segments
/// on the Field Builder canvas. Replaces the many DrawMode enum states and
/// scattered state variables.
/// </summary>
public class CanvasDrawSession
{
    public DrawTarget Target { get; set; } = DrawTarget.None;
    public DrawPhase Phase { get; set; } = DrawPhase.Idle;

    /// <summary>Points being drawn (boundary or free-form).</summary>
    public List<Vec3> Points { get; } = new();

    /// <summary>Boundary polygon for snapping (null for free-form).</summary>
    public BoundaryPolygon? BoundaryPoly { get; set; }

    /// <summary>Boundary vertex indices for start/end.</summary>
    public int BoundaryStartIndex { get; set; } = -1;
    public int BoundaryEndIndex { get; set; } = -1;

    /// <summary>Offset distance for headland segments.</summary>
    public double Offset { get; set; } = 12.0;

    /// <summary>Heading angle for A+ Line.</summary>
    public double Heading { get; set; }

    /// <summary>Extension lengths for headland offset line ends.</summary>
    public double StartExtension { get; set; } = 50;
    public double EndExtension { get; set; } = 50;

    /// <summary>Backup for edit cancel.</summary>
    public Models.Headland.HeadlandSegment? BackupSegment { get; set; }
    public Models.Track.Track? BackupTrack { get; set; }

    /// <summary>Drag state.</summary>
    public int DragPointIndex { get; set; } = -1;
    public bool IsDragging { get; set; }
    public bool IsArrowDrag { get; set; }
    public bool IsArrowStart { get; set; }

    public bool IsActive => Target != DrawTarget.None;
    public bool IsPreview => Phase == DrawPhase.Preview;
    public bool IsHeadland => Target is DrawTarget.HeadlandLine or DrawTarget.HeadlandCurve or DrawTarget.HeadlandBoundary;
    public bool IsTrack => Target is DrawTarget.TrackABLine or DrawTarget.TrackCurve or DrawTarget.TrackAPlus
                           or DrawTarget.TrackBoundaryLine or DrawTarget.TrackBoundaryCurve;
    public bool IsBoundarySnap => Target is DrawTarget.TrackBoundaryLine or DrawTarget.TrackBoundaryCurve
                                   or DrawTarget.HeadlandLine or DrawTarget.HeadlandCurve;
    public bool IsLinear => Target is DrawTarget.TrackABLine or DrawTarget.TrackBoundaryLine
                            or DrawTarget.HeadlandLine or DrawTarget.TrackAPlus;
    public bool IsCurve => Points.Count > 2;
    public bool HasHeadingInput => Target == DrawTarget.TrackAPlus;
    public bool HasOffsetInput => IsHeadland;
    public bool HasExtendShrink => IsHeadland && IsPreview;
    public bool ShowFinishButton => Target is DrawTarget.TrackCurve && Phase == DrawPhase.PickingMore;
    public bool ShowCreateButton => IsPreview;

    /// <summary>Status text for the canvas banner.</summary>
    public string StatusText
    {
        get
        {
            if (Phase == DrawPhase.PickingA)
                return IsBoundarySnap ? "Click first point on boundary" : "Click point A";
            if (Phase == DrawPhase.PickingB)
                return IsBoundarySnap ? "Click second point on boundary" : "Click point B";
            if (Phase == DrawPhase.PickingMore)
                return $"Click next point ({Points.Count} placed)";
            if (Phase == DrawPhase.Preview)
            {
                if (HasOffsetInput)
                    return "Set offset, drag points, then Create";
                if (HasHeadingInput)
                    return $"Heading: {Heading:F1} - click Create";
                if (IsLinear && Points.Count >= 2)
                {
                    double h = Math.Atan2(Points[1].Easting - Points[0].Easting,
                                          Points[1].Northing - Points[0].Northing) * 180.0 / Math.PI;
                    if (h < 0) h += 360;
                    return $"Heading: {h:F1} - drag points or Create";
                }
                return "Drag points or Create";
            }
            return "";
        }
    }

    public void Reset()
    {
        Target = DrawTarget.None;
        Phase = DrawPhase.Idle;
        Points.Clear();
        BoundaryPoly = null;
        BoundaryStartIndex = BoundaryEndIndex = -1;
        Offset = 12.0;
        Heading = 0;
        StartExtension = 50;
        EndExtension = 50;
        BackupSegment = null;
        BackupTrack = null;
        DragPointIndex = -1;
        IsDragging = false;
        IsArrowDrag = false;
    }

    /// <summary>Find nearest boundary vertex to field coordinates.</summary>
    public int FindNearestBoundaryPoint(double fieldE, double fieldN)
    {
        if (BoundaryPoly?.Points == null) return -1;

        var pts = BoundaryPoly.Points;
        double minDist = double.MaxValue;
        int bestIdx = -1;

        for (int i = 0; i < pts.Count; i++)
        {
            double dx = pts[i].Easting - fieldE;
            double dy = pts[i].Northing - fieldN;
            double dist = dx * dx + dy * dy;
            if (dist < minDist) { minDist = dist; bestIdx = i; }
        }
        return bestIdx;
    }

    /// <summary>Extract boundary segment between two indices (shorter path).</summary>
    public List<Vec3> ExtractBoundarySegment(int idx1, int idx2)
    {
        if (BoundaryPoly?.Points == null) return new();
        var pts = BoundaryPoly.Points;
        int count = pts.Count;

        var forward = new List<Vec3>();
        int i = idx1;
        while (true)
        {
            var p = pts[i];
            forward.Add(new Vec3(p.Easting, p.Northing, p.Heading));
            if (i == idx2) break;
            i = (i + 1) % count;
            if (forward.Count > count + 1) break;
        }

        var reverse = new List<Vec3>();
        i = idx1;
        while (true)
        {
            var p = pts[i];
            reverse.Add(new Vec3(p.Easting, p.Northing, p.Heading));
            if (i == idx2) break;
            i = (i - 1 + count) % count;
            if (reverse.Count > count + 1) break;
        }

        return forward.Count <= reverse.Count ? forward : reverse;
    }
}
