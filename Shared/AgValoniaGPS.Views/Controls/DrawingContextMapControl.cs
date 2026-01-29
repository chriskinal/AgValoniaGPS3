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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.Track;
using SkiaSharp;

// For loading embedded resources
using AssetLoader = Avalonia.Platform.AssetLoader;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Shared interface for map rendering controls - enables cross-platform code sharing.
/// This interface is implemented by DrawingContextMapControl in the shared Views project.
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
    void SetToolPosition(double x, double y, double heading, double width, double hitchX, double hitchY);
    void SetSectionStates(bool[] sectionOn, double[] sectionWidths, int numSections, int[]? buttonStates = null);
    void SetGridVisible(bool visible);
    void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points);
    void ClearRecordingPoints();
    void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY);
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

    // Coverage visualization
    void SetCoveragePatches(IReadOnlyList<CoveragePatch> patches);

    // Coverage polygon provider for extruded rendering (dual-buffer approach)
    void SetCoveragePolygonProvider(Func<IEnumerable<(int SectionIndex, CoverageColor Color, IReadOnlyList<(double E, double N)> LeftEdge, IReadOnlyList<(double E, double N)> RightEdge)>>? provider);

    // Coverage bitmap providers for PERF-004 bitmap-based rendering
    void SetCoverageBitmapProviders(
        Func<(double MinE, double MaxE, double MinN, double MaxN)?>? boundsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? allCellsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? newCellsProvider);

    // Mark coverage as needing refresh (call when coverage data changes)
    void MarkCoverageDirty();

    // Grid visibility property
    bool IsGridVisible { get; set; }

    // Use polygon-based rendering for coverage (faster - one polygon per section)
    bool UsePolygonCoverageRendering { get; set; }

    // Auto-pan: keeps vehicle visible by panning map when vehicle nears edge
    bool AutoPanEnabled { get; set; }
}

/// <summary>
/// Cross-platform map control using Avalonia's DrawingContext.
/// Works on Desktop, iOS, and Android without platform-specific rendering code.
/// </summary>
public class DrawingContextMapControl : Control, ISharedMapControl
{
    // Avalonia styled property for grid visibility
    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(IsGridVisible), defaultValue: false);

    public bool IsGridVisible
    {
        get => GetValue(IsGridVisibleProperty);
        set => SetValue(IsGridVisibleProperty, value);
    }

    // Avalonia styled property for polygon-based coverage rendering (faster - one polygon per section)
    public static readonly StyledProperty<bool> UsePolygonCoverageRenderingProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(UsePolygonCoverageRendering), defaultValue: true);

    public bool UsePolygonCoverageRendering
    {
        get => GetValue(UsePolygonCoverageRenderingProperty);
        set => SetValue(UsePolygonCoverageRenderingProperty, value);
    }

    // Avalonia styled property for stroke-based coverage rendering (draws centerlines with thick strokes)
    // This is an alternative to polygon rendering that may be faster for large coverage areas
    public static readonly StyledProperty<bool> UseStrokeCoverageRenderingProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(UseStrokeCoverageRendering), defaultValue: false);

    public bool UseStrokeCoverageRendering
    {
        get => GetValue(UseStrokeCoverageRenderingProperty);
        set => SetValue(UseStrokeCoverageRenderingProperty, value);
    }

    // Avalonia styled property for bitmap-based coverage rendering (PERF-004)
    // Renders coverage to a WriteableBitmap for O(1) render time regardless of coverage amount
    // Uses Image control pattern with lock-based synchronization to avoid render pass conflicts
    public static readonly StyledProperty<bool> UseBitmapCoverageRenderingProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(UseBitmapCoverageRendering), defaultValue: true);

    public bool UseBitmapCoverageRendering
    {
        get => GetValue(UseBitmapCoverageRenderingProperty);
        set => SetValue(UseBitmapCoverageRenderingProperty, value);
    }

    // Avalonia styled property for vehicle visibility (can hide for headland editing)
    public static readonly StyledProperty<bool> ShowVehicleProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(ShowVehicle), defaultValue: true);

    public bool ShowVehicle
    {
        get => GetValue(ShowVehicleProperty);
        set => SetValue(ShowVehicleProperty, value);
    }

    // Avalonia styled property to enable click-to-select mode (for headland editing)
    public static readonly StyledProperty<bool> EnableClickSelectionProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(EnableClickSelection), defaultValue: false);

    public bool EnableClickSelection
    {
        get => GetValue(EnableClickSelectionProperty);
        set => SetValue(EnableClickSelectionProperty, value);
    }

    /// <summary>
    /// Event fired when the map is clicked in click-selection mode.
    /// EventArgs contain the world coordinates (Easting, Northing).
    /// </summary>
    public event EventHandler<MapClickEventArgs>? MapClicked;

    // Camera/viewport state
    private double _cameraX = 0.0;
    private double _cameraY = 0.0;
    private double _zoom = 1.0;
    private double _rotation = 0.0;
    private double _cameraPitch = 0.0;
    private double _cameraDistance = 100.0;
    private bool _is3DMode = false;

    // Auto-pan settings
    private bool _autoPanEnabled = true;
    private const double AutoPanSafeZone = 0.65; // Vehicle must stay within inner 65% of screen
    private const double AutoPanSmoothing = 0.15; // How fast to pan (0.1 = slow, 0.3 = fast)

    // Vehicle state
    private double _vehicleX = 0.0;
    private double _vehicleY = 0.0;
    private double _vehicleHeading = 0.0;

    // Tool state
    private double _toolX = 0.0;
    private double _toolY = 0.0;
    private double _toolHeading = 0.0;
    private double _toolWidth = 0.0;
    private double _hitchX = 0.0;
    private double _hitchY = 0.0;

    // Section state for individual section rendering
    private bool[] _sectionOn = new bool[16];
    private int[] _sectionButtonState = new int[16]; // 0=Off, 1=Auto, 2=On
    private double[] _sectionWidths = new double[16]; // Width of each section in meters
    private double[] _sectionLeft = new double[16];   // Left edge position relative to tool center
    private double[] _sectionRight = new double[16];  // Right edge position relative to tool center
    private int _numSections = 0;

    // Mouse interaction
    private bool _isPanning = false;
    private bool _isRotating = false;
    private Point _lastMousePosition;

    // Boundary data
    private Boundary? _boundary;
    private List<(double Easting, double Northing)>? _recordingPoints;
    private bool _showBoundaryOffsetIndicator = false;
    private double _boundaryOffsetMeters = 0.0;

    // Background image
    private string? _backgroundImagePath;
    private Bitmap? _backgroundImage;
    private double _bgMinX, _bgMaxY, _bgMaxX, _bgMinY; // Geo-reference bounds (local coordinates)

    // Headland data
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec3>? _headlandLine;
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? _headlandPreview;
    private bool _isHeadlandVisible = true;

    // Selection markers (for headland point selection)
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? _selectionMarkers;

    // Clip line (for headland clipping - line between two selected points)
    private (AgValoniaGPS.Models.Base.Vec2 Start, AgValoniaGPS.Models.Base.Vec2 End)? _clipLine;

    // Clip path (for curved headland clipping - follows the headland curve)
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? _clipPath;

    // YouTurn path
    private IReadOnlyList<(double Easting, double Northing)>? _youTurnPath;

    // Coverage patches for worked area display
    private IReadOnlyList<CoveragePatch> _coveragePatches = Array.Empty<CoveragePatch>();

    // Cached coverage geometry (rebuilt incrementally as patches grow)
    // IsFinalized = true means patch is complete and will never change
    // Includes bounding box for viewport culling
    private List<(Geometry Geometry, IBrush Brush, int VertexCount, bool IsFinalized, double MinX, double MinY, double MaxX, double MaxY)> _cachedCoverageGeometry = new();

    // Batched geometry by color for efficient drawing (ONLY finalized patches)
    // Active patches are drawn separately since their geometry changes every frame
    private Dictionary<uint, (GeometryGroup Geometry, IBrush Brush)> _batchedCoverageByColor = new();
    private HashSet<int> _batchedGeometryIndices = new(); // Track which patches are already in batches
    private HashSet<int> _activePatchIndices = new(); // Track active (non-finalized) patches for O(1) lookup

    // Coverage bitmap cache - renders all coverage to a single bitmap for O(1) drawing
    private RenderTargetBitmap? _coverageBitmap;
    private bool _coverageBitmapDirty = true;
    private double _coverageBoundsMinX, _coverageBoundsMinY, _coverageBoundsMaxX, _coverageBoundsMaxY;
    private const double COVERAGE_PIXELS_PER_METER = 0.5; // 0.5 pixels per meter = 2m resolution

    // Track what's already rendered to bitmap for incremental updates
    private int _lastRenderedPatchCount = 0;
    private List<int> _lastRenderedVertexCounts = new();

    // Track first non-finalized patch to skip finalized patches entirely in loop
    private int _firstNonFinalizedPatchIndex = 0;

    // Cached Skia draw operation (rebuilt when coverage changes)
    private CoverageDrawOperation? _cachedCoverageDrawOp;
    private int _lastDrawOpPatchCount = -1;

    // Polygon-based coverage rendering (dual-buffer approach - one extruded polygon per section)
    private Func<IEnumerable<(int SectionIndex, CoverageColor Color, IReadOnlyList<(double E, double N)> LeftEdge, IReadOnlyList<(double E, double N)> RightEdge)>>? _coveragePolygonProvider;

    // Cached polygon geometry (rebuilt when coverage changes)
    private Dictionary<int, (StreamGeometry Geometry, IBrush Brush)> _cachedPolygonGeometry = new();
    private int _lastPolygonPointCount = 0;
    private bool _polygonGeometryDirty = true;
    private readonly System.Diagnostics.Stopwatch _polygonRebuildThrottle = System.Diagnostics.Stopwatch.StartNew();
    private const double POLYGON_REBUILD_INTERVAL_MS = 1000; // Only rebuild geometry every 1 second

    // Cached stroke geometry for stroke-based coverage rendering (alternative to polygons)
    private Dictionary<int, (StreamGeometry Geometry, Pen Pen)> _cachedStrokeGeometry = new();
    private int _lastStrokePointCount = 0;

    // WriteableBitmap for PERF-004 bitmap-based coverage rendering
    // O(1) render time - blit pre-rendered bitmap each frame
    private WriteableBitmap? _coverageWriteableBitmap;
    private const double COVERAGE_BITMAP_CELL_SIZE = 0.5; // 0.5m per pixel (matches internal coverage resolution)
    private double _bitmapMinE, _bitmapMinN, _bitmapMaxE, _bitmapMaxN; // World coordinates of bitmap bounds
    private int _bitmapWidth, _bitmapHeight; // Pixel dimensions
    private bool _bitmapNeedsFullRebuild = true;
    private bool _bitmapNeedsIncrementalUpdate = false;
    private bool _bitmapUpdatePending = false; // Prevents re-entry during update

    // Provider for coverage bitmap data (from ICoverageMapService)
    private Func<(double MinE, double MaxE, double MinN, double MaxN)?>? _coverageBoundsProvider;
    private Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? _coverageAllCellsProvider;
    private Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? _coverageNewCellsProvider;

    // Track data
    private AgValoniaGPS.Models.Track.Track? _activeTrack;
    private AgValoniaGPS.Models.Track.Track? _nextTrack; // Next track to follow after U-turn
    private bool _isInYouTurn; // When true, current line is dotted, next line is solid
    private AgValoniaGPS.Models.Position? _pendingPointA; // Point A while waiting for Point B

    // Pens and brushes (reused for performance)
    private readonly Pen _gridPenMinor;
    private readonly Pen _gridPenMajor;
    private readonly Pen _gridPenAxisX;
    private readonly Pen _gridPenAxisY;
    private readonly Pen _boundaryPenOuter;
    private readonly Pen _boundaryPenInner;
    private readonly Pen _recordingPen;
    private readonly IBrush _vehicleBrush;
    private readonly Pen _vehiclePen;
    private readonly IBrush _recordingPointBrush;
    private readonly Pen _headlandPen;
    private readonly Pen _headlandPreviewPen;
    private readonly IBrush _selectionMarkerBrush;
    private readonly Pen _selectionMarkerPen;
    private readonly Pen _clipLinePen;
    private readonly Pen _abLinePen;
    private readonly Pen _abLineExtendPen;
    private readonly IBrush _pointABrush;
    private readonly IBrush _pointBBrush;
    private IImage? _vehicleImage;
    private readonly IBrush _toolBrush;
    private readonly Pen _toolPen;
    private readonly Pen _hitchPen;

    // Render timer
    private readonly DispatcherTimer _renderTimer;

    // FPS tracking
    private static DateTime _lastFpsUpdate = DateTime.UtcNow;
    private static int _frameCount;
    private static double _currentFps;

    // Performance profiling
    private static readonly System.Diagnostics.Stopwatch _profileSw = new();
    private static readonly System.Diagnostics.Stopwatch _renderSw = new();
    private static double _lastCoverageRenderMs;
    private static double _lastSetCoveragePatchesMs;
    private static double _lastFullRenderMs;
    private static int _profileCounter;
    private static int _renderCounter;

    /// <summary>
    /// Current frames per second (updated every second)
    /// </summary>
    public static double CurrentFps => _currentFps;

    /// <summary>
    /// Event raised when FPS is updated (every second)
    /// </summary>
    public static event Action<double>? FpsUpdated;

    public DrawingContextMapControl()
    {
        Debug.WriteLine("[DrawingContextMapControl] Constructor starting...");

        // Make control focusable for input
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        // Initialize pens and brushes (thinner lines to test shimmer)
        _gridPenMinor = new Pen(new SolidColorBrush(Color.FromArgb(77, 77, 77, 77)), 0.5);
        _gridPenMajor = new Pen(new SolidColorBrush(Color.FromArgb(128, 77, 77, 77)), 0.5);
        _gridPenAxisX = new Pen(new SolidColorBrush(Color.FromArgb(204, 204, 51, 51)), 1);
        _gridPenAxisY = new Pen(new SolidColorBrush(Color.FromArgb(204, 51, 204, 51)), 1);
        _boundaryPenOuter = new Pen(Brushes.Yellow, 1);
        _boundaryPenInner = new Pen(Brushes.Red, 1);
        _recordingPen = new Pen(Brushes.Cyan, 0.5); // Thinner line than dot markers
        _vehicleBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
        _vehiclePen = new Pen(Brushes.DarkGreen, 2);
        _recordingPointBrush = new SolidColorBrush(Color.FromRgb(255, 128, 0));
        _headlandPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 255, 128)), 0.1); // Green headland line (0.1m = 10cm width)
        _headlandPreviewPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 255, 165, 0)), 1.5); // Semi-transparent orange preview
        _selectionMarkerBrush = new SolidColorBrush(Color.FromRgb(255, 0, 255)); // Magenta selection markers
        _selectionMarkerPen = new Pen(Brushes.White, 2); // White outline
        _clipLinePen = new Pen(Brushes.Red, 3); // Red clip line
        _abLinePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 165, 0)), 3); // Orange AB line
        _abLineExtendPen = new Pen(new SolidColorBrush(Color.FromArgb(128, 255, 165, 0)), 1.5); // Semi-transparent extended line
        _pointABrush = new SolidColorBrush(Color.FromRgb(0, 255, 0)); // Green Point A
        _pointBBrush = new SolidColorBrush(Color.FromRgb(255, 0, 0)); // Red Point B
        _toolBrush = new SolidColorBrush(Color.FromArgb(180, 200, 80, 40)); // Semi-transparent brownish-red tool body
        _toolPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 150, 0)), 0.1); // Thin orange outline
        _hitchPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 0)), 0.15); // Yellow hitch line

        // Load vehicle (tractor) image from embedded resources
        LoadVehicleImage();

        // Render timer for continuous updates (30 FPS)
        // ARM64 Mac/iOS handles 60 FPS fine, but 30 FPS saves battery with no visible difference
        // Intel Mac simulator needs ~10 FPS due to ARM emulation overhead
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _renderTimer.Tick += OnRenderTimerTick;
        _renderTimer.Start();

        // Wire up mouse events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    private void OnRenderTimerTick(object? sender, EventArgs e)
    {
        // Just trigger render - don't count here (we count actual completed renders)
        InvalidateVisual();
    }

    /// <summary>
    /// Update FPS counter. Called at end of Render() to count actual completed frames.
    /// </summary>
    private void UpdateFpsCounter()
    {
        _frameCount++;

        // Check if it's time to update FPS (every second)
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;
        if (elapsed >= 1.0)
        {
            _currentFps = _frameCount / elapsed;
            _frameCount = 0;
            _lastFpsUpdate = now;
            // Fire event to update UI - must post to dispatcher to avoid
            // "Visual was invalidated during render pass" error when
            // the event handler updates bound properties
            var fps = _currentFps;
            Dispatcher.UIThread.Post(() => FpsUpdated?.Invoke(fps), DispatcherPriority.Background);
        }
    }

    public override void Render(DrawingContext context)
    {
        _renderSw.Restart();

        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Dark background
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(26, 26, 26)), null, new Rect(bounds.Size));

        // Calculate view transformation
        double aspect = bounds.Width / bounds.Height;
        double viewWidth = 200.0 * aspect / _zoom;
        double viewHeight = 200.0 / _zoom;

        // Save context state and apply camera transform
        using (context.PushTransform(GetCameraTransform(bounds, viewWidth, viewHeight)))
        {
            // Draw background image first (under everything else)
            if (_backgroundImage != null)
            {
                DrawBackgroundImage(context);
            }

            // Draw grid (if visible)
            if (IsGridVisible)
            {
                DrawGrid(context, viewWidth, viewHeight);
            }

            // Draw boundary
            if (_boundary != null)
            {
                DrawBoundary(context);
            }

            // Draw headland line (before recording points and vehicle)
            if (_isHeadlandVisible && _headlandLine != null && _headlandLine.Count > 2)
            {
                DrawHeadlandLine(context);
            }

            // Draw headland preview (semi-transparent)
            if (_headlandPreview != null && _headlandPreview.Count > 2)
            {
                DrawHeadlandPreview(context);
            }

            // Draw coverage (worked area) - drawn early so it's under everything else
            // Call if we have patches OR if polygon-based rendering is enabled with a provider
            if (_coveragePatches.Count > 0 || (UsePolygonCoverageRendering && _coveragePolygonProvider != null))
            {
                DrawCoverage(context);
            }

            // Draw recording points
            if (_recordingPoints != null && _recordingPoints.Count > 0)
            {
                DrawRecordingPoints(context);
            }

            // Draw selection markers (for headland point selection)
            if (_selectionMarkers != null && _selectionMarkers.Count > 0)
            {
                DrawSelectionMarkers(context);
            }

            // Draw clip line or clip path (red line between selected points)
            if (_clipLine.HasValue || (_clipPath != null && _clipPath.Count >= 2))
            {
                DrawClipLine(context);
            }

            // Draw Track (active track and pending Point A)
            if (_activeTrack != null || _pendingPointA != null)
            {
                DrawTrack(context);
            }

            // Draw YouTurn path
            if (_youTurnPath != null && _youTurnPath.Count > 1)
            {
                DrawYouTurnPath(context);
            }

            // Draw tool BEFORE vehicle (so vehicle appears on top)
            if (ShowVehicle && _toolWidth > 0.1)
            {
                DrawTool(context);
            }

            // Draw vehicle (can be hidden for headland editing mode)
            if (ShowVehicle)
            {
                DrawVehicle(context);
            }

            // Draw boundary offset indicator
            if (_showBoundaryOffsetIndicator)
            {
                DrawBoundaryOffsetIndicator(context);
            }
        }

        _renderSw.Stop();
        _lastFullRenderMs = _renderSw.Elapsed.TotalMilliseconds;

        // Log full render time every 30 frames
        if (++_renderCounter % 30 == 0)
        {
            Debug.WriteLine($"[Timing] Render: {_lastFullRenderMs:F2}ms, CovDraw: {_lastCoverageRenderMs:F2}ms, Polygons: {_cachedPolygonGeometry.Count}");
        }

        // Count actual completed renders for accurate FPS
        UpdateFpsCounter();
    }

    private Matrix GetCameraTransform(Rect bounds, double viewWidth, double viewHeight)
    {
        // Transform from world coordinates to screen coordinates
        // 1. Translate so camera center is at origin
        // 2. Scale from world units (meters) to pixels
        // 3. Rotate around center
        // 4. Translate to screen center

        double scaleX = bounds.Width / viewWidth;
        double scaleY = -bounds.Height / viewHeight; // Flip Y (screen Y is down, world Y is up)

        var matrix = Matrix.Identity;

        // Center on screen
        matrix = matrix * Matrix.CreateTranslation(bounds.Width / 2, bounds.Height / 2);

        // Apply rotation around center
        if (Math.Abs(_rotation) > 0.001)
        {
            matrix = Matrix.CreateRotation(-_rotation) * matrix;
        }

        // Scale from world to screen
        matrix = Matrix.CreateScale(scaleX, scaleY) * matrix;

        // Translate camera position
        matrix = Matrix.CreateTranslation(-_cameraX, -_cameraY) * matrix;

        return matrix;
    }

    private void DrawGrid(DrawingContext context, double viewWidth, double viewHeight)
    {
        double gridSize = 500.0; // 500m grid
        double spacing = 10.0;   // 10m spacing

        // Calculate visible range (with some padding)
        double minX = _cameraX - viewWidth;
        double maxX = _cameraX + viewWidth;
        double minY = _cameraY - viewHeight;
        double maxY = _cameraY + viewHeight;

        // Clamp to grid bounds
        minX = Math.Max(minX, -gridSize);
        maxX = Math.Min(maxX, gridSize);
        minY = Math.Max(minY, -gridSize);
        maxY = Math.Min(maxY, gridSize);

        // Snap to grid lines
        double startX = Math.Floor(minX / spacing) * spacing;
        double startY = Math.Floor(minY / spacing) * spacing;

        // Draw vertical lines
        for (double x = startX; x <= maxX; x += spacing)
        {
            if (x < -gridSize || x > gridSize) continue;

            bool isMajor = Math.Abs(x % 50.0) < 0.1;
            bool isAxis = Math.Abs(x) < 0.1;

            Pen pen = isAxis ? _gridPenAxisY : (isMajor ? _gridPenMajor : _gridPenMinor);
            context.DrawLine(pen, new Point(x, Math.Max(minY, -gridSize)), new Point(x, Math.Min(maxY, gridSize)));
        }

        // Draw horizontal lines
        for (double y = startY; y <= maxY; y += spacing)
        {
            if (y < -gridSize || y > gridSize) continue;

            bool isMajor = Math.Abs(y % 50.0) < 0.1;
            bool isAxis = Math.Abs(y) < 0.1;

            Pen pen = isAxis ? _gridPenAxisX : (isMajor ? _gridPenMajor : _gridPenMinor);
            context.DrawLine(pen, new Point(Math.Max(minX, -gridSize), y), new Point(Math.Min(maxX, gridSize), y));
        }
    }

    private void DrawBackgroundImage(DrawingContext context)
    {
        if (_backgroundImage == null) return;

        // Calculate the rectangle in world coordinates where the image should be drawn
        // The bounds are: minX (west), maxY (north), maxX (east), minY (south)
        double width = _bgMaxX - _bgMinX;
        double height = _bgMaxY - _bgMinY;

        // Calculate image center for proper Y-flip
        double centerX = (_bgMinX + _bgMaxX) / 2;
        double centerY = (_bgMinY + _bgMaxY) / 2;

        // Draw image with Y-flip around image center
        // The image needs to be flipped vertically because we're in a Y-up coordinate system
        // but image pixels have Y increasing downward
        using (context.PushTransform(Matrix.CreateTranslation(centerX, centerY)))
        using (context.PushTransform(Matrix.CreateScale(1, -1)))
        {
            var destRect = new Rect(-width / 2, -height / 2, width, height);
            context.DrawImage(_backgroundImage, destRect);
        }
    }

    private void DrawBoundary(DrawingContext context)
    {
        if (_boundary == null) return;

        // Draw outer boundary
        if (_boundary.OuterBoundary != null && _boundary.OuterBoundary.IsValid && _boundary.OuterBoundary.Points.Count > 1)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var points = _boundary.OuterBoundary.Points;
                ctx.BeginFigure(new Point(points[0].Easting, points[0].Northing), false);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(new Point(points[i].Easting, points[i].Northing));
                }
                ctx.LineTo(new Point(points[0].Easting, points[0].Northing)); // Close the loop
                ctx.EndFigure(true);
            }
            context.DrawGeometry(null, _boundaryPenOuter, geometry);
        }

        // Draw inner boundaries (holes)
        foreach (var inner in _boundary.InnerBoundaries)
        {
            if (inner.IsValid && inner.Points.Count > 1)
            {
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var points = inner.Points;
                    ctx.BeginFigure(new Point(points[0].Easting, points[0].Northing), false);
                    for (int i = 1; i < points.Count; i++)
                    {
                        ctx.LineTo(new Point(points[i].Easting, points[i].Northing));
                    }
                    ctx.LineTo(new Point(points[0].Easting, points[0].Northing));
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(null, _boundaryPenInner, geometry);
            }
        }

        // Draw headland polygon (working area boundary) - uses same style as inner boundaries
        if (_boundary.HeadlandPolygon != null && _boundary.HeadlandPolygon.IsValid && _boundary.HeadlandPolygon.Points.Count > 1)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                var points = _boundary.HeadlandPolygon.Points;
                ctx.BeginFigure(new Point(points[0].Easting, points[0].Northing), false);
                for (int i = 1; i < points.Count; i++)
                {
                    ctx.LineTo(new Point(points[i].Easting, points[i].Northing));
                }
                ctx.LineTo(new Point(points[0].Easting, points[0].Northing));
                ctx.EndFigure(true);
            }
            context.DrawGeometry(null, _boundaryPenInner, geometry);
        }
    }

    private void DrawCoverage(DrawingContext context)
    {
        // Debug: log every 90 frames to see if this is being called
        if (_renderCounter % 90 == 1)
            Console.WriteLine($"[Timing] DrawCov: UsePoly={UsePolygonCoverageRendering}, HasProvider={_coveragePolygonProvider != null}");

        _profileSw.Restart();

        // Compute visible world bounds for viewport culling
        // Use axis-aligned bounding box that contains the rotated view (conservative but fast)
        double aspect = Bounds.Width > 0 && Bounds.Height > 0 ? Bounds.Width / Bounds.Height : 1.0;
        double viewHalfWidth = 100.0 * aspect / _zoom;
        double viewHalfHeight = 100.0 / _zoom;

        // For rotated view, use the diagonal as the radius for the AABB
        double viewRadius = Math.Sqrt(viewHalfWidth * viewHalfWidth + viewHalfHeight * viewHalfHeight);
        double visMinX = _cameraX - viewRadius;
        double visMaxX = _cameraX + viewRadius;
        double visMinY = _cameraY - viewRadius;
        double visMaxY = _cameraY + viewRadius;

        int drawnCount;

        // Use bitmap-based rendering if enabled (PERF-004 - highest priority)
        if (UseBitmapCoverageRendering && _coverageBoundsProvider != null)
        {
            drawnCount = DrawCoverageBitmap(context);
        }
        // Use stroke-based rendering if enabled (experimental)
        else if (UseStrokeCoverageRendering && _coveragePolygonProvider != null)
        {
            drawnCount = DrawCoverageStrokes(context);
        }
        // Use polygon-based rendering if enabled and provider is available
        else if (UsePolygonCoverageRendering && _coveragePolygonProvider != null)
        {
            drawnCount = DrawCoveragePolygons(context);
        }
        else
        {
            // Log once why we're not using polygon rendering
            if (_renderCounter % 90 == 1)
                Console.WriteLine($"[Timing] CovPath: UsePoly={UsePolygonCoverageRendering}, Provider={_coveragePolygonProvider != null}");
            // Fall back to patch-based rendering
            drawnCount = DrawCoveragePatches(context, visMinX, visMaxX, visMinY, visMaxY);
        }

        _profileSw.Stop();
        _lastCoverageRenderMs = _profileSw.Elapsed.TotalMilliseconds;
        _lastDrawnPatchCount = drawnCount;
    }

    /// <summary>
    /// Update coverage bitmap if needed. Called outside of render pass via Dispatcher.
    /// </summary>
    private void UpdateCoverageBitmapIfNeeded()
    {
        if (_coverageBoundsProvider == null || _coverageAllCellsProvider == null)
            return;

        // Get coverage bounds
        var bounds = _coverageBoundsProvider();
        if (bounds == null)
        {
            // No coverage - clear the bitmap
            if (_coverageWriteableBitmap != null)
            {
                Console.WriteLine("[Timing] CovBitmap: Clearing bitmap (no coverage)");
                _coverageWriteableBitmap.Dispose();
                _coverageWriteableBitmap = null;
                _bitmapWidth = 0;
                _bitmapHeight = 0;
            }
            return;
        }

        var (minE, maxE, minN, maxN) = bounds.Value;
        double worldWidth = maxE - minE;
        double worldHeight = maxN - minN;

        if (worldWidth <= 0 || worldHeight <= 0)
            return;

        // Calculate required bitmap dimensions at 0.5m per pixel
        int requiredWidth = (int)Math.Ceiling(worldWidth / COVERAGE_BITMAP_CELL_SIZE);
        int requiredHeight = (int)Math.Ceiling(worldHeight / COVERAGE_BITMAP_CELL_SIZE);

        // Limit bitmap size for safety (max ~256MB at 4 bytes per pixel)
        // 8192 pixels @ 0.5m = 4096m (4km) max field dimension
        const int MAX_DIMENSION = 8192;
        if (requiredWidth > MAX_DIMENSION || requiredHeight > MAX_DIMENSION)
        {
            Console.WriteLine($"[Timing] CovBitmap: Too large {requiredWidth}x{requiredHeight}, skipping bitmap");
            return;
        }

        // Check if we need to rebuild the bitmap (bounds changed or first time)
        bool boundsChanged = _coverageWriteableBitmap == null ||
            Math.Abs(_bitmapMinE - minE) > 0.01 ||
            Math.Abs(_bitmapMinN - minN) > 0.01 ||
            _bitmapWidth != requiredWidth ||
            _bitmapHeight != requiredHeight;

        if (boundsChanged)
        {
            // Bounds changed - need full rebuild
            _bitmapNeedsFullRebuild = true;
            _bitmapMinE = minE;
            _bitmapMinN = minN;
            _bitmapMaxE = maxE;
            _bitmapMaxN = maxN;
            _bitmapWidth = requiredWidth;
            _bitmapHeight = requiredHeight;

            // Dispose old bitmap and create new one
            _coverageWriteableBitmap?.Dispose();
            _coverageWriteableBitmap = new WriteableBitmap(
                new PixelSize(requiredWidth, requiredHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            Console.WriteLine($"[Timing] CovBitmap: Created {requiredWidth}x{requiredHeight} bitmap");
        }

        // Update bitmap with coverage cells
        // Always do full rebuild for now - incremental updates have coordinate issues
        // when bounds expand (cells calculated relative to new bounds don't match old bitmap)
        if (_bitmapNeedsFullRebuild || _bitmapNeedsIncrementalUpdate)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int cellCount = UpdateCoverageBitmapFull();
            sw.Stop();
            Console.WriteLine($"[Timing] CovBitmap: Full rebuild {cellCount} cells in {sw.ElapsedMilliseconds}ms");
            _bitmapNeedsFullRebuild = false;
            _bitmapNeedsIncrementalUpdate = false;
        }
    }

    /// <summary>
    /// Draw coverage using WriteableBitmap (PERF-004).
    /// O(1) render time - just blit the pre-rendered bitmap.
    /// Bitmap is updated outside of render pass via MarkCoverageDirty.
    /// </summary>
    private int DrawCoverageBitmap(DrawingContext context)
    {
        // If bitmap not ready yet, fall back to polygons
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
        {
            // Schedule initial bitmap creation
            if (!_bitmapUpdatePending && _coverageBoundsProvider != null)
            {
                _bitmapUpdatePending = true;
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateCoverageBitmapIfNeeded();
                    _bitmapUpdatePending = false;
                }, DispatcherPriority.Background);
            }
            return DrawCoveragePolygons(context);
        }

        // Draw the bitmap
        double worldWidth = _bitmapMaxE - _bitmapMinE;
        double worldHeight = _bitmapMaxN - _bitmapMinN;

        var srcRect = new Rect(0, 0, _bitmapWidth, _bitmapHeight);
        var destRect = new Rect(_bitmapMinE, _bitmapMinN, worldWidth, worldHeight);

        context.DrawImage(_coverageWriteableBitmap, srcRect, destRect);

        return _bitmapWidth * _bitmapHeight;
    }

    /// <summary>
    /// Update coverage bitmap with all cells (full rebuild).
    /// </summary>
    private int UpdateCoverageBitmapFull()
    {
        if (_coverageWriteableBitmap == null || _coverageAllCellsProvider == null)
            return 0;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        int stride = framebuffer.RowBytes;
        int bufferSize = stride * _bitmapHeight;

        // Create a managed buffer to work with
        var buffer = new byte[bufferSize];
        // Buffer is already zeroed (transparent)

        int cellCount = 0;
        foreach (var (cellX, cellY, color) in _coverageAllCellsProvider(COVERAGE_BITMAP_CELL_SIZE))
        {
            // Convert cell coordinates to bitmap pixel
            // CellY is relative to minN (increasing north)
            // Don't flip Y - bitmap top-left is at (minE, minN) which matches cell (0,0)
            int px = cellX;
            int py = cellY;

            if (px >= 0 && px < _bitmapWidth && py >= 0 && py < _bitmapHeight)
            {
                int offset = py * stride + px * 4;
                buffer[offset + 0] = color.B; // BGRA format
                buffer[offset + 1] = color.G;
                buffer[offset + 2] = color.R;
                buffer[offset + 3] = 200; // Semi-transparent
                cellCount++;
            }
        }

        // Copy buffer to framebuffer
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, framebuffer.Address, bufferSize);
        return cellCount;
    }

    /// <summary>
    /// Update coverage bitmap with only new cells (incremental update).
    /// </summary>
    private int UpdateCoverageBitmapIncremental()
    {
        if (_coverageWriteableBitmap == null || _coverageNewCellsProvider == null)
            return 0;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        int stride = framebuffer.RowBytes;
        int bufferSize = stride * _bitmapHeight;

        // Read existing buffer
        var buffer = new byte[bufferSize];
        System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address, buffer, 0, bufferSize);

        int cellCount = 0;
        foreach (var (cellX, cellY, color) in _coverageNewCellsProvider(COVERAGE_BITMAP_CELL_SIZE))
        {
            int px = cellX;
            int py = cellY; // No Y flip needed

            if (px >= 0 && px < _bitmapWidth && py >= 0 && py < _bitmapHeight)
            {
                int offset = py * stride + px * 4;
                buffer[offset + 0] = color.B;
                buffer[offset + 1] = color.G;
                buffer[offset + 2] = color.R;
                buffer[offset + 3] = 200;
                cellCount++;
            }
        }

        // Copy buffer back to framebuffer
        System.Runtime.InteropServices.Marshal.Copy(buffer, 0, framebuffer.Address, bufferSize);
        return cellCount;
    }

    /// <summary>
    /// Draw coverage using extruded polygons (one per section).
    /// Much faster than grid cells or individual patches.
    /// </summary>
    private int DrawCoveragePolygons(DrawingContext context)
    {
        if (_coveragePolygonProvider == null)
            return 0;

        // Rebuild cached geometry if dirty
        // Throttle geometry rebuilds - only rebuild every 1 second to avoid stuttering
        // during continuous coverage recording. Force rebuild if cache is empty.
        int cacheCount = _cachedPolygonGeometry.Count;
        double elapsedMs = _polygonRebuildThrottle.Elapsed.TotalMilliseconds;
        bool shouldRebuild = _polygonGeometryDirty &&
            (cacheCount == 0 || elapsedMs >= POLYGON_REBUILD_INTERVAL_MS);

        // Log why we're rebuilding (or not)
        if (_renderCounter % 30 == 0)
        {
            Console.WriteLine($"[Timing] CovPoly: dirty={_polygonGeometryDirty}, cache={cacheCount}, elapsed={elapsedMs:F0}ms, rebuild={shouldRebuild}");
        }

        if (shouldRebuild)
        {
            // Reset dirty and restart timer BEFORE rebuild to prevent re-triggering
            // if MarkCoverageDirty is called during the rebuild
            _polygonGeometryDirty = false;
            _polygonRebuildThrottle.Restart();

            // Log the specific reason for rebuild
            string reason = cacheCount == 0 ? "cache empty" : $"elapsed >= {POLYGON_REBUILD_INTERVAL_MS}ms";

            // Time the rebuild
            var rebuildSw = System.Diagnostics.Stopwatch.StartNew();
            RebuildPolygonGeometryCache();
            rebuildSw.Stop();
            Console.WriteLine($"[Timing] CovRebuildStart: reason={reason}, took={rebuildSw.ElapsedMilliseconds}ms");
        }

        // Draw cached geometry (one draw call per section)
        foreach (var (_, (geometry, brush)) in _cachedPolygonGeometry)
        {
            context.DrawGeometry(brush, null, geometry);
        }

        return _cachedPolygonGeometry.Count;
    }

    /// <summary>
    /// Draw coverage using stroked lines (centerline with thick stroke width).
    /// This is an alternative to filled polygons that may be faster.
    /// </summary>
    private int DrawCoverageStrokes(DrawingContext context)
    {
        if (_coveragePolygonProvider == null)
            return 0;

        // Use same throttling as polygon rendering
        int cacheCount = _cachedStrokeGeometry.Count;
        double elapsedMs = _polygonRebuildThrottle.Elapsed.TotalMilliseconds;
        bool shouldRebuild = _polygonGeometryDirty &&
            (cacheCount == 0 || elapsedMs >= POLYGON_REBUILD_INTERVAL_MS);

        if (_renderCounter % 30 == 0)
        {
            Console.WriteLine($"[Timing] CovStroke: dirty={_polygonGeometryDirty}, cache={cacheCount}, elapsed={elapsedMs:F0}ms, rebuild={shouldRebuild}");
        }

        if (shouldRebuild)
        {
            _polygonGeometryDirty = false;
            _polygonRebuildThrottle.Restart();

            string reason = cacheCount == 0 ? "cache empty" : $"elapsed >= {POLYGON_REBUILD_INTERVAL_MS}ms";

            var rebuildSw = System.Diagnostics.Stopwatch.StartNew();
            RebuildStrokeGeometryCache();
            rebuildSw.Stop();
            Console.WriteLine($"[Timing] StrokeRebuildStart: reason={reason}, took={rebuildSw.ElapsedMilliseconds}ms");
        }

        // Draw cached stroke geometry
        foreach (var (_, (geometry, pen)) in _cachedStrokeGeometry)
        {
            context.DrawGeometry(null, pen, geometry);
        }

        return _cachedStrokeGeometry.Count;
    }

    /// <summary>
    /// Rebuild the cached stroke geometry from the coverage polygon provider.
    /// Each pass gets a polyline centerline with stroke width = section width.
    /// </summary>
    private void RebuildStrokeGeometryCache()
    {
        if (_coveragePolygonProvider == null)
        {
            _cachedStrokeGeometry.Clear();
            _lastStrokePointCount = 0;
            return;
        }

        var newCache = new Dictionary<int, (StreamGeometry Geometry, Pen Pen)>();
        int totalPoints = 0;
        int strokeCount = 0;

        // Decimation threshold - very aggressive for strokes since they're just visual indication
        const double decimationThresholdSq = 2500.0; // 50m minimum spacing squared

        foreach (var (sectionIndex, color, leftEdge, rightEdge) in _coveragePolygonProvider())
        {
            if (leftEdge.Count < 2) continue;

            // Calculate average section width from edges (for stroke width)
            double totalWidth = 0;
            int widthSamples = 0;
            for (int i = 0; i < Math.Min(leftEdge.Count, rightEdge.Count); i += Math.Max(1, leftEdge.Count / 10))
            {
                double dx = rightEdge[i].E - leftEdge[i].E;
                double dy = rightEdge[i].N - leftEdge[i].N;
                totalWidth += Math.Sqrt(dx * dx + dy * dy);
                widthSamples++;
            }
            double strokeWidth = widthSamples > 0 ? totalWidth / widthSamples : 1.0;

            // Create pen with section color and calculated width
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)), strokeWidth)
            {
                LineCap = PenLineCap.Flat,
                LineJoin = PenLineJoin.Round
            };

            // Build centerline geometry
            var streamGeometry = new StreamGeometry();
            using (var ctx = streamGeometry.Open())
            {
                // Calculate first centerline point
                var firstLeft = leftEdge[0];
                var firstRight = rightEdge[0];
                double centerE = (firstLeft.E + firstRight.E) / 2;
                double centerN = (firstLeft.N + firstRight.N) / 2;

                ctx.BeginFigure(new Point(centerE, centerN), false); // false = not filled
                var lastPt = (E: centerE, N: centerN);
                int addedPoints = 1;

                // Add centerline points with decimation
                int minCount = Math.Min(leftEdge.Count, rightEdge.Count);
                for (int i = 1; i < minCount; i++)
                {
                    centerE = (leftEdge[i].E + rightEdge[i].E) / 2;
                    centerN = (leftEdge[i].N + rightEdge[i].N) / 2;

                    double dx = centerE - lastPt.E;
                    double dy = centerN - lastPt.N;

                    // Always include last point, otherwise decimate
                    if (dx * dx + dy * dy >= decimationThresholdSq || i == minCount - 1)
                    {
                        ctx.LineTo(new Point(centerE, centerN));
                        lastPt = (centerE, centerN);
                        addedPoints++;
                    }
                }

                // Don't close the figure - it's a polyline, not a polygon
                ctx.EndFigure(false);
                totalPoints += addedPoints;
            }

            newCache[strokeCount] = (streamGeometry, pen);
            strokeCount++;
        }

        _cachedStrokeGeometry = newCache;

        if (strokeCount == 0)
            Console.WriteLine($"[Timing] StrokeRebuild: WARNING cache empty!");
        else
            Console.WriteLine($"[Timing] StrokeRebuild: {strokeCount} strokes, {totalPoints} points");

        _lastStrokePointCount = totalPoints;
    }

    /// <summary>
    /// Rebuild the cached polygon geometry from the coverage polygon provider.
    /// Each section gets one StreamGeometry built from left edge + reversed right edge.
    /// Note: Throttling is handled by the caller (DrawCoveragePolygons) using _polygonRebuildThrottle.
    /// </summary>
    private void RebuildPolygonGeometryCache()
    {
        if (_coveragePolygonProvider == null)
        {
            _cachedPolygonGeometry.Clear();
            _polygonGeometryDirty = false;
            _lastPolygonPointCount = 0;
            return;
        }

        // Build into a new dictionary to avoid clearing during iteration
        var newCache = new Dictionary<int, (StreamGeometry Geometry, IBrush Brush)>();
        int totalPoints = 0;
        int polygonCount = 0;

        foreach (var (sectionIndex, color, leftEdge, rightEdge) in _coveragePolygonProvider())
        {
            if (leftEdge.Count < 2) continue;

            // Create brush from color (R, G, B are already 0-255 bytes)
            // Use opaque (255) to avoid expensive alpha blending
            var brush = new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B));

            // Build polygon: left edge forward, then right edge backward
            // Decimate points - skip points closer than threshold to reduce vertex count
            // 10m threshold = 100 sq - aggressive decimation for performance
            const double decimationThresholdSq = 100.0; // 10m minimum spacing squared

            var streamGeometry = new StreamGeometry();
            using (var ctx = streamGeometry.Open())
            {
                // Start at first left edge point
                ctx.BeginFigure(new Point(leftEdge[0].E, leftEdge[0].N), true);
                var lastPt = leftEdge[0];
                int addedPoints = 1;

                // Continue along left edge with decimation
                for (int i = 1; i < leftEdge.Count; i++)
                {
                    var pt = leftEdge[i];
                    double dx = pt.E - lastPt.E;
                    double dy = pt.N - lastPt.N;
                    if (dx * dx + dy * dy >= decimationThresholdSq || i == leftEdge.Count - 1)
                    {
                        ctx.LineTo(new Point(pt.E, pt.N));
                        lastPt = pt;
                        addedPoints++;
                    }
                }

                // Continue along right edge in reverse with decimation
                lastPt = rightEdge[rightEdge.Count - 1];
                ctx.LineTo(new Point(lastPt.E, lastPt.N));
                addedPoints++;

                for (int i = rightEdge.Count - 2; i >= 0; i--)
                {
                    var pt = rightEdge[i];
                    double dx = pt.E - lastPt.E;
                    double dy = pt.N - lastPt.N;
                    if (dx * dx + dy * dy >= decimationThresholdSq || i == 0)
                    {
                        ctx.LineTo(new Point(pt.E, pt.N));
                        lastPt = pt;
                        addedPoints++;
                    }
                }

                ctx.EndFigure(true);
                totalPoints += addedPoints;
            }

            newCache[polygonCount] = (streamGeometry, brush);
            polygonCount++;
        }

        // Swap cache atomically so it's never empty during normal operation
        _cachedPolygonGeometry = newCache;

        // Warn if cache is empty - this will cause rebuilds every frame!
        if (polygonCount == 0)
            Console.WriteLine($"[Timing] CovRebuild: WARNING cache empty! Will rebuild every frame");
        else
            Console.WriteLine($"[Timing] CovRebuild: {polygonCount} polygons, {totalPoints} points");

        _lastPolygonPointCount = totalPoints;
        // Note: dirty flag is now reset BEFORE rebuild in DrawCoveragePolygons
    }

    /// <summary>
    /// Draw coverage using triangle strip patches (detailed, original method).
    /// </summary>
    private int DrawCoveragePatches(DrawingContext context, double visMinX, double visMaxX, double visMinY, double visMaxY)
    {
        // Update tracking for active vs finalized patches
        UpdateColorBatchesIncremental();

        // Draw only visible patches from the cache
        int drawnCount = 0;
        for (int i = 0; i < _cachedCoverageGeometry.Count; i++)
        {
            var cached = _cachedCoverageGeometry[i];

            // Viewport culling: skip patches entirely outside visible bounds
            if (cached.MaxX < visMinX || cached.MinX > visMaxX ||
                cached.MaxY < visMinY || cached.MinY > visMaxY)
                continue;

            context.DrawGeometry(cached.Brush, null, cached.Geometry);
            drawnCount++;
        }

        return drawnCount;
    }

    private int _lastDrawnPatchCount;

    private void UpdateColorBatchesIncremental()
    {
        // If coverage was cleared, reset
        if (_cachedCoverageGeometry.Count == 0)
        {
            _batchedCoverageByColor.Clear();
            _batchedGeometryIndices.Clear();
            _activePatchIndices.Clear();
            return;
        }

        // If our tracked indices exceed cache size, coverage was reset
        if (_batchedGeometryIndices.Count > 0 &&
            _batchedGeometryIndices.Max() >= _cachedCoverageGeometry.Count)
        {
            _batchedCoverageByColor.Clear();
            _batchedGeometryIndices.Clear();
            _activePatchIndices.Clear();
        }

        // Check active patches - some may have just finalized
        // Copy to list to allow modification during iteration
        var toRemove = new List<int>();
        foreach (int idx in _activePatchIndices)
        {
            if (idx >= _cachedCoverageGeometry.Count)
            {
                toRemove.Add(idx);
                continue;
            }

            var cached = _cachedCoverageGeometry[idx];
            if (cached.IsFinalized && !_batchedGeometryIndices.Contains(idx))
            {
                // This patch just finalized - add to batch
                AddToBatch(idx, cached.Geometry, cached.Brush);
                toRemove.Add(idx);
            }
        }

        foreach (int idx in toRemove)
            _activePatchIndices.Remove(idx);
    }

    private void AddToBatch(int idx, Geometry geometry, IBrush brush)
    {
        // Get color key from brush
        uint colorKey = 0;
        if (brush is SolidColorBrush scb)
        {
            colorKey = ((uint)scb.Color.A << 24) | ((uint)scb.Color.R << 16) |
                      ((uint)scb.Color.G << 8) | scb.Color.B;
        }

        // Get or create GeometryGroup for this color
        if (!_batchedCoverageByColor.TryGetValue(colorKey, out var batch))
        {
            batch = (new GeometryGroup(), brush);
            _batchedCoverageByColor[colorKey] = batch;
        }

        // Add geometry to the group and mark as batched
        batch.Geometry.Children.Add(geometry);
        _batchedGeometryIndices.Add(idx);
    }

    private void RebuildCoverageBitmap()
    {
        if (_coverageBitmap == null) return;
        if (_cachedCoverageGeometry.Count == 0) return;

        // Calculate bitmap dimensions
        double worldWidth = _coverageBoundsMaxX - _coverageBoundsMinX;
        double worldHeight = _coverageBoundsMaxY - _coverageBoundsMinY;

        if (worldWidth <= 0 || worldHeight <= 0) return;

        // Check if we need a full redraw (coverage was cleared)
        bool needsFullRedraw = _lastRenderedPatchCount > _cachedCoverageGeometry.Count;

        // Find patches that need rendering (new or grown)
        var patchesToRender = new List<int>();
        for (int i = 0; i < _cachedCoverageGeometry.Count; i++)
        {
            var cached = _cachedCoverageGeometry[i];
            var vertexCount = cached.VertexCount;

            // New patch?
            if (i >= _lastRenderedVertexCounts.Count)
            {
                patchesToRender.Add(i);
                continue;
            }

            // Patch has grown?
            if (vertexCount > _lastRenderedVertexCounts[i])
            {
                patchesToRender.Add(i);
            }
        }

        // Nothing to render?
        if (!needsFullRedraw && patchesToRender.Count == 0) return;

        // Create drawing context
        // Use false parameter to NOT clear the bitmap (incremental rendering)
        using (var dc = _coverageBitmap.CreateDrawingContext(needsFullRedraw))
        {
            // Transform from world coordinates to bitmap coordinates
            double scaleX = _coverageBitmap.PixelSize.Width / worldWidth;
            double scaleY = _coverageBitmap.PixelSize.Height / worldHeight;

            var transform = Matrix.CreateTranslation(-_coverageBoundsMinX, -_coverageBoundsMaxY) *
                           Matrix.CreateScale(scaleX, -scaleY);

            using (dc.PushTransform(transform))
            {
                if (needsFullRedraw)
                {
                    // Full redraw - render all patches
                    foreach (var cached in _cachedCoverageGeometry)
                    {
                        dc.DrawGeometry(cached.Brush, null, cached.Geometry);
                    }
                }
                else
                {
                    // Incremental - only render changed patches
                    foreach (int idx in patchesToRender)
                    {
                        var cached = _cachedCoverageGeometry[idx];
                        dc.DrawGeometry(cached.Brush, null, cached.Geometry);
                    }
                }
            }
        }

        // Update tracking state
        _lastRenderedPatchCount = _cachedCoverageGeometry.Count;
        _lastRenderedVertexCounts.Clear();
        foreach (var cached in _cachedCoverageGeometry)
        {
            _lastRenderedVertexCounts.Add(cached.VertexCount);
        }
    }

    /// <summary>
    /// Initialize or resize the coverage bitmap based on boundary bounds
    /// </summary>
    private void InitializeCoverageBitmap()
    {
        if (_boundary?.OuterBoundary == null || !_boundary.OuterBoundary.IsValid)
        {
            _coverageBitmap?.Dispose();
            _coverageBitmap = null;
            return;
        }

        // Calculate bounds from boundary points
        var points = _boundary.OuterBoundary.Points;
        if (points.Count < 3) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var pt in points)
        {
            if (pt.Easting < minX) minX = pt.Easting;
            if (pt.Easting > maxX) maxX = pt.Easting;
            if (pt.Northing < minY) minY = pt.Northing;
            if (pt.Northing > maxY) maxY = pt.Northing;
        }

        // Add padding (50m on each side)
        const double padding = 50.0;
        _coverageBoundsMinX = minX - padding;
        _coverageBoundsMinY = minY - padding;
        _coverageBoundsMaxX = maxX + padding;
        _coverageBoundsMaxY = maxY + padding;

        double worldWidth = _coverageBoundsMaxX - _coverageBoundsMinX;
        double worldHeight = _coverageBoundsMaxY - _coverageBoundsMinY;

        // Calculate bitmap size (limit to reasonable dimensions)
        int bitmapWidth = Math.Clamp((int)(worldWidth * COVERAGE_PIXELS_PER_METER), 64, 4096);
        int bitmapHeight = Math.Clamp((int)(worldHeight * COVERAGE_PIXELS_PER_METER), 64, 4096);

        // Create or recreate bitmap if size changed
        if (_coverageBitmap == null ||
            _coverageBitmap.PixelSize.Width != bitmapWidth ||
            _coverageBitmap.PixelSize.Height != bitmapHeight)
        {
            _coverageBitmap?.Dispose();
            _coverageBitmap = new RenderTargetBitmap(new PixelSize(bitmapWidth, bitmapHeight));
            _coverageBitmapDirty = true;

            // Reset incremental rendering state
            _lastRenderedPatchCount = 0;
            _lastRenderedVertexCounts.Clear();
            _firstNonFinalizedPatchIndex = 0;

            Debug.WriteLine($"[DrawingContextMapControl] Created coverage bitmap: {bitmapWidth}x{bitmapHeight} for {worldWidth:F0}x{worldHeight:F0}m field");
        }
    }

    private void DrawRecordingPoints(DrawingContext context)
    {
        if (_recordingPoints == null || _recordingPoints.Count == 0) return;

        // Draw line strip connecting all points
        if (_recordingPoints.Count > 1)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(_recordingPoints[0].Easting, _recordingPoints[0].Northing), false);
                for (int i = 1; i < _recordingPoints.Count; i++)
                {
                    ctx.LineTo(new Point(_recordingPoints[i].Easting, _recordingPoints[i].Northing));
                }
                ctx.EndFigure(false);
            }
            context.DrawGeometry(null, _recordingPen, geometry);
        }

        // Draw point markers (0.75m radius)
        foreach (var point in _recordingPoints)
        {
            context.DrawEllipse(_recordingPointBrush, null, new Point(point.Easting, point.Northing), 0.75, 0.75);
        }
    }

    private void LoadVehicleImage()
    {
        try
        {
            // Load tractor image from embedded Avalonia resources using AssetLoader
            var uri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/TractorAoG.png");
            using var stream = AssetLoader.Open(uri);
            _vehicleImage = new Bitmap(stream);
            Debug.WriteLine("[DrawingContextMapControl] Loaded tractor image successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DrawingContextMapControl] Failed to load tractor image: {ex.Message}");
            // Fallback to triangle drawing if image fails to load
        }
    }

    // Section color brushes (matching AgOpenGPS)
    // ButtonState: 0=Off, 1=Auto, 2=On (manual)
    private static readonly SolidColorBrush _sectionOffBrush = new SolidColorBrush(Color.FromRgb(242, 51, 51));     // Red - manually off
    private static readonly SolidColorBrush _sectionManualOnBrush = new SolidColorBrush(Color.FromRgb(247, 247, 0)); // Yellow - manually on
    private static readonly SolidColorBrush _sectionAutoOnBrush = new SolidColorBrush(Color.FromRgb(0, 242, 0));    // Green - auto and active
    private static readonly SolidColorBrush _sectionAutoOffBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)); // Gray - auto but inactive
    private static readonly Pen _sectionOutlinePen = new Pen(Brushes.Black, 0.1);

    private void DrawTool(DrawingContext context)
    {
        // Don't draw if tool has no width (not configured or zero width)
        if (_toolWidth < 0.1) return;

        double toolDepth = 2.0; // Tool depth in meters (front to back)

        // Draw hitch line from vehicle to tool center
        context.DrawLine(_hitchPen, new Point(_vehicleX, _vehicleY), new Point(_toolX, _toolY));

        // Draw individual sections centered at tool position, rotated to tool heading
        using (context.PushTransform(Matrix.CreateTranslation(_toolX, _toolY)))
        using (context.PushTransform(Matrix.CreateRotation(-_toolHeading))) // Negated for screen coordinates
        {
            if (_numSections > 0)
            {
                // Draw each section individually
                double sectionGap = 0.05; // Small gap between sections (5cm)

                for (int i = 0; i < _numSections; i++)
                {
                    // Get section bounds (with small inset for gap)
                    double left = _sectionLeft[i] + sectionGap / 2;
                    double right = _sectionRight[i] - sectionGap / 2;
                    double width = right - left;

                    if (width < 0.01) continue; // Skip if section too narrow

                    // Choose brush based on button state
                    // 3-state model: 0=Off (Red), 1=Auto (Green), 2=On (Yellow)
                    IBrush brush;
                    switch (_sectionButtonState[i])
                    {
                        case 0: // Off - manually forced off
                            brush = _sectionOffBrush; // Red
                            break;
                        case 2: // On - manually forced on
                            brush = _sectionManualOnBrush; // Yellow
                            break;
                        default: // Auto (1) - automatic mode
                            brush = _sectionAutoOnBrush; // Green
                            break;
                    }

                    // Draw section rectangle
                    var sectionRect = new Rect(left, -toolDepth / 2, width, toolDepth);
                    context.DrawRectangle(brush, _sectionOutlinePen, sectionRect);
                }
            }
            else
            {
                // Fallback: draw single tool rectangle if no sections configured
                double halfWidth = _toolWidth / 2.0;
                var toolRect = new Rect(-halfWidth, -toolDepth / 2, _toolWidth, toolDepth);
                context.DrawRectangle(_toolBrush, _toolPen, toolRect);
            }

            // Draw a center marker line to show tool heading direction
            var centerLine = new Pen(Brushes.White, 0.1);
            context.DrawLine(centerLine, new Point(0, -toolDepth / 2), new Point(0, toolDepth / 2));
        }
    }

    private void DrawVehicle(DrawingContext context)
    {
        // Size in meters (typical tractor ~5m)
        double size = 5.0;

        // Save transform and apply vehicle rotation
        using (context.PushTransform(Matrix.CreateTranslation(_vehicleX, _vehicleY)))
        using (context.PushTransform(Matrix.CreateRotation(-_vehicleHeading))) // Heading in radians, negated for screen coordinates
        {
            if (_vehicleImage != null)
            {
                // Draw tractor image centered at vehicle position
                // The image needs to be flipped vertically because we're in a y-up coordinate system
                using (context.PushTransform(Matrix.CreateScale(1, -1)))
                {
                    var destRect = new Rect(-size / 2, -size / 2, size, size);
                    context.DrawImage(_vehicleImage, destRect);
                }
            }
            else
            {
                // Fallback: draw a simple triangle
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(0, size / 2), true); // Front point
                    ctx.LineTo(new Point(-size / 3, -size / 2));   // Back left
                    ctx.LineTo(new Point(size / 3, -size / 2));    // Back right
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(_vehicleBrush, _vehiclePen, geometry);
            }
        }
    }

    private void DrawBoundaryOffsetIndicator(DrawingContext context)
    {
        // Reference point at vehicle
        double refX = _vehicleX;
        double refY = _vehicleY;

        // Draw reference marker (cyan square)
        double markerSize = 1.0;
        var cyanBrush = new SolidColorBrush(Color.FromRgb(0, 204, 204));
        context.DrawRectangle(cyanBrush, null,
            new Rect(refX - markerSize / 2, refY - markerSize / 2, markerSize, markerSize));

        // Draw offset arrow if offset is non-zero
        if (Math.Abs(_boundaryOffsetMeters) > 0.01)
        {
            double perpAngle = _vehicleHeading + Math.PI / 2.0;
            double offsetX = refX + _boundaryOffsetMeters * Math.Sin(perpAngle);
            double offsetY = refY + _boundaryOffsetMeters * Math.Cos(perpAngle);

            var yellowPen = new Pen(Brushes.Yellow, 0.5);
            context.DrawLine(yellowPen, new Point(refX, refY), new Point(offsetX, offsetY));

            // Arrowhead
            double arrowSize = 1.5;
            double dx = offsetX - refX;
            double dy = offsetY - refY;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001)
            {
                dx /= len;
                dy /= len;
                double px = -dy;
                double py = dx;

                var arrowGeometry = new StreamGeometry();
                using (var ctx = arrowGeometry.Open())
                {
                    ctx.BeginFigure(new Point(offsetX, offsetY), true);
                    ctx.LineTo(new Point(offsetX - dx * arrowSize + px * arrowSize * 0.5,
                                        offsetY - dy * arrowSize + py * arrowSize * 0.5));
                    ctx.LineTo(new Point(offsetX - dx * arrowSize - px * arrowSize * 0.5,
                                        offsetY - dy * arrowSize - py * arrowSize * 0.5));
                    ctx.EndFigure(true);
                }
                context.DrawGeometry(Brushes.Yellow, null, arrowGeometry);
            }
        }
    }

    private void DrawHeadlandLine(DrawingContext context)
    {
        if (_headlandLine == null || _headlandLine.Count < 3) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(_headlandLine[0].Easting, _headlandLine[0].Northing), false);
            for (int i = 1; i < _headlandLine.Count; i++)
            {
                ctx.LineTo(new Point(_headlandLine[i].Easting, _headlandLine[i].Northing));
            }
            // Close the polygon
            ctx.LineTo(new Point(_headlandLine[0].Easting, _headlandLine[0].Northing));
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, _headlandPen, geometry);
    }

    private void DrawHeadlandPreview(DrawingContext context)
    {
        if (_headlandPreview == null || _headlandPreview.Count < 3) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(_headlandPreview[0].Easting, _headlandPreview[0].Northing), false);
            for (int i = 1; i < _headlandPreview.Count; i++)
            {
                ctx.LineTo(new Point(_headlandPreview[i].Easting, _headlandPreview[i].Northing));
            }
            // Close the polygon
            ctx.LineTo(new Point(_headlandPreview[0].Easting, _headlandPreview[0].Northing));
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, _headlandPreviewPen, geometry);
    }

    private void DrawYouTurnPath(DrawingContext context)
    {
        if (_youTurnPath == null || _youTurnPath.Count < 2) return;

        // Create a pen for the YouTurn path - orange color for path line
        var youTurnPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 165, 0)), 1.0);

        // Draw the path as connected line segments
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(_youTurnPath[0].Easting, _youTurnPath[0].Northing), false);
            for (int i = 1; i < _youTurnPath.Count; i++)
            {
                ctx.LineTo(new Point(_youTurnPath[i].Easting, _youTurnPath[i].Northing));
            }
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, youTurnPen, geometry);

        // Draw path points as small squares (less distortion than circles when scaled)
        var pathPointBrush = new SolidColorBrush(Color.FromArgb(180, 255, 165, 0)); // Semi-transparent orange
        double squareSize = 0.8; // meters (in world coordinates)
        double halfSize = squareSize / 2.0;

        // Draw every Nth point to avoid clutter (every 2 meters roughly)
        int skipPoints = Math.Max(1, _youTurnPath.Count / 50);
        for (int i = 0; i < _youTurnPath.Count; i += skipPoints)
        {
            var pt = _youTurnPath[i];
            var rect = new Rect(pt.Easting - halfSize, pt.Northing - halfSize, squareSize, squareSize);
            context.DrawRectangle(pathPointBrush, null, rect);
        }

        // Draw start point marker (green square - larger)
        var startMarkerBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
        double markerSize = 2.0; // meters
        double halfMarker = markerSize / 2.0;
        var startRect = new Rect(
            _youTurnPath[0].Easting - halfMarker,
            _youTurnPath[0].Northing - halfMarker,
            markerSize, markerSize);
        context.DrawRectangle(startMarkerBrush, null, startRect);

        // Draw end point marker (red square - larger)
        var endMarkerBrush = new SolidColorBrush(Color.FromRgb(200, 0, 0));
        var endPt = _youTurnPath[_youTurnPath.Count - 1];
        var endRect = new Rect(
            endPt.Easting - halfMarker,
            endPt.Northing - halfMarker,
            markerSize, markerSize);
        context.DrawRectangle(endMarkerBrush, null, endRect);
    }

    private void DrawSelectionMarkers(DrawingContext context)
    {
        if (_selectionMarkers == null || _selectionMarkers.Count == 0) return;

        // Draw large circles at selection points
        double markerRadius = 4.0; // World units (meters)

        // Use different colors for first (orange) and second (blue) markers
        var orangeBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));
        var blueBrush = new SolidColorBrush(Color.FromRgb(0, 150, 255));

        for (int i = 0; i < _selectionMarkers.Count; i++)
        {
            var marker = _selectionMarkers[i];
            var brush = i == 0 ? orangeBrush : blueBrush;
            var center = new Point(marker.Easting, marker.Northing);
            context.DrawEllipse(brush, _selectionMarkerPen, center, markerRadius, markerRadius);
        }
    }

    private void DrawClipLine(DrawingContext context)
    {
        // Draw curved clip path if available (for curve mode)
        if (_clipPath != null && _clipPath.Count >= 2)
        {
            for (int i = 0; i < _clipPath.Count - 1; i++)
            {
                var p1 = new Point(_clipPath[i].Easting, _clipPath[i].Northing);
                var p2 = new Point(_clipPath[i + 1].Easting, _clipPath[i + 1].Northing);
                context.DrawLine(_clipLinePen, p1, p2);
            }
            return;
        }

        // Draw straight clip line (for line mode)
        if (!_clipLine.HasValue) return;

        var start = new Point(_clipLine.Value.Start.Easting, _clipLine.Value.Start.Northing);
        var end = new Point(_clipLine.Value.End.Easting, _clipLine.Value.End.Northing);
        context.DrawLine(_clipLinePen, start, end);
    }

    private void DrawTrack(DrawingContext context)
    {
        // Calculate scale factor: convert from desired screen size to world units
        // At zoom=1, viewHeight=200m maps to screen height
        // For ~0.75mm points at 96 DPI, that's about 3 pixels
        // worldRadius = screenPixels * (viewHeight / screenHeight)
        double viewHeight = 200.0 / _zoom;
        double screenHeight = Bounds.Height > 0 ? Bounds.Height : 600;
        double worldPerPixel = viewHeight / screenHeight;

        double pointRadius = 4 * worldPerPixel;  // ~4 pixels for point markers
        double lineThickness = 2 * worldPerPixel; // ~2 pixels for lines
        double labelOffset = 8 * worldPerPixel;   // Offset for A/B labels

        // Create scaled pens - solid and dotted versions
        var trackPenSolid = new Pen(new SolidColorBrush(Color.FromRgb(255, 165, 0)), lineThickness);
        var trackPenDotted = new Pen(new SolidColorBrush(Color.FromRgb(255, 165, 0)), lineThickness)
        {
            DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
        };
        var trackExtendPenScaled = new Pen(new SolidColorBrush(Color.FromArgb(128, 255, 165, 0)), lineThickness * 0.5);
        var trackExtendPenDotted = new Pen(new SolidColorBrush(Color.FromArgb(128, 255, 165, 0)), lineThickness * 0.5)
        {
            DashStyle = new DashStyle(new double[] { 4, 4 }, 0)
        };
        var pointOutlinePen = new Pen(Brushes.White, lineThickness * 0.5);

        // Next line pen (cyan/blue for visibility)
        var nextLinePenSolid = new Pen(new SolidColorBrush(Color.FromRgb(0, 200, 255)), lineThickness);
        var nextLineExtendPen = new Pen(new SolidColorBrush(Color.FromArgb(128, 0, 200, 255)), lineThickness * 0.5);

        // Draw pending Point A (green marker while waiting for Point B)
        if (_pendingPointA != null)
        {
            var pointA = new Point(_pendingPointA.Easting, _pendingPointA.Northing);
            context.DrawEllipse(_pointABrush, pointOutlinePen, pointA, pointRadius, pointRadius);

            // Draw "A" label offset to the right
            DrawLabel(context, "A", pointA.X + labelOffset, pointA.Y, worldPerPixel, Brushes.LimeGreen);
        }

        // Draw next track first (so current track renders on top)
        if (_isInYouTurn && _nextTrack != null)
        {
            DrawSingleTrack(context, _nextTrack, nextLinePenSolid, nextLineExtendPen, pointOutlinePen,
                pointRadius, labelOffset, worldPerPixel, "Next");
        }

        // Draw active track
        if (_activeTrack != null)
        {
            // When in U-turn, draw current line as dotted; otherwise solid
            var mainPen = _isInYouTurn ? trackPenDotted : trackPenSolid;
            var extendPen = _isInYouTurn ? trackExtendPenDotted : trackExtendPenScaled;

            DrawSingleTrack(context, _activeTrack, mainPen, extendPen, pointOutlinePen,
                pointRadius, labelOffset, worldPerPixel, "Current");
        }
    }

    private void DrawSingleTrack(DrawingContext context, AgValoniaGPS.Models.Track.Track track,
        Pen mainPen, Pen extendPen, Pen pointOutlinePen,
        double pointRadius, double labelOffset, double worldPerPixel, string lineType)
    {
        if (track.Points.Count < 2)
            return;

        var trackPointA = track.Points[0];
        var trackPointB = track.Points[track.Points.Count - 1];

        var pointA = new Point(trackPointA.Easting, trackPointA.Northing);
        var pointB = new Point(trackPointB.Easting, trackPointB.Northing);

        // For AB lines (2 points), draw extended line in both directions
        if (track.Points.Count == 2)
        {
            Console.WriteLine($"[DrawTrack] AB Line: '{track.Name}' with 2 points");
            double dx = pointB.X - pointA.X;
            double dy = pointB.Y - pointA.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length > 0.01)
            {
                double nx = dx / length;
                double ny = dy / length;
                double extendDistance = 500.0;
                var extendA = new Point(pointA.X - nx * extendDistance, pointA.Y - ny * extendDistance);
                var extendB = new Point(pointB.X + nx * extendDistance, pointB.Y + ny * extendDistance);
                context.DrawLine(extendPen, extendA, extendB);
            }

            // Draw main AB line
            context.DrawLine(mainPen, pointA, pointB);
        }
        else
        {
            // For curves (>2 points), draw all segments
            Console.WriteLine($"[DrawTrack] Curve: '{track.Name}' drawing {track.Points.Count - 1} segments");
            for (int i = 0; i < track.Points.Count - 1; i++)
            {
                var p1 = new Point(track.Points[i].Easting, track.Points[i].Northing);
                var p2 = new Point(track.Points[i + 1].Easting, track.Points[i + 1].Northing);
                context.DrawLine(mainPen, p1, p2);
            }
        }

        // Draw Point A marker (green)
        context.DrawEllipse(_pointABrush, pointOutlinePen, pointA, pointRadius, pointRadius);

        // Draw Point B marker (red)
        context.DrawEllipse(_pointBBrush, pointOutlinePen, pointB, pointRadius, pointRadius);

        // Draw labels - only for current line to avoid clutter
        if (lineType == "Current")
        {
            DrawLabel(context, "A", pointA.X + labelOffset, pointA.Y, worldPerPixel, Brushes.LimeGreen);
            DrawLabel(context, "B", pointB.X + labelOffset, pointB.Y, worldPerPixel, Brushes.Red);
        }
    }

    private void DrawLabel(DrawingContext context, string text, double x, double y, double worldPerPixel, IBrush brush)
    {
        // Scale font size based on zoom (target ~12 pixels on screen)
        double fontSize = 12 * worldPerPixel;

        var typeface = new Typeface("Arial", FontStyle.Normal, FontWeight.Bold);
        var formattedText = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            brush);

        // Note: Y is flipped in world coordinates, so we need to handle that
        // The camera transform already handles the flip, so just draw normally
        // But text will appear upside down - we need to flip it back
        using (context.PushTransform(Matrix.CreateScale(1, -1) * Matrix.CreateTranslation(x, y)))
        {
            context.DrawText(formattedText, new Point(0, -fontSize));
        }
    }

    // Mouse event handlers
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
            // In click selection mode, fire the MapClicked event instead of panning
            if (EnableClickSelection)
            {
                var worldPos = ScreenToWorld(point.Position.X, point.Position.Y);
                MapClicked?.Invoke(this, new MapClickEventArgs(worldPos.Easting, worldPos.Northing));
                e.Handled = true;
                return;
            }

            _isPanning = true;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            _isRotating = true;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var currentPos = point.Position;

        if (_isPanning)
        {
            double deltaX = currentPos.X - _lastMousePosition.X;
            double deltaY = currentPos.Y - _lastMousePosition.Y;

            // Convert screen delta to world delta
            double aspect = Bounds.Width / Bounds.Height;
            double viewWidth = 200.0 * aspect / _zoom;
            double viewHeight = 200.0 / _zoom;

            double worldDeltaX = -deltaX * viewWidth / Bounds.Width;
            double worldDeltaY = deltaY * viewHeight / Bounds.Height; // Flip Y

            // Apply rotation to the delta
            double cos = Math.Cos(_rotation);
            double sin = Math.Sin(_rotation);
            double rotatedDeltaX = worldDeltaX * cos - worldDeltaY * sin;
            double rotatedDeltaY = worldDeltaX * sin + worldDeltaY * cos;

            _cameraX += rotatedDeltaX;
            _cameraY += rotatedDeltaY;

            _lastMousePosition = currentPos;
            e.Handled = true;
        }
        else if (_isRotating)
        {
            double deltaX = currentPos.X - _lastMousePosition.X;
            _rotation += deltaX * 0.01;
            _lastMousePosition = currentPos;
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning || _isRotating)
        {
            _isPanning = false;
            _isRotating = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
        _zoom *= zoomFactor;
        _zoom = Math.Clamp(_zoom, 0.02, 100.0);  // Min zoom 0.02 = 10km view height for large fields
        e.Handled = true;
    }

    // Public API methods (matching IMapControl interface)
    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _cameraX = x;
        _cameraY = y;
        _zoom = zoom;
        _rotation = rotation;
    }

    public void Pan(double deltaX, double deltaY)
    {
        _cameraX += deltaX;
        _cameraY += deltaY;
    }

    public void PanTo(double x, double y)
    {
        _cameraX = x;
        _cameraY = y;
    }

    public void Zoom(double factor)
    {
        if (_is3DMode)
        {
            _cameraDistance *= (1.0 / factor);
            _cameraDistance = Math.Clamp(_cameraDistance, 10.0, 500.0);
        }
        else
        {
            _zoom *= factor;
            _zoom = Math.Clamp(_zoom, 0.02, 100.0);  // Min zoom 0.02 = 10km view height for large fields
        }
    }

    public double GetZoom() => _zoom;

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
    }

    public void SetGridVisible(bool visible)
    {
        IsGridVisible = visible;
    }

    public void Toggle3DMode()
    {
        _is3DMode = !_is3DMode;
        if (_is3DMode)
        {
            _cameraPitch = Math.PI / 6.0;
            _cameraDistance = 150.0;
        }
        else
        {
            _cameraPitch = 0.0;
        }
    }

    public void Set3DMode(bool is3D)
    {
        if (_is3DMode != is3D)
        {
            Toggle3DMode();
        }
    }

    public bool Is3DMode => _is3DMode;

    public void SetPitch(double deltaRadians)
    {
        _cameraPitch += deltaRadians;
        _cameraPitch = Math.Clamp(_cameraPitch, 0.0, Math.PI / 2.5);
    }

    public void SetPitchAbsolute(double pitchRadians)
    {
        _cameraPitch = Math.Clamp(pitchRadians, 0.0, Math.PI / 2.5);
    }

    public void SetVehiclePosition(double x, double y, double heading)
    {
        _vehicleX = x;
        _vehicleY = y;
        _vehicleHeading = heading;

        // Auto-pan to keep vehicle visible
        if (_autoPanEnabled && Bounds.Width > 0 && Bounds.Height > 0)
        {
            ApplyAutoPan();
        }
    }

    public void SetToolPosition(double x, double y, double heading, double width, double hitchX, double hitchY)
    {
        _toolX = x;
        _toolY = y;
        _toolHeading = heading;
        _toolWidth = width;
        _hitchX = hitchX;
        _hitchY = hitchY;
    }

    public void SetSectionStates(bool[] sectionOn, double[] sectionWidths, int numSections, int[]? buttonStates = null)
    {
        _numSections = Math.Min(numSections, 16);

        // Copy state, button states, and widths
        for (int i = 0; i < _numSections; i++)
        {
            _sectionOn[i] = i < sectionOn.Length && sectionOn[i];
            _sectionButtonState[i] = buttonStates != null && i < buttonStates.Length ? buttonStates[i] : 1; // Default to Auto
            _sectionWidths[i] = i < sectionWidths.Length ? sectionWidths[i] : 1.0;
        }

        // Calculate total width and section positions
        // Sections are distributed left-to-right, centered on tool position
        double totalWidth = 0;
        for (int i = 0; i < _numSections; i++)
        {
            totalWidth += _sectionWidths[i];
        }

        // Calculate left/right positions for each section
        // Left edge of first section is at -totalWidth/2
        double runningPosition = -totalWidth / 2.0;
        for (int i = 0; i < _numSections; i++)
        {
            _sectionLeft[i] = runningPosition;
            _sectionRight[i] = runningPosition + _sectionWidths[i];
            runningPosition += _sectionWidths[i];
        }
    }

    /// <summary>
    /// Auto-pan the camera to keep the vehicle within the safe zone.
    /// Uses smooth interpolation to avoid jarring camera movements.
    /// </summary>
    private void ApplyAutoPan()
    {
        // Calculate current view dimensions
        double aspect = Bounds.Width / Bounds.Height;
        double viewWidth = 200.0 * aspect / _zoom;
        double viewHeight = 200.0 / _zoom;

        // Calculate safe zone boundaries (in world coordinates relative to camera)
        double safeHalfWidth = (viewWidth / 2) * AutoPanSafeZone;
        double safeHalfHeight = (viewHeight / 2) * AutoPanSafeZone;

        // Calculate vehicle position relative to camera (accounting for rotation)
        double relX = _vehicleX - _cameraX;
        double relY = _vehicleY - _cameraY;

        // Apply rotation to get screen-aligned relative position
        double cos = Math.Cos(-_rotation);
        double sin = Math.Sin(-_rotation);
        double screenRelX = relX * cos - relY * sin;
        double screenRelY = relX * sin + relY * cos;

        // Check if vehicle is outside safe zone and calculate needed pan
        double panX = 0;
        double panY = 0;

        if (screenRelX > safeHalfWidth)
            panX = screenRelX - safeHalfWidth;
        else if (screenRelX < -safeHalfWidth)
            panX = screenRelX + safeHalfWidth;

        if (screenRelY > safeHalfHeight)
            panY = screenRelY - safeHalfHeight;
        else if (screenRelY < -safeHalfHeight)
            panY = screenRelY + safeHalfHeight;

        // If pan is needed, apply it with smoothing
        if (Math.Abs(panX) > 0.01 || Math.Abs(panY) > 0.01)
        {
            // Convert pan back from screen-aligned to world coordinates
            double worldPanX = panX * Math.Cos(_rotation) - panY * Math.Sin(_rotation);
            double worldPanY = panX * Math.Sin(_rotation) + panY * Math.Cos(_rotation);

            // Apply smooth interpolation
            _cameraX += worldPanX * AutoPanSmoothing;
            _cameraY += worldPanY * AutoPanSmoothing;
        }
    }

    /// <summary>
    /// Enable or disable auto-pan feature
    /// </summary>
    public bool AutoPanEnabled
    {
        get => _autoPanEnabled;
        set => _autoPanEnabled = value;
    }

    public void SetBoundary(Boundary? boundary)
    {
        _boundary = boundary;
        InitializeCoverageBitmap();
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _recordingPoints = new List<(double, double)>(points);
    }

    public void ClearRecordingPoints()
    {
        _recordingPoints = null;
    }

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY)
    {
        _backgroundImagePath = imagePath;
        _bgMinX = minX;
        _bgMaxY = maxY;
        _bgMaxX = maxX;
        _bgMinY = minY;

        // Load the bitmap
        _backgroundImage?.Dispose();
        _backgroundImage = null;

        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                _backgroundImage = new Bitmap(imagePath);
                Debug.WriteLine($"[DrawingContextMapControl] Loaded background image: {imagePath} ({_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height})");
                Debug.WriteLine($"  Bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DrawingContextMapControl] Failed to load background image: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"[DrawingContextMapControl] Background image path invalid or not found: {imagePath}");
        }
    }

    public void ClearBackground()
    {
        _backgroundImagePath = null;
        _backgroundImage?.Dispose();
        _backgroundImage = null;
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _showBoundaryOffsetIndicator = show;
        _boundaryOffsetMeters = offsetMeters;
    }

    // Headland visualization
    public void SetHeadlandLine(IReadOnlyList<AgValoniaGPS.Models.Base.Vec3>? headlandPoints)
    {
        _headlandLine = headlandPoints;
    }

    public void SetHeadlandPreview(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? previewPoints)
    {
        _headlandPreview = previewPoints;
    }

    public void SetHeadlandVisible(bool visible)
    {
        _isHeadlandVisible = visible;
    }

    // YouTurn path visualization
    public void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath)
    {
        _youTurnPath = turnPath;
    }

    public void SetSelectionMarkers(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? markers)
    {
        _selectionMarkers = markers;
    }

    public void SetClipLine(AgValoniaGPS.Models.Base.Vec2? start, AgValoniaGPS.Models.Base.Vec2? end)
    {
        if (start.HasValue && end.HasValue)
        {
            _clipLine = (start.Value, end.Value);
        }
        else
        {
            _clipLine = null;
        }
    }

    public void SetClipPath(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? path)
    {
        _clipPath = path;
    }

    // Track visualization
    public void SetActiveTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _activeTrack = track;
    }

    public void SetNextTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _nextTrack = track;
    }

    public void SetIsInYouTurn(bool isInTurn)
    {
        _isInYouTurn = isInTurn;
    }

    public void SetPendingPointA(AgValoniaGPS.Models.Position? pointA)
    {
        _pendingPointA = pointA;
    }

    // Coverage visualization
    public void SetCoveragePatches(IReadOnlyList<CoveragePatch> patches)
    {
        _profileSw.Restart();

        _coveragePatches = patches;

        // Mark polygon geometry as dirty when coverage changes
        _polygonGeometryDirty = true;

        // Rebuild Skia draw operation if patch count changed (new patches added)
        // We rebuild the whole thing because Skia paths are immutable
        if (patches.Count != _lastDrawOpPatchCount)
        {
            _cachedCoverageDrawOp?.Dispose();
            _cachedCoverageDrawOp = new CoverageDrawOperation(
                new Rect(-100000, -100000, 200000, 200000), // Large bounds to cover any field
                patches);
            _lastDrawOpPatchCount = patches.Count;
        }

        // Still maintain geometry cache for fallback
        RebuildCoverageGeometryCache();

        _profileSw.Stop();
        _lastSetCoveragePatchesMs = _profileSw.Elapsed.TotalMilliseconds;

        // Log every 30 calls (~1 second at 30 FPS)
        if (++_profileCounter % 30 == 0)
        {
            int batchedCount = 0;
            foreach (var (_, (geom, _)) in _batchedCoverageByColor)
                batchedCount += geom.Children.Count;

            Debug.WriteLine($"[Timing] SetPatches: {_lastSetCoveragePatchesMs:F2}ms, CovDraw: {_lastCoverageRenderMs:F2}ms, Drawn: {_lastDrawnPatchCount}/{patches.Count}, Batched: {batchedCount}, Active: {_activePatchIndices.Count}");
        }
    }

    public void SetCoveragePolygonProvider(Func<IEnumerable<(int SectionIndex, CoverageColor Color, IReadOnlyList<(double E, double N)> LeftEdge, IReadOnlyList<(double E, double N)> RightEdge)>>? provider)
    {
        _coveragePolygonProvider = provider;
        _polygonGeometryDirty = true;
    }

    public void SetCoverageBitmapProviders(
        Func<(double MinE, double MaxE, double MinN, double MaxN)?>? boundsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? allCellsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? newCellsProvider)
    {
        _coverageBoundsProvider = boundsProvider;
        _coverageAllCellsProvider = allCellsProvider;
        _coverageNewCellsProvider = newCellsProvider;
        _bitmapNeedsFullRebuild = true;
    }

    public void MarkCoverageDirty()
    {
        _polygonGeometryDirty = true;
        _bitmapNeedsIncrementalUpdate = true;

        // Schedule bitmap update
        if (UseBitmapCoverageRendering && !_bitmapUpdatePending)
        {
            _bitmapUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                UpdateCoverageBitmapIfNeeded();
                _bitmapUpdatePending = false;
            }, DispatcherPriority.Background);
        }
    }

    private void RebuildCoverageGeometryCache()
    {
        // Incremental update: only rebuild geometry for patches that changed
        // OPTIMIZATION: Start from first non-finalized patch to skip O(n) iteration

        int patchCount = _coveragePatches.Count;

        // If we have more cached entries than patches, clear and rebuild
        // (this happens when coverage is cleared)
        if (_cachedCoverageGeometry.Count > patchCount)
        {
            _cachedCoverageGeometry.Clear();
            _batchedCoverageByColor.Clear();
            _batchedGeometryIndices.Clear();
            _activePatchIndices.Clear();
            _coverageBitmapDirty = true;
            _firstNonFinalizedPatchIndex = 0;
        }

        // Start from first non-finalized patch (skip all finalized ones at start)
        int startIndex = Math.Min(_firstNonFinalizedPatchIndex, patchCount);

        for (int p = startIndex; p < patchCount; p++)
        {
            var patch = _coveragePatches[p];
            if (!patch.IsRenderable) continue;

            var vertices = patch.Vertices;
            if (vertices.Count < 4) continue;

            // Check if we already have cached geometry for this patch
            if (p < _cachedCoverageGeometry.Count)
            {
                var cached = _cachedCoverageGeometry[p];

                // Check if patch just became finalized (was active, now inactive)
                bool isNowFinalized = !patch.IsActive;
                if (!cached.IsFinalized && isNowFinalized)
                {
                    // Update cache to mark as finalized (geometry doesn't change, just the flag)
                    _cachedCoverageGeometry[p] = (cached.Geometry, cached.Brush, cached.VertexCount, true,
                        cached.MinX, cached.MinY, cached.MaxX, cached.MaxY);
                }

                // If patch is finalized in cache, update start index and skip
                if (cached.IsFinalized || isNowFinalized)
                {
                    // Move start index past consecutive finalized patches
                    if (p == _firstNonFinalizedPatchIndex)
                    {
                        _firstNonFinalizedPatchIndex = p + 1;
                    }
                    continue;
                }

                // If vertex count unchanged, skip rebuild but still track as active
                if (cached.VertexCount == vertices.Count)
                {
                    // Still need to track active patches for drawing
                    if (!cached.IsFinalized)
                    {
                        _activePatchIndices.Add(p);
                    }
                    continue;
                }
            }

            // Create brush from patch color with 60% alpha (matching AgOpenGPS)
            var color = Color.FromArgb(152, patch.Color.R, patch.Color.G, patch.Color.B);
            var brush = new SolidColorBrush(color);

            // Calculate bounding box while iterating vertices
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            // Build coverage polygon from triangle strip
            // Triangle strip vertices alternate: left1, right1, left2, right2, ...
            // Convert to polygon: down the left side, then back up the right side
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                // Skip vertex 0 (color data), start from vertex 1
                // Collect left edge (odd indices) and right edge (even indices)
                var leftEdge = new List<Point>();
                var rightEdge = new List<Point>();

                for (int i = 1; i < vertices.Count; i++)
                {
                    var v = vertices[i];
                    var pt = new Point(v.Easting, v.Northing);

                    // Track bounding box
                    if (v.Easting < minX) minX = v.Easting;
                    if (v.Easting > maxX) maxX = v.Easting;
                    if (v.Northing < minY) minY = v.Northing;
                    if (v.Northing > maxY) maxY = v.Northing;

                    if (i % 2 == 1)
                        leftEdge.Add(pt);
                    else
                        rightEdge.Add(pt);
                }

                if (leftEdge.Count > 0 && rightEdge.Count > 0)
                {
                    // Draw as single polygon: down left edge, back up right edge
                    ctx.BeginFigure(leftEdge[0], true);
                    for (int i = 1; i < leftEdge.Count; i++)
                        ctx.LineTo(leftEdge[i]);

                    // Connect to right edge at the end
                    if (rightEdge.Count > 0)
                        ctx.LineTo(rightEdge[rightEdge.Count - 1]);

                    // Go back up the right edge
                    for (int i = rightEdge.Count - 2; i >= 0; i--)
                        ctx.LineTo(rightEdge[i]);

                    ctx.EndFigure(true);
                }
            }

            // Mark as finalized if patch is no longer active (complete)
            bool isFinalized = !patch.IsActive;

            // Update or add the cached entry with bounding box
            if (p < _cachedCoverageGeometry.Count)
            {
                _cachedCoverageGeometry[p] = (geometry, brush, vertices.Count, isFinalized, minX, minY, maxX, maxY);
            }
            else
            {
                _cachedCoverageGeometry.Add((geometry, brush, vertices.Count, isFinalized, minX, minY, maxX, maxY));
            }

            // Track active patches for efficient drawing (avoid O(n) scan)
            if (!isFinalized)
            {
                _activePatchIndices.Add(p);
            }

            // Mark bitmap cache as needing rebuild
            // (color batches are updated incrementally when finalized)
            _coverageBitmapDirty = true;
        }
    }

    // Mouse interaction support (for external control)
    public void StartPan(Point position)
    {
        _isPanning = true;
        _lastMousePosition = position;
    }

    public void StartRotate(Point position)
    {
        _isRotating = true;
        _lastMousePosition = position;
    }

    public void UpdateMouse(Point position)
    {
        if (_isPanning || _isRotating)
        {
            // Handled by OnPointerMoved
        }
    }

    public void EndPanRotate()
    {
        _isPanning = false;
        _isRotating = false;
    }

    /// <summary>
    /// Convert screen coordinates to world coordinates (Easting, Northing)
    /// </summary>
    public (double Easting, double Northing) ScreenToWorld(double screenX, double screenY)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return (_cameraX, _cameraY);

        // Calculate view dimensions
        double aspect = Bounds.Width / Bounds.Height;
        double viewWidth = 200.0 * aspect / _zoom;
        double viewHeight = 200.0 / _zoom;

        // Convert screen position to normalized coordinates (-0.5 to 0.5)
        double normalizedX = (screenX / Bounds.Width) - 0.5;
        double normalizedY = 0.5 - (screenY / Bounds.Height); // Flip Y

        // Convert to world offset from camera center
        double worldOffsetX = normalizedX * viewWidth;
        double worldOffsetY = normalizedY * viewHeight;

        // Apply rotation
        double cos = Math.Cos(_rotation);
        double sin = Math.Sin(_rotation);
        double rotatedX = worldOffsetX * cos - worldOffsetY * sin;
        double rotatedY = worldOffsetX * sin + worldOffsetY * cos;

        // Add camera position
        return (_cameraX + rotatedX, _cameraY + rotatedY);
    }
}

/// <summary>
/// Event arguments for map click events containing world coordinates
/// </summary>
public class MapClickEventArgs : EventArgs
{
    public double Easting { get; }
    public double Northing { get; }

    public MapClickEventArgs(double easting, double northing)
    {
        Easting = easting;
        Northing = northing;
    }
}

/// <summary>
/// Custom draw operation for coverage rendering using direct Skia access.
/// This bypasses Avalonia's DrawingContext overhead for better performance.
/// The draw operation renders in world coordinates - the parent context handles transforms.
/// </summary>
public class CoverageDrawOperation : ICustomDrawOperation
{
    private readonly List<(SKPath Path, SKPaint Paint)> _coveragePaths;

    public Rect Bounds { get; }

    public CoverageDrawOperation(Rect bounds, IReadOnlyList<CoveragePatch> patches)
    {
        Bounds = bounds;
        _coveragePaths = new List<(SKPath, SKPaint)>();

        // Pre-build Skia paths for all patches
        BuildCoveragePaths(patches);
    }

    private void BuildCoveragePaths(IReadOnlyList<CoveragePatch> patches)
    {
        foreach (var patch in patches)
        {
            if (!patch.IsRenderable || patch.Vertices.Count < 4)
                continue;

            var vertices = patch.Vertices;

            // Create paint with patch color (60% alpha)
            var paint = new SKPaint
            {
                Color = new SKColor(patch.Color.R, patch.Color.G, patch.Color.B, 152),
                Style = SKPaintStyle.Fill,
                IsAntialias = false // Faster without antialiasing for coverage
            };

            // Build path from triangle strip (convert to polygon)
            var path = new SKPath();
            var leftEdge = new List<SKPoint>();
            var rightEdge = new List<SKPoint>();

            // Skip vertex 0 (color data), collect left (odd) and right (even) edges
            for (int i = 1; i < vertices.Count; i++)
            {
                var v = vertices[i];
                var pt = new SKPoint((float)v.Easting, (float)v.Northing);
                if (i % 2 == 1)
                    leftEdge.Add(pt);
                else
                    rightEdge.Add(pt);
            }

            if (leftEdge.Count > 0 && rightEdge.Count > 0)
            {
                // Draw as polygon: down left edge, back up right edge
                path.MoveTo(leftEdge[0]);
                for (int i = 1; i < leftEdge.Count; i++)
                    path.LineTo(leftEdge[i]);

                // Connect to right edge at end
                path.LineTo(rightEdge[rightEdge.Count - 1]);

                // Back up right edge
                for (int i = rightEdge.Count - 2; i >= 0; i--)
                    path.LineTo(rightEdge[i]);

                path.Close();
            }

            _coveragePaths.Add((path, paint));
        }
    }

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null)
            return; // Skia not available

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        // Draw all coverage paths (transform already applied by parent context)
        foreach (var (path, paint) in _coveragePaths)
        {
            canvas.DrawPath(path, paint);
        }
    }

    public void Dispose()
    {
        foreach (var (path, paint) in _coveragePaths)
        {
            path.Dispose();
            paint.Dispose();
        }
        _coveragePaths.Clear();
    }

    public bool HitTest(Point p) => false;
    public bool Equals(ICustomDrawOperation? other) => false;
}
