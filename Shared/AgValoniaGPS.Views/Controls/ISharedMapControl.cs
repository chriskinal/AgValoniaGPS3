// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using Avalonia;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Coverage;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Shared interface for map rendering controls. After the GL pivot (Phase 4)
/// the sole implementor is <see cref="SkiaMapControl"/>; the interface stays
/// as the abstraction between platform MapService bridges and the control.
/// </summary>
public interface ISharedMapControl
{
    // Camera/View control
    void Toggle3DMode();
    void Set3DMode(bool is3D);
    bool Is3DMode { get; }
    void SetPitch(double deltaRadians);
    void PanTo(double x, double y);
    void SetPitchAbsolute(double pitchRadians);
    void Pan(double deltaX, double deltaY);
    void Zoom(double factor);
    double GetZoom();
    (double X, double Y) GetCameraCenter();
    void SetCamera(double x, double y, double zoom, double rotation);
    void Rotate(double deltaRadians);

    // Mouse interaction
    void StartPan(Point position);
    void StartRotate(Point position);
    void UpdateMouse(Point position);
    void EndPanRotate();

    // Content
    void SetBoundary(Boundary? boundary);
    void SetVehiclePosition(double x, double y, double heading);
    void SetVehicleSteerAngle(double radians);
    void SetToolPosition(double x, double y, double heading, double width, double hitchX, double hitchY, bool isReady = true);

    /// <summary>
    /// Atomic update of vehicle + tool positions in a single call.
    /// Prevents rendering mismatches between vehicle and tool.
    /// </summary>
    void SetAllPositions(double vehicleX, double vehicleY, double vehicleHeading,
        double toolX, double toolY, double toolHeading, double toolWidth,
        double hitchX, double hitchY, bool toolReady);
    void SetSectionStates(bool[] sectionOn, double[] sectionWidths, int numSections, int[]? buttonStates = null);
    void SetGridVisible(bool visible);
    void SetNorthUp(bool isNorthUp);
    void SetDayMode(bool isDayMode);
    void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points);
    void ClearRecordingPoints();
    void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY);
    void SetBackgroundImageWithMercator(string imagePath, double minX, double maxY, double maxX, double minY,
        double mercMinX, double mercMaxX, double mercMinY, double mercMaxY,
        double originLat, double originLon);
    void ClearBackground();

    // Boundary recording indicator
    void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0);

    // Headland visualization
    void SetHeadlandLine(IReadOnlyList<AgValoniaGPS.Models.Base.Vec3>? headlandPoints);
    void SetHeadlandPreview(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? previewPoints);
    void SetHeadlandVisible(bool visible);

    // YouTurn path visualization
    void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath);

    // Track visualization for U-turns
    void SetNextTrack(AgValoniaGPS.Models.Track.Track? track);
    void SetIsInYouTurn(bool isInTurn);
    void SetActiveTrack(AgValoniaGPS.Models.Track.Track? track);
    void SetBaseTrack(AgValoniaGPS.Models.Track.Track? track);
    void SetPendingPointA(AgValoniaGPS.Models.Position? pointA);
    bool EnableClickSelection { get; set; }
    (double Easting, double Northing) ScreenToWorld(double screenX, double screenY);

    // Tram line visualization
    void SetTramLines(
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? outerTrack,
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? innerTrack,
        IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? parallelLines,
        IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? boundaryExtraLines = null);
    void SetTramControlByte(byte controlByte);

    // Recorded path / contour strip visualization
    void SetRecordedPaths(IReadOnlyList<AgValoniaGPS.Models.Track.Track> paths);
    void SetContourStrips(IReadOnlyList<AgValoniaGPS.Models.Track.Track> strips);

    // Coverage visualization
    void SetCoveragePatches(IReadOnlyList<CoveragePatch> patches);

    // Coverage bitmap providers for bitmap-based rendering
    // allCellsProvider signature: (cellSize, viewMinE, viewMaxE, viewMinN, viewMaxN) -> cells within bounds
    void SetCoverageBitmapProviders(
        Func<(double MinE, double MaxE, double MinN, double MaxN)?>? boundsProvider,
        Func<double, double, double, double, double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? allCellsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? newCellsProvider);

    // Mark coverage as needing refresh (call when coverage data changes)
    void MarkCoverageDirty();

    // Mark coverage as needing full rebuild (call after loading from file)
    void MarkCoverageFullRebuildNeeded();

    // Initialize coverage bitmap with field bounds (call on field load)
    // If background image is set, composites it; otherwise initializes to black
    void InitializeCoverageBitmapWithBounds(double minE, double maxE, double minN, double maxN);

    // Direct pixel access for unified bitmap (service writes directly to bitmap)
    ushort GetCoveragePixel(int localX, int localY);
    void SetCoveragePixel(int localX, int localY, ushort rgb565);
    void ClearCoveragePixels();
    ushort[]? GetCoveragePixelBuffer();
    void SetCoveragePixelBuffer(ushort[] pixels);
    (int Width, int Height, double CellSize)? GetDisplayBitmapInfo();

    // Grid visibility property
    bool IsGridVisible { get; set; }

    // Flag markers on the map
    void SetFlags(IReadOnlyList<(double Easting, double Northing, string Color, string Name)> flags);

    // Camera follow mode (0=NorthUp, 1=HeadingUp, 2=Free, 3=Map)
    int CameraFollowMode { get; set; }

    // Fired when user manually pans/drags the map
    event Action? UserPanned;

    // Reverse indicator
    bool IsReversing { get; set; }

    // Guidance look-ahead points
    void SetGuidancePoints(double goalEasting, double goalNorthing, bool isActive);

    // Auto-pan: keeps vehicle visible by panning map when vehicle nears edge
    bool AutoPanEnabled { get; set; }

    // Map snapshot (thumbnail). Asynchronous: the request is queued to the
    // render thread, captured on the next frame, then delivered as PNG bytes
    // on the UI thread via SnapshotCaptured. maxDimension caps the larger of
    // the thumbnail's width/height in pixels (aspect preserved).
    void RequestSnapshot(int maxDimension);
    event EventHandler<byte[]>? SnapshotCaptured;
}
