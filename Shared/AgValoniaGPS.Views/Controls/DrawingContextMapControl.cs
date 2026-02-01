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

    // Grid visibility property
    bool IsGridVisible { get; set; }

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

    // Avalonia styled property for bitmap-based coverage rendering
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
    private int _boundaryPointsWhenSet; // Track point count when boundary was set (for debugging)
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

    // WriteableBitmap for bitmap-based coverage rendering
    // O(1) render time - blit pre-rendered bitmap each frame
    private WriteableBitmap? _coverageWriteableBitmap;
    private const double MIN_BITMAP_CELL_SIZE = 0.1; // Preferred resolution (matches RTK precision)
    private const int MAX_BITMAP_DIMENSION = 16384; // Max pixels per dimension (~1GB at 4 bytes/pixel)
    private double _actualBitmapCellSize = MIN_BITMAP_CELL_SIZE; // Dynamically adjusted for large fields

    // Thumbnail bitmap for zoomed-out views (avoids expensive GPU downscaling)
    private WriteableBitmap? _coverageThumbnail;
    private const double THUMBNAIL_CELL_SIZE = 1.0; // 10x lower resolution than full
    private const double THUMBNAIL_ZOOM_THRESHOLD = 0.3; // Use thumbnail when zoom < this
    private int _thumbnailWidth, _thumbnailHeight;
    private bool _thumbnailNeedsRebuild = true;

    // Background compositing - background image is composited into coverage bitmap
    private bool _backgroundComposited = false;
    // Flag to preserve bitmap when explicitly initialized (don't dispose when no coverage)
    private bool _bitmapExplicitlyInitialized = false;

    // EXPERIMENTAL: Use Rgb565 (16-bit) at full 0.1m resolution
    // If viable, WriteableBitmap can serve as both detection AND display
    private const bool USE_RGB565_FULL_RESOLUTION = true;
    private double _bitmapMinE, _bitmapMinN, _bitmapMaxE, _bitmapMaxN; // World coordinates of bitmap bounds
    private int _bitmapWidth, _bitmapHeight; // Pixel dimensions
    private bool _bitmapNeedsFullRebuild = true;
    private bool _bitmapNeedsIncrementalUpdate = false;
    private bool _bitmapUpdatePending = false; // Prevents re-entry during update

    // Provider for coverage bitmap data (from ICoverageMapService)
    private Func<(double MinE, double MaxE, double MinN, double MaxN)?>? _coverageBoundsProvider;
    // Provider signature: (cellSize, viewMinE, viewMaxE, viewMinN, viewMaxN) -> cells
    private Func<double, double, double, double, double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? _coverageAllCellsProvider;
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
    private static readonly RenderOptions _highQualityRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
    private static readonly RenderOptions _lowQualityRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.LowQuality };
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

        // DEBUG: Log which control is rendering (reduced frequency)
        // Console.WriteLine($"[Render] Control={GetHashCode()}, bounds={bounds.Width:F0}x{bounds.Height:F0}, explicit={_bitmapExplicitlyInitialized}");

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
            // Skip if background is composited into coverage bitmap
            if (_backgroundImage != null && !_backgroundComposited)
            {
                DrawBackgroundImage(context);
            }

            // Draw grid (if visible)
            if (IsGridVisible)
            {
                DrawGrid(context, viewWidth, viewHeight);
            }

            // Draw coverage FIRST (bottom layer) - Rgb565 has no alpha, so it's opaque
            // Everything else draws on top of coverage
            var covSw = System.Diagnostics.Stopwatch.StartNew();
            if (_coveragePatches.Count > 0 || _coverageBoundsProvider != null || _bitmapExplicitlyInitialized)
            {
                DrawCoverage(context);
            }
            covSw.Stop();

            // Draw boundary (on top of coverage)
            var boundSw = System.Diagnostics.Stopwatch.StartNew();
            if (_boundary != null)
            {
                DrawBoundary(context);
            }
            boundSw.Stop();

            // Log timing every 60 frames
            if (_renderCounter % 60 == 0)
            {
                Console.WriteLine($"[Timing] Render: zoom={_zoom:F3}, coverage={covSw.ElapsedMilliseconds}ms, boundary={boundSw.ElapsedMilliseconds}ms");
            }

            // Draw headland line (on top of coverage and boundary)
            if (_isHeadlandVisible && _headlandLine != null && _headlandLine.Count > 2)
            {
                DrawHeadlandLine(context);
            }

            // Draw headland preview (semi-transparent)
            if (_headlandPreview != null && _headlandPreview.Count > 2)
            {
                DrawHeadlandPreview(context);
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
            Debug.WriteLine($"[Timing] Render: {_lastFullRenderMs:F2}ms, CovDraw: {_lastCoverageRenderMs:F2}ms, Patches: {_lastDrawnPatchCount}");
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
        if (_boundary == null)
        {
            if (_renderCounter % 60 == 0)
                Console.WriteLine("[MapControl] DrawBoundary: _boundary is null!");
            return;
        }

        // Draw outer boundary
        if (_boundary.OuterBoundary != null && _boundary.OuterBoundary.IsValid && _boundary.OuterBoundary.Points.Count > 1)
        {
            // Log occasionally to confirm we're drawing
            if (_renderCounter % 60 == 0)
            {
                Console.WriteLine($"[MapControl] DrawBoundary: Drawing outer boundary with {_boundary.OuterBoundary.Points.Count} points");
            }
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
        else
        {
            // Log why we're not drawing
            if (_renderCounter % 60 == 0)
                Console.WriteLine($"[MapControl] DrawBoundary: NOT drawing outer! OuterBoundary={_boundary.OuterBoundary != null}, IsValid={_boundary.OuterBoundary?.IsValid}, Points={_boundary.OuterBoundary?.Points?.Count ?? 0}");
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

        // Use bitmap-based rendering if provider is available or bitmap was explicitly initialized
        if (_coverageBoundsProvider != null || _bitmapExplicitlyInitialized)
        {
            drawnCount = DrawCoverageBitmap(context);
        }
        else
        {
            // Fall back to patch-based rendering (legacy)
            drawnCount = DrawCoveragePatches(context, visMinX, visMaxX, visMinY, visMaxY);
        }

        _profileSw.Stop();
        _lastCoverageRenderMs = _profileSw.Elapsed.TotalMilliseconds;
        _lastDrawnPatchCount = drawnCount;
    }

    /// <summary>
    /// THE ONLY PLACE the coverage WriteableBitmap is created.
    /// Creates bitmap, loads background PNG if available, otherwise fills with black.
    /// Call this on field load and when coverage is cleared/reset.
    /// </summary>
    private unsafe void CreateCoverageBitmap()
    {
        if (_bitmapWidth <= 0 || _bitmapHeight <= 0)
        {
            Console.WriteLine($"[CreateCoverageBitmap] Invalid dimensions: {_bitmapWidth}x{_bitmapHeight}");
            return;
        }

        // Dispose old bitmap
        _coverageWriteableBitmap?.Dispose();

        // Create new bitmap - always use Rgb565 for consistency
        _coverageWriteableBitmap = new WriteableBitmap(
            new PixelSize(_bitmapWidth, _bitmapHeight),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Rgb565);

        long memMB = (long)_bitmapWidth * _bitmapHeight * 2 / 1024 / 1024;
        Console.WriteLine($"[CreateCoverageBitmap] Created {_bitmapWidth}x{_bitmapHeight} Rgb565 bitmap (~{memMB}MB)");

        // Initialize bitmap content: background image or black
        using (var framebuffer = _coverageWriteableBitmap.Lock())
        {
            int stride = framebuffer.RowBytes;
            byte* ptr = (byte*)framebuffer.Address;
            int bufferSize = stride * _bitmapHeight;

            // Clear to black first
            new Span<byte>(ptr, bufferSize).Clear();

            // Composite background if available
            if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
            {
                Console.WriteLine($"[CreateCoverageBitmap] Compositing background from {_backgroundImagePath}");
                CompositeBackgroundIntoBuffer((ushort*)ptr, stride / 2);
            }
            else
            {
                Console.WriteLine($"[CreateCoverageBitmap] No background, initialized to black");
            }
        }

        // Set state flags
        _backgroundComposited = true;
        _thumbnailNeedsRebuild = true;
        _bitmapExplicitlyInitialized = true;
    }

    /// <summary>
    /// Update coverage bitmap if needed. Called outside of render pass via Dispatcher.
    /// Does NOT create the bitmap - only updates existing bitmap with coverage cells.
    /// </summary>
    private void UpdateCoverageBitmapIfNeeded()
    {
        Console.WriteLine($"[UpdateCovBitmapIfNeeded] boundsProvider={_coverageBoundsProvider != null}, cellsProvider={_coverageAllCellsProvider != null}, needsRebuild={_bitmapNeedsFullRebuild}");

        if (_coverageBoundsProvider == null || _coverageAllCellsProvider == null)
        {
            Console.WriteLine("[UpdateCovBitmapIfNeeded] No providers, returning early");
            return;
        }

        // Get coverage bounds
        var bounds = _coverageBoundsProvider();
        Console.WriteLine($"[UpdateCovBitmapIfNeeded] bounds={bounds != null}, explicit={_bitmapExplicitlyInitialized}");
        if (bounds == null)
        {
            // No coverage data - but if bitmap was explicitly initialized (with background),
            // preserve it so the background stays visible
            Console.WriteLine($"[UpdateCovBitmapIfNeeded] bounds=null, preserving bitmap (explicit={_bitmapExplicitlyInitialized})");
            if (_coverageWriteableBitmap != null && !_bitmapExplicitlyInitialized)
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

        // Calculate optimal cell size
        double cellSize;

        if (USE_RGB565_FULL_RESOLUTION)
        {
            // Full 0.1m resolution - WriteableBitmap serves as both detection and display
            cellSize = MIN_BITMAP_CELL_SIZE;
        }
        else
        {
            // Scale up for large fields to fit in ~200MB (32-bit BGRA)
            const long MAX_PIXELS = 50_000_000;
            cellSize = MIN_BITMAP_CELL_SIZE;

            long pixelsAtMinRes = (long)Math.Ceiling(worldWidth / MIN_BITMAP_CELL_SIZE) *
                                  (long)Math.Ceiling(worldHeight / MIN_BITMAP_CELL_SIZE);

            if (pixelsAtMinRes > MAX_PIXELS)
            {
                double scaleFactor = Math.Sqrt((double)pixelsAtMinRes / MAX_PIXELS);
                cellSize = MIN_BITMAP_CELL_SIZE * scaleFactor;
                if (cellSize <= 0.2) cellSize = 0.2;
                else if (cellSize <= 0.25) cellSize = 0.25;
                else if (cellSize <= 0.35) cellSize = 0.35;
                else if (cellSize <= 0.5) cellSize = 0.5;
                else if (cellSize <= 0.75) cellSize = 0.75;
                else cellSize = Math.Ceiling(cellSize);
            }
        }

        _actualBitmapCellSize = cellSize;

        int requiredWidth = (int)Math.Ceiling(worldWidth / cellSize);
        int requiredHeight = (int)Math.Ceiling(worldHeight / cellSize);

        // Ensure valid dimensions
        if (requiredWidth <= 0 || requiredHeight <= 0)
            return;

        // Check if we need to rebuild the bitmap (bounds changed or first time)
        bool boundsChanged = _coverageWriteableBitmap == null ||
            Math.Abs(_bitmapMinE - minE) > 0.01 ||
            Math.Abs(_bitmapMinN - minN) > 0.01 ||
            _bitmapWidth != requiredWidth ||
            _bitmapHeight != requiredHeight;

        if (boundsChanged)
        {
            // Bounds changed - update dimensions and create new bitmap
            _bitmapMinE = minE;
            _bitmapMinN = minN;
            _bitmapMaxE = maxE;
            _bitmapMaxN = maxN;
            _bitmapWidth = requiredWidth;
            _bitmapHeight = requiredHeight;
            _bitmapNeedsFullRebuild = true;

            // Use unified bitmap creation
            CreateCoverageBitmap();
        }

        // Update bitmap with coverage cells
        if (_bitmapNeedsFullRebuild)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int cellCount = UpdateCoverageBitmapFull();
            sw.Stop();
            Console.WriteLine($"[Timing] CovBitmap: Full rebuild {cellCount} cells in {sw.ElapsedMilliseconds}ms");
            _bitmapNeedsFullRebuild = false;
            _bitmapNeedsIncrementalUpdate = false;
            _thumbnailNeedsRebuild = true; // Rebuild thumbnail after full rebuild
        }
        else if (_bitmapNeedsIncrementalUpdate)
        {
            // Incremental update - only add new cells (fast, O(new cells) not O(total coverage))
            int cellCount = UpdateCoverageBitmapIncremental();
            if (cellCount > 0)
            {
                Console.WriteLine($"[Timing] CovBitmap: Incremental {cellCount} cells");
                _thumbnailNeedsRebuild = true; // Rebuild thumbnail after incremental update
            }
            _bitmapNeedsIncrementalUpdate = false;
        }

        // Update thumbnail if needed (for fast zoomed-out rendering)
        if (_thumbnailNeedsRebuild && _coverageWriteableBitmap != null)
        {
            UpdateCoverageThumbnail();
            _thumbnailNeedsRebuild = false;
        }
    }

    /// <summary>
    /// Generate a low-resolution thumbnail from the full bitmap for fast zoomed-out rendering.
    /// </summary>
    private unsafe void UpdateCoverageThumbnail()
    {
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
            return;

        // Calculate thumbnail dimensions (10x smaller)
        int scale = (int)(THUMBNAIL_CELL_SIZE / MIN_BITMAP_CELL_SIZE);
        _thumbnailWidth = (_bitmapWidth + scale - 1) / scale;
        _thumbnailHeight = (_bitmapHeight + scale - 1) / scale;

        // Create or recreate thumbnail bitmap
        if (_coverageThumbnail == null ||
            _coverageThumbnail.PixelSize.Width != _thumbnailWidth ||
            _coverageThumbnail.PixelSize.Height != _thumbnailHeight)
        {
            _coverageThumbnail?.Dispose();
            _coverageThumbnail = new WriteableBitmap(
                new PixelSize(_thumbnailWidth, _thumbnailHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Rgb565);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Downsample from full bitmap to thumbnail
        using var srcBuffer = _coverageWriteableBitmap.Lock();
        using var dstBuffer = _coverageThumbnail.Lock();

        ushort* src = (ushort*)srcBuffer.Address;
        ushort* dst = (ushort*)dstBuffer.Address;
        int srcStride = srcBuffer.RowBytes / 2; // ushort stride
        int dstStride = dstBuffer.RowBytes / 2;

        // Simple point sampling (take center pixel of each block)
        for (int ty = 0; ty < _thumbnailHeight; ty++)
        {
            int sy = ty * scale + scale / 2;
            if (sy >= _bitmapHeight) sy = _bitmapHeight - 1;

            for (int tx = 0; tx < _thumbnailWidth; tx++)
            {
                int sx = tx * scale + scale / 2;
                if (sx >= _bitmapWidth) sx = _bitmapWidth - 1;

                dst[ty * dstStride + tx] = src[sy * srcStride + sx];
            }
        }

        sw.Stop();
        Console.WriteLine($"[Timing] Thumbnail: Created {_thumbnailWidth}x{_thumbnailHeight} in {sw.ElapsedMilliseconds}ms");
    }

    /// <summary>
    /// Composite the background image into the coverage bitmap.
    /// This allows us to draw a single bitmap instead of background + coverage separately.
    /// </summary>
    public unsafe void CompositeBackgroundIntoBitmap()
    {
        if (_backgroundImage == null || _coverageWriteableBitmap == null ||
            _bitmapWidth == 0 || _bitmapHeight == 0)
        {
            _backgroundComposited = false;
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Calculate the overlap between background bounds and coverage bounds
        double overlapMinE = Math.Max(_bgMinX, _bitmapMinE);
        double overlapMaxE = Math.Min(_bgMaxX, _bitmapMaxE);
        double overlapMinN = Math.Max(_bgMinY, _bitmapMinN);
        double overlapMaxN = Math.Min(_bgMaxY, _bitmapMaxN);

        if (overlapMinE >= overlapMaxE || overlapMinN >= overlapMaxN)
        {
            Console.WriteLine("[Background] No overlap between background and coverage bounds");
            _backgroundComposited = false;
            return;
        }

        // Background image dimensions and world-to-pixel scale
        int bgWidth = _backgroundImage.PixelSize.Width;
        int bgHeight = _backgroundImage.PixelSize.Height;
        double bgWorldWidth = _bgMaxX - _bgMinX;
        double bgWorldHeight = _bgMaxY - _bgMinY;
        double bgPixelsPerMeterX = bgWidth / bgWorldWidth;
        double bgPixelsPerMeterY = bgHeight / bgWorldHeight;

        // Copy background to a WriteableBitmap so we can read pixels
        using var bgWriteable = new WriteableBitmap(
            new PixelSize(bgWidth, bgHeight),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888,
            Avalonia.Platform.AlphaFormat.Premul);

        // Render background image to the writeable bitmap
        using (var bgBuffer = bgWriteable.Lock())
        {
            // Use RenderTargetBitmap to render the image
            using var renderTarget = new RenderTargetBitmap(new PixelSize(bgWidth, bgHeight));
            using (var ctx = renderTarget.CreateDrawingContext())
            {
                ctx.DrawImage(_backgroundImage, new Rect(0, 0, bgWidth, bgHeight));
            }

            // Now copy from RenderTargetBitmap to our buffer via SaveAsXxx workaround
            // Actually, let's use a simpler approach - render directly and copy
        }

        // Alternative: Use SkiaSharp to decode the image directly
        // For now, let's try rendering to a temp surface
        byte[]? bgPixelData = null;
        try
        {
            // Create temp WriteableBitmap and render the background to it
            using var tempBitmap = new WriteableBitmap(
                new PixelSize(bgWidth, bgHeight),
                new Vector(96, 96),
                Avalonia.Platform.PixelFormat.Bgra8888,
                Avalonia.Platform.AlphaFormat.Premul);

            // We can't easily render an Avalonia Bitmap to a WriteableBitmap
            // Instead, reload from file using SkiaSharp
            if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
            {
                using var skBitmap = SKBitmap.Decode(_backgroundImagePath);
                if (skBitmap != null)
                {
                    bgPixelData = new byte[skBitmap.Width * skBitmap.Height * 4];
                    var pixels = skBitmap.Pixels;
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        bgPixelData[i * 4 + 0] = pixels[i].Blue;
                        bgPixelData[i * 4 + 1] = pixels[i].Green;
                        bgPixelData[i * 4 + 2] = pixels[i].Red;
                        bgPixelData[i * 4 + 3] = pixels[i].Alpha;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Background] Failed to decode background: {ex.Message}");
            _backgroundComposited = false;
            return;
        }

        if (bgPixelData == null)
        {
            Console.WriteLine("[Background] Failed to get background pixel data");
            _backgroundComposited = false;
            return;
        }

        // Lock coverage bitmap for writing
        using var covBuffer = _coverageWriteableBitmap.Lock();
        ushort* covPixels = (ushort*)covBuffer.Address;
        int covStride = covBuffer.RowBytes / 2;

        // Sample background into coverage bitmap
        // Coverage bitmap: pixel (x,y) corresponds to world (_bitmapMinE + x*cellSize, _bitmapMinN + y*cellSize)
        int pixelsWritten = 0;
        for (int cy = 0; cy < _bitmapHeight; cy++)
        {
            double worldN = _bitmapMinN + cy * _actualBitmapCellSize;
            if (worldN < overlapMinN || worldN >= overlapMaxN) continue;

            // Background Y is flipped (0 = top = maxY)
            int bgY = (int)((_bgMaxY - worldN) * bgPixelsPerMeterY);
            if (bgY < 0 || bgY >= bgHeight) continue;

            for (int cx = 0; cx < _bitmapWidth; cx++)
            {
                double worldE = _bitmapMinE + cx * _actualBitmapCellSize;
                if (worldE < overlapMinE || worldE >= overlapMaxE) continue;

                int bgX = (int)((worldE - _bgMinX) * bgPixelsPerMeterX);
                if (bgX < 0 || bgX >= bgWidth) continue;

                // Read BGRA from background
                int bgIdx = (bgY * bgWidth + bgX) * 4;
                byte b = bgPixelData[bgIdx];
                byte g = bgPixelData[bgIdx + 1];
                byte r = bgPixelData[bgIdx + 2];

                // Convert to Rgb565
                ushort rgb565 = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));

                // Write to coverage bitmap
                covPixels[cy * covStride + cx] = rgb565;
                pixelsWritten++;
            }
        }

        sw.Stop();
        Console.WriteLine($"[Background] Composited {pixelsWritten} pixels into coverage bitmap in {sw.ElapsedMilliseconds}ms");
        _backgroundComposited = true;
        _thumbnailNeedsRebuild = true; // Thumbnail needs to include background too
    }

    /// <summary>
    /// Clear background from coverage bitmap (fill with black).
    /// Called when coverage is erased.
    /// </summary>
    public void ClearBackgroundFromBitmap()
    {
        _backgroundComposited = false;
        // The bitmap will be cleared when coverage is cleared
    }

    /// <summary>
    /// Composite background into an already-locked buffer.
    /// Used during full rebuild to avoid double-locking.
    /// </summary>
    private unsafe void CompositeBackgroundIntoBuffer(ushort* covPixels, int covStride)
    {
        Console.WriteLine($"[CompositeBackground] Starting: path={_backgroundImagePath}");

        // Only check path - we load from file via SkiaSharp, don't need _backgroundImage
        if (string.IsNullOrEmpty(_backgroundImagePath) || !File.Exists(_backgroundImagePath))
        {
            Console.WriteLine("[CompositeBackground] FAILED: No background image path");
            return;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Calculate the overlap between background bounds and coverage bounds
        double overlapMinE = Math.Max(_bgMinX, _bitmapMinE);
        double overlapMaxE = Math.Min(_bgMaxX, _bitmapMaxE);
        double overlapMinN = Math.Max(_bgMinY, _bitmapMinN);
        double overlapMaxN = Math.Min(_bgMaxY, _bitmapMaxN);

        Console.WriteLine($"[CompositeBackground] BG bounds: E[{_bgMinX:F1},{_bgMaxX:F1}] N[{_bgMinY:F1},{_bgMaxY:F1}]");
        Console.WriteLine($"[CompositeBackground] Bitmap bounds: E[{_bitmapMinE:F1},{_bitmapMaxE:F1}] N[{_bitmapMinN:F1},{_bitmapMaxN:F1}]");
        Console.WriteLine($"[CompositeBackground] Overlap: E[{overlapMinE:F1},{overlapMaxE:F1}] N[{overlapMinN:F1},{overlapMaxN:F1}]");

        if (overlapMinE >= overlapMaxE || overlapMinN >= overlapMaxN)
        {
            Console.WriteLine("[CompositeBackground] FAILED: No overlap between bounds");
            return;
        }

        // Load background using SkiaSharp
        byte[]? bgPixelData = null;
        int bgWidth = 0, bgHeight = 0;

        try
        {
            using var skBitmap = SKBitmap.Decode(_backgroundImagePath);
            if (skBitmap != null)
            {
                bgWidth = skBitmap.Width;
                bgHeight = skBitmap.Height;
                bgPixelData = new byte[bgWidth * bgHeight * 4];
                var pixels = skBitmap.Pixels;
                for (int i = 0; i < pixels.Length; i++)
                {
                    bgPixelData[i * 4 + 0] = pixels[i].Blue;
                    bgPixelData[i * 4 + 1] = pixels[i].Green;
                    bgPixelData[i * 4 + 2] = pixels[i].Red;
                    bgPixelData[i * 4 + 3] = pixels[i].Alpha;
                }
            }
        }
        catch
        {
            return;
        }

        if (bgPixelData == null || bgWidth == 0 || bgHeight == 0)
            return;

        double bgWorldWidth = _bgMaxX - _bgMinX;
        double bgWorldHeight = _bgMaxY - _bgMinY;
        double bgPixelsPerMeterX = bgWidth / bgWorldWidth;
        double bgPixelsPerMeterY = bgHeight / bgWorldHeight;

        int pixelsWritten = 0;
        for (int cy = 0; cy < _bitmapHeight; cy++)
        {
            double worldN = _bitmapMinN + cy * _actualBitmapCellSize;
            if (worldN < overlapMinN || worldN >= overlapMaxN) continue;

            int bgY = (int)((_bgMaxY - worldN) * bgPixelsPerMeterY);
            if (bgY < 0 || bgY >= bgHeight) continue;

            for (int cx = 0; cx < _bitmapWidth; cx++)
            {
                double worldE = _bitmapMinE + cx * _actualBitmapCellSize;
                if (worldE < overlapMinE || worldE >= overlapMaxE) continue;

                int bgX = (int)((worldE - _bgMinX) * bgPixelsPerMeterX);
                if (bgX < 0 || bgX >= bgWidth) continue;

                int bgIdx = (bgY * bgWidth + bgX) * 4;
                byte b = bgPixelData[bgIdx];
                byte g = bgPixelData[bgIdx + 1];
                byte r = bgPixelData[bgIdx + 2];

                ushort rgb565 = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                covPixels[cy * covStride + cx] = rgb565;
                pixelsWritten++;
            }
        }

        sw.Stop();
        Console.WriteLine($"[Background] Composited {pixelsWritten} pixels in {sw.ElapsedMilliseconds}ms");
        _backgroundComposited = true;
    }

    /// <summary>
    /// Draw coverage using WriteableBitmap (PERF-004).
    /// O(1) render time - just blit the pre-rendered bitmap.
    /// Bitmap is updated outside of render pass via MarkCoverageDirty.
    /// </summary>
    private int DrawCoverageBitmap(DrawingContext context)
    {
        // Debug: Log what we have (only once per second to reduce spam)
        // Console.WriteLine($"[DrawCovBitmap] bitmap={_coverageWriteableBitmap != null}, w={_bitmapWidth}, h={_bitmapHeight}, explicit={_bitmapExplicitlyInitialized}");

        // If bitmap not ready yet, check if we need to create one
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
        {
            // Console.WriteLine("[DrawCovBitmap] Bitmap not ready, returning 0");
            // Only schedule bitmap creation if there's actual coverage to show
            // This prevents allocating closures every frame when there's no coverage
            if (!_bitmapUpdatePending && _coverageBoundsProvider != null)
            {
                var bounds = _coverageBoundsProvider();
                if (bounds != null)
                {
                    _bitmapUpdatePending = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateCoverageBitmapIfNeeded();
                        _bitmapUpdatePending = false;
                    }, DispatcherPriority.Background);
                }
            }
            return 0; // Bitmap not ready yet
        }

        // Draw the bitmap
        double worldWidth = _bitmapMaxE - _bitmapMinE;
        double worldHeight = _bitmapMaxN - _bitmapMinN;
        var destRect = new Rect(_bitmapMinE, _bitmapMinN, worldWidth, worldHeight);

        // Use thumbnail when zoomed out to avoid expensive GPU downscaling
        if (_zoom < THUMBNAIL_ZOOM_THRESHOLD && _coverageThumbnail != null)
        {
            // Using thumbnail for zoomed-out view
            var srcRect = new Rect(0, 0, _thumbnailWidth, _thumbnailHeight);
            using (context.PushRenderOptions(_lowQualityRenderOptions))
            {
                context.DrawImage(_coverageThumbnail, srcRect, destRect);
            }
            return _thumbnailWidth * _thumbnailHeight;
        }
        // Full bitmap path

        // Use full-resolution bitmap when zoomed in
        var fullSrcRect = new Rect(0, 0, _bitmapWidth, _bitmapHeight);

        // Composite background into bitmap on first draw
        if (!_backgroundComposited)
        {
            using (var fb = _coverageWriteableBitmap.Lock())
            {
                unsafe
                {
                    ushort* pixels = (ushort*)fb.Address;
                    int stride = fb.RowBytes / 2;
                    int count = _bitmapWidth * _bitmapHeight;

                    // Check if background image file exists
                    if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
                    {
                        Console.WriteLine($"[DrawCovBitmap] Loading background from {_backgroundImagePath}");
                        CompositeBackgroundIntoBuffer(pixels, stride);
                    }
                    else
                    {
                        // No background - fill with black
                        for (int i = 0; i < count; i++)
                            pixels[i] = 0;
                    }
                }
            }
            _backgroundComposited = true;
        }

        // Use LowQuality when moderately zoomed out, HighQuality when zoomed in
        var renderOptions = _zoom < 0.5 ? _lowQualityRenderOptions : _highQualityRenderOptions;
        using (context.PushRenderOptions(renderOptions))
        {
            context.DrawImage(_coverageWriteableBitmap, fullSrcRect, destRect);
        }

        return _bitmapWidth * _bitmapHeight;
    }

    /// <summary>
    /// Update coverage bitmap with all cells (full rebuild).
    /// Writes directly to framebuffer - no managed buffer allocation.
    /// </summary>
    private unsafe int UpdateCoverageBitmapFull()
    {
        Console.WriteLine($"[UpdateCovBitmapFull] Called: control={GetHashCode()}, bitmap={_coverageWriteableBitmap != null}, provider={_coverageAllCellsProvider != null}, bgImage={_backgroundImage != null}, bgPath={_backgroundImagePath}");

        if (_coverageWriteableBitmap == null || _coverageAllCellsProvider == null)
            return 0;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        int stride = framebuffer.RowBytes;
        int bufferSize = stride * _bitmapHeight;
        byte* ptr = (byte*)framebuffer.Address;

        // Clear framebuffer to black first
        new Span<byte>(ptr, bufferSize).Clear();

        // If background image exists, composite it into the bitmap
        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            Console.WriteLine($"[UpdateCovBitmapFull] Compositing background from {_backgroundImagePath}");
            CompositeBackgroundIntoBuffer((ushort*)ptr, stride / 2);
        }
        else
        {
            Console.WriteLine($"[UpdateCovBitmapFull] No background to composite, path={_backgroundImagePath}");
        }

        int cellCount = 0;
        // Query only cells within the bitmap bounds - O(viewport) not O(total coverage)
        foreach (var (cellX, cellY, color) in _coverageAllCellsProvider(
            _actualBitmapCellSize, _bitmapMinE, _bitmapMaxE, _bitmapMinN, _bitmapMaxN))
        {
            // Convert cell coordinates to bitmap pixel
            // CellY is relative to minN (increasing north)
            // Don't flip Y - bitmap top-left is at (minE, minN) which matches cell (0,0)
            int px = cellX;
            int py = cellY;

            if (px >= 0 && px < _bitmapWidth && py >= 0 && py < _bitmapHeight)
            {
                if (USE_RGB565_FULL_RESOLUTION)
                {
                    // Rgb565 format: 2 bytes per pixel
                    // Bits: RRRR RGGG GGGB BBBB (little-endian: low byte first)
                    ushort* pixel = (ushort*)(ptr + py * stride + px * 2);
                    ushort rgb565 = (ushort)(
                        ((color.R >> 3) << 11) |  // 5 bits red
                        ((color.G >> 2) << 5) |   // 6 bits green
                        (color.B >> 3));          // 5 bits blue
                    *pixel = rgb565;
                }
                else
                {
                    // Bgra8888 format: 4 bytes per pixel
                    byte* pixel = ptr + py * stride + px * 4;
                    pixel[0] = color.B;
                    pixel[1] = color.G;
                    pixel[2] = color.R;
                    pixel[3] = 200; // Semi-transparent
                }
                cellCount++;
            }
        }

        return cellCount;
    }

    /// <summary>
    /// Update coverage bitmap with only new cells (incremental update).
    /// Writes directly to framebuffer - no buffer copying.
    /// </summary>
    private unsafe int UpdateCoverageBitmapIncremental()
    {
        if (_coverageWriteableBitmap == null || _coverageNewCellsProvider == null)
            return 0;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        int stride = framebuffer.RowBytes;
        byte* ptr = (byte*)framebuffer.Address;

        int cellCount = 0;
        foreach (var (cellX, cellY, color) in _coverageNewCellsProvider(_actualBitmapCellSize))
        {
            if (cellX >= 0 && cellX < _bitmapWidth && cellY >= 0 && cellY < _bitmapHeight)
            {
                if (USE_RGB565_FULL_RESOLUTION)
                {
                    // Rgb565 format: 2 bytes per pixel
                    ushort* pixel = (ushort*)(ptr + cellY * stride + cellX * 2);
                    ushort rgb565 = (ushort)(
                        ((color.R >> 3) << 11) |  // 5 bits red
                        ((color.G >> 2) << 5) |   // 6 bits green
                        (color.B >> 3));          // 5 bits blue
                    *pixel = rgb565;
                }
                else
                {
                    // Bgra8888 format: 4 bytes per pixel
                    byte* pixel = ptr + cellY * stride + cellX * 4;
                    pixel[0] = color.B;
                    pixel[1] = color.G;
                    pixel[2] = color.R;
                    pixel[3] = 200; // Semi-transparent
                }
                cellCount++;
            }
        }

        return cellCount;
    }

    // ========== Direct Pixel Access Methods (for unified bitmap) ==========

    /// <summary>
    /// Get a coverage pixel value at the given local coordinates.
    /// Returns 0 if out of bounds or bitmap not allocated.
    /// </summary>
    public ushort GetCoveragePixel(int localX, int localY)
    {
        if (_coverageWriteableBitmap == null ||
            localX < 0 || localX >= _bitmapWidth ||
            localY < 0 || localY >= _bitmapHeight)
            return 0;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            if (USE_RGB565_FULL_RESOLUTION)
            {
                ushort* ptr = (ushort*)framebuffer.Address;
                return ptr[localY * _bitmapWidth + localX];
            }
            else
            {
                // Bgra8888 - convert to "is covered" check (non-zero alpha = covered)
                byte* ptr = (byte*)framebuffer.Address;
                int offset = localY * framebuffer.RowBytes + localX * 4;
                return ptr[offset + 3] != 0 ? (ushort)1 : (ushort)0;
            }
        }
    }

    /// <summary>
    /// Set a coverage pixel value at the given local coordinates.
    /// </summary>
    public void SetCoveragePixel(int localX, int localY, ushort rgb565)
    {
        if (_coverageWriteableBitmap == null ||
            localX < 0 || localX >= _bitmapWidth ||
            localY < 0 || localY >= _bitmapHeight)
            return;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            if (USE_RGB565_FULL_RESOLUTION)
            {
                ushort* ptr = (ushort*)framebuffer.Address;
                ptr[localY * _bitmapWidth + localX] = rgb565;
            }
            else
            {
                // Convert Rgb565 to Bgra8888
                byte r = (byte)((rgb565 >> 11) << 3);
                byte g = (byte)(((rgb565 >> 5) & 0x3F) << 2);
                byte b = (byte)((rgb565 & 0x1F) << 3);
                byte* ptr = (byte*)framebuffer.Address;
                int offset = localY * framebuffer.RowBytes + localX * 4;
                ptr[offset + 0] = b;
                ptr[offset + 1] = g;
                ptr[offset + 2] = r;
                ptr[offset + 3] = 200; // Semi-transparent
            }
        }
    }

    /// <summary>
    /// Clear all coverage pixels - resets to background image or black.
    /// </summary>
    public void ClearCoveragePixels()
    {
        if (_coverageWriteableBitmap == null)
            return;

        using var framebuffer = _coverageWriteableBitmap.Lock();
        int stride = framebuffer.RowBytes;
        int bufferSize = stride * _bitmapHeight;
        unsafe
        {
            byte* ptr = (byte*)framebuffer.Address;

            // Clear to black first
            new Span<byte>(ptr, bufferSize).Clear();

            // Re-composite background if available (check path only, load from file)
            if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
            {
                Console.WriteLine($"[ClearCoveragePixels] Re-compositing background from {_backgroundImagePath}");
                CompositeBackgroundIntoBuffer((ushort*)ptr, stride / 2);
            }
        }

        // Rebuild thumbnail
        _thumbnailNeedsRebuild = true;
        InvalidateVisual();
    }

    /// <summary>
    /// Get the coverage pixel buffer as a ushort array (for save operations).
    /// Returns null if bitmap not allocated.
    /// </summary>
    public ushort[]? GetCoveragePixelBuffer()
    {
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
            return null;

        var pixels = new ushort[_bitmapWidth * _bitmapHeight];
        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            if (USE_RGB565_FULL_RESOLUTION)
            {
                // Direct copy - bitmap is already Rgb565
                ushort* src = (ushort*)framebuffer.Address;
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = src[i];
            }
            else
            {
                // Convert Bgra8888 to Rgb565
                byte* src = (byte*)framebuffer.Address;
                int stride = framebuffer.RowBytes;
                for (int y = 0; y < _bitmapHeight; y++)
                {
                    for (int x = 0; x < _bitmapWidth; x++)
                    {
                        int offset = y * stride + x * 4;
                        byte b = src[offset + 0];
                        byte g = src[offset + 1];
                        byte r = src[offset + 2];
                        byte a = src[offset + 3];
                        if (a == 0)
                            pixels[y * _bitmapWidth + x] = 0;
                        else
                            pixels[y * _bitmapWidth + x] = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
                    }
                }
            }
        }
        return pixels;
    }

    /// <summary>
    /// Set the coverage pixel buffer from a ushort array (for load operations).
    /// Allocates/resizes bitmap if needed using CreateCoverageBitmap().
    /// </summary>
    public void SetCoveragePixelBuffer(ushort[] pixels)
    {
        if (pixels == null || _bitmapWidth == 0 || _bitmapHeight == 0)
            return;

        // Ensure bitmap exists with correct size - use unified creation
        if (_coverageWriteableBitmap == null ||
            _coverageWriteableBitmap.PixelSize.Width != _bitmapWidth ||
            _coverageWriteableBitmap.PixelSize.Height != _bitmapHeight)
        {
            CreateCoverageBitmap();
        }

        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            if (USE_RGB565_FULL_RESOLUTION)
            {
                // Direct copy - bitmap is Rgb565
                ushort* dst = (ushort*)framebuffer.Address;
                int count = Math.Min(pixels.Length, _bitmapWidth * _bitmapHeight);
                for (int i = 0; i < count; i++)
                    dst[i] = pixels[i];
            }
            else
            {
                // Convert Rgb565 to Bgra8888
                byte* dst = (byte*)framebuffer.Address;
                int stride = framebuffer.RowBytes;
                int count = Math.Min(pixels.Length, _bitmapWidth * _bitmapHeight);
                for (int i = 0; i < count; i++)
                {
                    int x = i % _bitmapWidth;
                    int y = i / _bitmapWidth;
                    int offset = y * stride + x * 4;
                    ushort rgb565 = pixels[i];
                    if (rgb565 == 0)
                    {
                        dst[offset + 0] = 0;
                        dst[offset + 1] = 0;
                        dst[offset + 2] = 0;
                        dst[offset + 3] = 0;
                    }
                    else
                    {
                        dst[offset + 0] = (byte)((rgb565 & 0x1F) << 3);        // B
                        dst[offset + 1] = (byte)(((rgb565 >> 5) & 0x3F) << 2); // G
                        dst[offset + 2] = (byte)((rgb565 >> 11) << 3);         // R
                        dst[offset + 3] = 200;                                  // A
                    }
                }
            }
        }

        InvalidateVisual();
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
        var newOuterPoints = boundary?.OuterBoundary?.Points?.Count ?? 0;
        Console.WriteLine($"[MapControl] SetBoundary called: boundary={boundary != null}, outerPoints={newOuterPoints}");

        _boundary = boundary;
        _boundaryPointsWhenSet = newOuterPoints;
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
        Console.WriteLine($"[MapControl] SetBackgroundImage: {imagePath}, control={GetHashCode()}");
        Console.WriteLine($"[MapControl] Background bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");

        _backgroundImagePath = imagePath;
        _bgMinX = minX;
        _bgMaxY = maxY;
        _bgMaxX = maxX;
        _bgMinY = minY;
        _backgroundComposited = false;

        // Load the bitmap
        _backgroundImage?.Dispose();
        _backgroundImage = null;

        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                _backgroundImage = new Bitmap(imagePath);
                Console.WriteLine($"[MapControl] Loaded background image: {_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height}");
                Debug.WriteLine($"[DrawingContextMapControl] Loaded background image: {imagePath} ({_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height})");
                Debug.WriteLine($"  Bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");

                // Mark as not composited - DrawCoverageBitmap will do the compositing
                _backgroundComposited = false;
                Console.WriteLine("[MapControl] Background image loaded, will composite on next draw");
                InvalidateVisual();
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
        // Only clear in-memory bitmap, NOT the path - path is needed to reload from file
        Console.WriteLine($"[MapControl] ClearBackground() called - disposing image but keeping path={_backgroundImagePath}");
        _backgroundImage?.Dispose();
        _backgroundImage = null;
        // DON'T clear _backgroundImagePath - we need it to reload from file
        // _backgroundComposited stays as-is since bitmap content is unchanged
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

    public void SetCoverageBitmapProviders(
        Func<(double MinE, double MaxE, double MinN, double MaxN)?>? boundsProvider,
        Func<double, double, double, double, double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? allCellsProvider,
        Func<double, IEnumerable<(int CellX, int CellY, CoverageColor Color)>>? newCellsProvider)
    {
        _coverageBoundsProvider = boundsProvider;
        _coverageAllCellsProvider = allCellsProvider;
        _coverageNewCellsProvider = newCellsProvider;
        _bitmapNeedsFullRebuild = true;
    }

    public void MarkCoverageDirty()
    {
        _bitmapNeedsIncrementalUpdate = true;

        // Schedule bitmap update
        if (!_bitmapUpdatePending)
        {
            _bitmapUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                UpdateCoverageBitmapIfNeeded();
                _bitmapUpdatePending = false;
            }, DispatcherPriority.Background);
        }
    }

    public void MarkCoverageFullRebuildNeeded()
    {
        Console.WriteLine($"[MapControl] MarkCoverageFullRebuildNeeded() called, pending={_bitmapUpdatePending}");
        _bitmapNeedsFullRebuild = true;

        // Schedule bitmap update
        if (!_bitmapUpdatePending)
        {
            Console.WriteLine("[MapControl] Scheduling UpdateCoverageBitmapIfNeeded via Dispatcher");
            _bitmapUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                Console.WriteLine("[MapControl] Running UpdateCoverageBitmapIfNeeded() from full rebuild request");
                UpdateCoverageBitmapIfNeeded();
                _bitmapUpdatePending = false;
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// Initialize coverage bitmap with explicit field bounds.
    /// Called on field load to eagerly create the bitmap.
    /// If background image is set, composites it; otherwise initializes to black.
    /// </summary>
    public void InitializeCoverageBitmapWithBounds(double minE, double maxE, double minN, double maxN)
    {
        Console.WriteLine($"[MapControl] InitializeCoverageBitmapWithBounds: E[{minE:F1}, {maxE:F1}] N[{minN:F1}, {maxN:F1}], control={GetHashCode()}");

        double worldWidth = maxE - minE;
        double worldHeight = maxN - minN;

        // Use the detection resolution (0.1m/pixel)
        const double cellSize = 0.1;
        int requiredWidth = (int)Math.Ceiling(worldWidth / cellSize);
        int requiredHeight = (int)Math.Ceiling(worldHeight / cellSize);

        // Ensure valid dimensions
        if (requiredWidth <= 0 || requiredHeight <= 0)
        {
            Console.WriteLine($"[MapControl] Invalid bitmap dimensions: {requiredWidth}x{requiredHeight}");
            return;
        }

        // Skip if bitmap already exists with same bounds (avoids wiping composited background)
        if (_coverageWriteableBitmap != null &&
            Math.Abs(_bitmapMinE - minE) < 0.01 &&
            Math.Abs(_bitmapMaxE - maxE) < 0.01 &&
            Math.Abs(_bitmapMinN - minN) < 0.01 &&
            Math.Abs(_bitmapMaxN - maxN) < 0.01 &&
            _bitmapWidth == requiredWidth &&
            _bitmapHeight == requiredHeight)
        {
            Console.WriteLine($"[MapControl] Bitmap already initialized with same bounds, skipping");
            return;
        }

        // Store bounds
        _bitmapMinE = minE;
        _bitmapMaxE = maxE;
        _bitmapMinN = minN;
        _bitmapMaxN = maxN;
        _actualBitmapCellSize = cellSize;
        _bitmapWidth = requiredWidth;
        _bitmapHeight = requiredHeight;

        // Use unified bitmap creation (creates bitmap, composites background or fills black)
        CreateCoverageBitmap();

        // Trigger re-render
        InvalidateVisual();

        // Mark bitmap as ready
        _bitmapNeedsFullRebuild = false;
        _bitmapNeedsIncrementalUpdate = false;
        Console.WriteLine($"[MapControl] Bitmap initialized via CreateCoverageBitmap");
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
