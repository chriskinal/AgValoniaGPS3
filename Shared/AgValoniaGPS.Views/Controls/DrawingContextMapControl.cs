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
using Avalonia.Media.Immutable;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.Track;

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

    // Tram line visualization
    void SetTramLines(
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? outerTrack,
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? innerTrack,
        IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? parallelLines);

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

    // Camera follow mode (0=NorthUp, 1=HeadingUp, 2=Free)
    int CameraFollowMode { get; set; }

    // Fired when user manually pans/drags the map
    event Action? UserPanned;

    // Reverse indicator
    bool IsReversing { get; set; }

    // Guidance look-ahead points
    void SetGuidancePoints(double goalEasting, double goalNorthing, bool isActive);

    // Auto-pan: keeps vehicle visible by panning map when vehicle nears edge
    bool AutoPanEnabled { get; set; }

}

/// <summary>
/// Lightweight snapshot of all state the render thread needs.
/// Built on UI thread, sent to handler via SendHandlerMessage.
/// Uses references (not copies) for large data — benign torn reads are acceptable.
/// </summary>
internal class MapRenderState
{
    // Camera
    public double CameraX, CameraY, Zoom, Rotation, CameraPitch;
    public bool Is3DMode, IsNorthUp, IsDayMode;

    // Screen
    public double BoundsWidth, BoundsHeight;

    // Vehicle
    public double VehicleX, VehicleY, VehicleHeading;
    public bool HasValidHeading, IsReversing, ShowVehicle;

    // Tool
    public double ToolX, ToolY, ToolHeading, ToolWidth, HitchX, HitchY;
    public bool ToolReady;
    public double HitchLength;

    // Sections
    public bool[] SectionOn = Array.Empty<bool>();
    public double[] SectionWidths = Array.Empty<double>();
    public double[] SectionLeft = Array.Empty<double>();
    public double[] SectionRight = Array.Empty<double>();
    public int[] SectionButtonState = Array.Empty<int>();
    public int NumSections;

    // Coverage bitmap (SKBitmap reference — render thread reads directly)
    public SKImage? CoverageSkImage;  // Legacy — kept for field load/save
    public SKBitmap? CoverageSkBitmap; // Direct reference for render-thread drawing
    public double BitmapMinE, BitmapMinN, BitmapMaxE, BitmapMaxN;
    public int BitmapWidth, BitmapHeight;
    public bool BitmapHasContent;
    public bool BitmapExplicitlyInitialized;

    // Boundary
    public Boundary? Boundary;

    // Headland
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec3>? HeadlandLine;
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? HeadlandPreview;
    public bool IsHeadlandVisible;

    // YouTurn
    public IReadOnlyList<(double Easting, double Northing)>? YouTurnPath;
    public bool IsInYouTurn;

    // Tracks
    public AgValoniaGPS.Models.Track.Track? ActiveTrack;
    public AgValoniaGPS.Models.Track.Track? BaseTrack;
    public AgValoniaGPS.Models.Track.Track? NextTrack;
    public AgValoniaGPS.Models.Position? PendingPointA;
    public IReadOnlyList<AgValoniaGPS.Models.Track.Track> RecordedPaths = Array.Empty<AgValoniaGPS.Models.Track.Track>();
    public IReadOnlyList<AgValoniaGPS.Models.Track.Track> ContourStrips = Array.Empty<AgValoniaGPS.Models.Track.Track>();

    // Recording
    public List<(double Easting, double Northing)>? RecordingPoints;
    public bool ShowBoundaryOffsetIndicator;
    public double BoundaryOffsetMeters;

    // Selection markers
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? SelectionMarkers;
    public (AgValoniaGPS.Models.Base.Vec2 Start, AgValoniaGPS.Models.Base.Vec2 End)? ClipLine;
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? ClipPath;

    // Flags
    public IReadOnlyList<(double Easting, double Northing, string Color, string Name)> Flags
        = Array.Empty<(double, double, string, string)>();

    // Grid
    public bool IsGridVisible;

    // Guidance
    public double GoalEasting, GoalNorthing;
    public bool GuidanceActive;

    // Ground texture (Avalonia Bitmap — safe to use from render thread via DrawBitmap)
    public Bitmap? GroundTexture;

    // Background image
    public Bitmap? BackgroundImage;
    public double BgMinX, BgMaxY, BgMaxX, BgMinY;
    public bool BackgroundComposited;

    // Vehicle image
    public IImage? VehicleImage;

    // Display config flags
    public bool PolygonsVisible;
    public bool SectionLinesVisible;
    public bool SvennArrowVisible;
    public bool DirectionMarkersVisible;
    public bool FieldTextureVisible;
    public bool ExtraGuidelines;
    public int ExtraGuidelinesCount;
    public bool HeadlandDistanceVisible;
    public double HeadlandProximityDistance;
    public bool HeadlandProximityWarning;
    public bool HasHeadland;

    // Coverage patches (for wireframe/section lines/direction markers)
    public IReadOnlyList<CoveragePatch> CoveragePatches = Array.Empty<CoveragePatch>();
    public List<(Geometry Geometry, IBrush Brush, int VertexCount, bool IsFinalized,
        double MinX, double MinY, double MaxX, double MaxY)>? CachedCoverageGeometry;

    // Tram lines
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? TramOuterTrack;
    public IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? TramInnerTrack;
    public IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? TramParallelLines;
    public AgValoniaGPS.Models.Configuration.TramDisplayMode TramDisplayMode;
    public float TramAlpha;

    // Vehicle config
    public double AntennaPivot, AntennaOffset;
    public bool IsMetric;
}

/// <summary>
/// Cross-platform map control using Avalonia's CompositionCustomVisualHandler.
/// Rendering runs on the compositor render thread for smooth 60fps.
/// Works on Desktop, iOS, and Android without platform-specific rendering code.
/// </summary>
public class DrawingContextMapControl : Control, ISharedMapControl
{
    // Avalonia styled property for grid visibility
    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<DrawingContextMapControl, bool>(nameof(IsGridVisible), defaultValue: true);

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
    private bool _isNorthUp = false;
    private bool _isDayMode = true;

    // Camera follow mode: 0=NorthUp, 1=HeadingUp, 2=Free
    private int _cameraFollowMode = 0;
    public event Action? UserPanned;

    // Reverse indicator
    private bool _isReversing;

    // Heading validity (set after first GPS position update with movement)
    private bool _hasValidHeading;

    // Guidance look-ahead
    private double _goalEasting, _goalNorthing;
    private bool _guidanceActive;

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
    private bool _toolPositionReady;

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
    private Point _panStartPosition;
    private bool _hasDraggedPastThreshold = false;
    private double _rotationOnPanStart = 0;
    private const double DragThreshold = 5.0; // pixels before triggering Free mode

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

    // Web Mercator bounds for proper satellite tile sampling
    private double _bgMercatorMinX, _bgMercatorMaxX, _bgMercatorMinY, _bgMercatorMaxY;
    private double _fieldOriginLat, _fieldOriginLon;
    private double _metersPerDegreeLat, _metersPerDegreeLon;
    private bool _useMercatorSampling;

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

    // Tram lines
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? _tramOuterTrack;
    private IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? _tramInnerTrack;
    private IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? _tramParallelLines;

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
    private bool _bitmapHasContent; // true when bitmap has background image or painted coverage
    private double _coverageBoundsMinX, _coverageBoundsMinY, _coverageBoundsMaxX, _coverageBoundsMaxY;
    private const double COVERAGE_PIXELS_PER_METER = 0.5; // 0.5 pixels per meter = 2m resolution

    // Track what's already rendered to bitmap for incremental updates
    private int _lastRenderedPatchCount = 0;
    private List<int> _lastRenderedVertexCounts = new();

    // Track first non-finalized patch to skip finalized patches entirely in loop
    private int _firstNonFinalizedPatchIndex = 0;

    // (CoverageDrawOperation removed — rendering moved to MapCompositionHandler)

    // WriteableBitmap for bitmap-based coverage rendering
    // O(1) render time - blit pre-rendered bitmap each frame
    // Data bitmap (Rgb565) -- compact storage for save/load and pixel API
    // Display bitmap (Bgra8888) -- for rendering with black=transparent
    private WriteableBitmap? _coverageWriteableBitmap;
    private WriteableBitmap? _coverageDisplayBitmap;
    // SKBitmap for pixel writes (SetCoveragePixel), SKImage for GPU rendering.
    // SKImage is an immutable snapshot — GPU caches the texture.
    // Only recreated when pixels actually change.
    private SKBitmap? _coverageSkBitmap;
    private SKBitmap? _previousCoverageSkBitmap; // Keeps old bitmap alive while render thread may still reference it
    private SKImage? _coverageSkImage;          // Cached GPU texture — recreated on pixel change
    private SKImage? _previousSkImage;          // Keeps previous image alive while render thread may use it
    private volatile bool _coverageSkImageDirty; // True when SKBitmap has new pixels not in SKImage
    private DateTime _lastSkImageRecreateTime;  // Throttle snapshot creation (100MB copy)
    private volatile bool _writeableBitmapsDirty; // SKBitmap has pixels not yet synced to WriteableBitmaps
    private const double MIN_BITMAP_CELL_SIZE = 0.1; // Preferred resolution (matches RTK precision)
    private const int MAX_BITMAP_DIMENSION = 16384; // Max pixels per dimension (~1GB at 4 bytes/pixel)
    private double _actualBitmapCellSize = MIN_BITMAP_CELL_SIZE; // Dynamically adjusted for large fields

    // Background compositing - background image is composited into coverage bitmap
    private bool _backgroundComposited = false;
    // Flag to preserve bitmap when explicitly initialized (don't dispose when no coverage)
    private bool _bitmapExplicitlyInitialized = false;

    // Dynamic display resolution: scale based on field size to fit ~50M pixels
    // Detection stays at 0.1m (in CoverageMapService), display scales for large fields
    private const bool USE_RGB565_FULL_RESOLUTION = false;
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
    private AgValoniaGPS.Models.Track.Track? _baseTrack; // Original track (shown as dashed reference)
    private AgValoniaGPS.Models.Track.Track? _nextTrack; // Next track to follow after U-turn
    private bool _isInYouTurn; // When true, current line is dotted, next line is solid
    private AgValoniaGPS.Models.Position? _pendingPointA; // Point A while waiting for Point B
    private IReadOnlyList<AgValoniaGPS.Models.Track.Track> _recordedPaths = Array.Empty<AgValoniaGPS.Models.Track.Track>();
    private IReadOnlyList<AgValoniaGPS.Models.Track.Track> _contourStrips = Array.Empty<AgValoniaGPS.Models.Track.Track>();

    // Ground texture bitmaps (passed to render thread via state snapshot)
    private Bitmap? _groundTexture;
    private Bitmap? _groundTextureDay;
    private Bitmap? _groundTextureNight;
    // Vehicle image (passed to render thread via state snapshot)
    private IImage? _vehicleImage;

    // Composition visual for render-thread rendering
    private CompositionCustomVisual? _customVisual;
    private MapCompositionHandler? _handler;

    // Flag markers
    private IReadOnlyList<(double Easting, double Northing, string Color, string Name)> _flags = Array.Empty<(double, double, string, string)>();

    // FPS tracking (instance-based to avoid double-counting when multiple controls exist)
    private DateTime _lastFpsUpdate = DateTime.UtcNow;
    private int _frameCount;
    private double _currentFps;
    private int _lastDestRectLogSecond = -1;

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
    public double CurrentFps => _currentFps;

    /// <summary>
    /// Event raised when FPS is updated (every second)
    /// </summary>
    public event Action<double>? FpsUpdated;

    /// <summary>
    /// Static FPS for legacy bindings (returns main control's FPS if available)
    /// </summary>
    private static DrawingContextMapControl? _mainControl;
    public static double StaticCurrentFps => _mainControl?._currentFps ?? 0;

    public DrawingContextMapControl()
    {
        Debug.WriteLine("[DrawingContextMapControl] Constructor starting...");

        // Make control focusable for input
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        // Load ground textures (day and night variants)
        try
        {
            var dayUri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/GroundTexture.png");
            using var dayStream = AssetLoader.Open(dayUri);
            _groundTextureDay = new Bitmap(dayStream);

            var nightUri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/GroundTextureDark.png");
            using var nightStream = AssetLoader.Open(nightUri);
            _groundTextureNight = new Bitmap(nightStream);

            _groundTexture = _groundTextureDay;
            Debug.WriteLine("[DrawingContextMapControl] Loaded ground textures (day + night)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DrawingContextMapControl] Ground texture not found: {ex.Message}");
        }

        // All pens/brushes are now created as immutable types in MapCompositionHandler

        // Load vehicle (tractor) image from embedded resources
        LoadVehicleImage();

        // Handle visibility changes
        PropertyChanged += OnControlPropertyChanged;

        // Wire up mouse events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        // Rebuild coverage bitmap when display resolution changes
        AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display.PropertyChanged += OnDisplayConfigChanged;
    }

    private void OnDisplayConfigChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgValoniaGPS.Models.Configuration.DisplayConfig.DisplayResolutionMultiplier))
        {
            // Rebuild bitmap at new resolution using stored bounds.
            // Can't use UpdateCoverageBitmapIfNeeded — providers may be null on this instance.
            if (_coverageWriteableBitmap != null && _bitmapWidth > 0)
            {
                InitializeCoverageBitmapWithBounds(_bitmapMinE, _bitmapMaxE, _bitmapMinN, _bitmapMaxN);
            }
        }
    }

    /// <summary>
    /// Setup composition visual when control is attached to visual tree.
    /// </summary>
    /// <summary>
    /// Draw a transparent fill so the control is hit-testable for pointer events.
    /// The composition visual handles all actual rendering.
    /// </summary>
    public override void Render(Avalonia.Media.DrawingContext context)
    {
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SetupCompositionVisual();
        _mainControl = this;
    }

    private void SetupCompositionVisual()
    {
        var compositionVisual = ElementComposition.GetElementVisual(this);
        Debug.WriteLine($"[MapControl] SetupCompositionVisual: compositionVisual={compositionVisual != null}, Bounds={Bounds.Width:F0}x{Bounds.Height:F0}, Name={Name}");
        if (compositionVisual == null) return;

        var compositor = compositionVisual.Compositor;
        _handler = new MapCompositionHandler(this);
        _customVisual = compositor.CreateCustomVisual(_handler);
        _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
        ElementComposition.SetElementChildVisual(this, _customVisual);
        Debug.WriteLine($"[MapControl] SetupCompositionVisual: custom visual created, size={Bounds.Width:F0}x{Bounds.Height:F0}");

        // Send initial state
        SendStateToHandler();
    }

    /// <summary>
    /// Handle bounds changes to resize the composition visual.
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && _customVisual != null)
        {
            _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
            SendStateToHandler();
        }
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible))
        {
            bool isNowVisible = e.NewValue is true;
            if (isNowVisible)
            {
                _mainControl = this;
                SendStateToHandler(); // Resume rendering
            }
        }
    }

    /// <summary>
    /// Build a MapRenderState snapshot and send it to the composition handler.
    /// Call this whenever data changes that affects rendering.
    /// </summary>
    internal void SendStateToHandler()
    {
        if (_customVisual == null || _handler == null)
            return;

        // Ensure coverage bitmap is ready before snapshotting state
        EnsureCoverageBitmapReady();

        var displayCfg = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display;
        var vehicleCfg = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Vehicle;
        var toolCfg = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Tool;
        var fieldState = AgValoniaGPS.Models.State.ApplicationState.Instance.Field;

        var state = new MapRenderState
        {
            CameraX = _cameraX,
            CameraY = _cameraY,
            Zoom = _zoom,
            Rotation = _rotation,
            CameraPitch = _cameraPitch,
            Is3DMode = _is3DMode,
            IsNorthUp = _isNorthUp,
            IsDayMode = _isDayMode,

            BoundsWidth = Bounds.Width,
            BoundsHeight = Bounds.Height,

            VehicleX = _vehicleX,
            VehicleY = _vehicleY,
            VehicleHeading = _vehicleHeading,
            HasValidHeading = _hasValidHeading,
            IsReversing = _isReversing,
            ShowVehicle = ShowVehicle,

            ToolX = _toolX,
            ToolY = _toolY,
            ToolHeading = _toolHeading,
            ToolWidth = _toolWidth,
            HitchX = _hitchX,
            HitchY = _hitchY,
            ToolReady = _toolPositionReady,
            HitchLength = toolCfg.HitchLength,

            SectionOn = (bool[])_sectionOn.Clone(),
            SectionWidths = (double[])_sectionWidths.Clone(),
            SectionLeft = (double[])_sectionLeft.Clone(),
            SectionRight = (double[])_sectionRight.Clone(),
            SectionButtonState = (int[])_sectionButtonState.Clone(),
            NumSections = _numSections,

            CoverageSkImage = _coverageSkImage, // Legacy
            CoverageSkBitmap = _coverageSkBitmap,
            BitmapMinE = _bitmapMinE,
            BitmapMinN = _bitmapMinN,
            BitmapMaxE = _bitmapMaxE,
            BitmapMaxN = _bitmapMaxN,
            BitmapWidth = _bitmapWidth,
            BitmapHeight = _bitmapHeight,
            BitmapHasContent = _bitmapHasContent,
            BitmapExplicitlyInitialized = _bitmapExplicitlyInitialized,

            Boundary = _boundary,

            HeadlandLine = _headlandLine,
            HeadlandPreview = _headlandPreview,
            IsHeadlandVisible = _isHeadlandVisible,

            YouTurnPath = _youTurnPath,
            IsInYouTurn = _isInYouTurn,

            ActiveTrack = _activeTrack,
            BaseTrack = _baseTrack,
            NextTrack = _nextTrack,
            PendingPointA = _pendingPointA,
            RecordedPaths = _recordedPaths,
            ContourStrips = _contourStrips,

            RecordingPoints = _recordingPoints != null
                ? new List<(double, double)>(_recordingPoints) : null,
            ShowBoundaryOffsetIndicator = _showBoundaryOffsetIndicator,
            BoundaryOffsetMeters = _boundaryOffsetMeters,

            SelectionMarkers = _selectionMarkers,
            ClipLine = _clipLine,
            ClipPath = _clipPath,

            Flags = _flags,
            IsGridVisible = IsGridVisible,

            GoalEasting = _goalEasting,
            GoalNorthing = _goalNorthing,
            GuidanceActive = _guidanceActive,

            GroundTexture = _groundTexture,
            BackgroundImage = _backgroundImage,
            BgMinX = _bgMinX,
            BgMaxY = _bgMaxY,
            BgMaxX = _bgMaxX,
            BgMinY = _bgMinY,
            BackgroundComposited = _backgroundComposited,

            VehicleImage = _vehicleImage,

            PolygonsVisible = displayCfg.PolygonsVisible,
            SectionLinesVisible = displayCfg.SectionLinesVisible,
            SvennArrowVisible = displayCfg.SvennArrowVisible,
            DirectionMarkersVisible = displayCfg.DirectionMarkersVisible,
            FieldTextureVisible = displayCfg.FieldTextureVisible,
            ExtraGuidelines = displayCfg.ExtraGuidelines,
            ExtraGuidelinesCount = displayCfg.ExtraGuidelinesCount,
            HeadlandDistanceVisible = displayCfg.HeadlandDistanceVisible,
            HeadlandProximityDistance = fieldState.HeadlandProximityDistance ?? double.MaxValue,
            HeadlandProximityWarning = fieldState.HeadlandProximityWarning,
            HasHeadland = fieldState.HasHeadland,

            CoveragePatches = _coveragePatches,
            CachedCoverageGeometry = _cachedCoverageGeometry,

            TramOuterTrack = _tramOuterTrack,
            TramInnerTrack = _tramInnerTrack,
            TramParallelLines = _tramParallelLines,
            TramDisplayMode = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Tram.DisplayMode,
            TramAlpha = (float)AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Tram.Alpha,

            AntennaPivot = vehicleCfg.AntennaPivot,
            AntennaOffset = vehicleCfg.AntennaOffset,
            IsMetric = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.IsMetric,
        };

        _customVisual.SendHandlerMessage(state);
    }

    /// <summary>
    /// Update FPS from composition handler callback (fires on UI thread).
    /// </summary>
    internal void ReportFps(double fps)
    {
        _currentFps = fps;
        FpsUpdated?.Invoke(fps);
    }

    // GetCameraTransform and Draw* methods have been moved to MapCompositionHandler

    /// <summary>
    /// THE ONLY PLACE the coverage WriteableBitmap is created.
    /// Creates bitmap, loads background PNG if available, otherwise fills with black.
    /// Call this on field load and when coverage is cleared/reset.
    /// </summary>
    private unsafe void CreateCoverageBitmap()
    {
        if (_bitmapWidth <= 0 || _bitmapHeight <= 0)
        {
            Debug.WriteLine($"[CreateCoverageBitmap] Invalid dimensions: {_bitmapWidth}x{_bitmapHeight}");
            return;
        }

        // Dispose old bitmaps — keep SKBitmap alive until render thread moves on
        _coverageWriteableBitmap?.Dispose();
        _coverageDisplayBitmap?.Dispose();
        _previousCoverageSkBitmap?.Dispose(); // Dispose the one from TWO recreations ago (render thread is done with it)
        _previousCoverageSkBitmap = _coverageSkBitmap; // Keep current one alive for render thread
        _coverageSkBitmap = null; // Clear so SendStateToHandler sends null until new one is ready

        // Data bitmap: Rgb565 for compact storage and pixel API
        _coverageWriteableBitmap = new WriteableBitmap(
            new PixelSize(_bitmapWidth, _bitmapHeight),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Rgb565);

        // Display bitmap: Bgra8888 for rendering with transparency (black = alpha 0)
        _coverageDisplayBitmap = new WriteableBitmap(
            new PixelSize(_bitmapWidth, _bitmapHeight),
            new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888);

        // SKBitmap shadow for lock-free rendering — CoverageBitmapDrawOp reads this
        // without WriteableBitmap.Lock, avoiding contention with SetCoveragePixel
        _coverageSkBitmap = new SKBitmap(_bitmapWidth, _bitmapHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        _coverageSkBitmap.Erase(SKColor.Empty);

        long memMB = (long)_bitmapWidth * _bitmapHeight * 10 / 1024 / 1024; // 2 + 4 + 4 bytes per pixel
        Debug.WriteLine($"[CreateCoverageBitmap] Created {_bitmapWidth}x{_bitmapHeight} Rgb565+Bgra8888 bitmaps (~{memMB}MB)");

        // Clear data bitmap to black (0x0000)
        using (var framebuffer = _coverageWriteableBitmap.Lock())
        {
            int stride = framebuffer.RowBytes;
            byte* ptr = (byte*)framebuffer.Address;
            int bufferSize = stride * _bitmapHeight;
            new Span<byte>(ptr, bufferSize).Clear();
        }

        // Clear display bitmap to transparent (alpha=0)
        using (var framebuffer = _coverageDisplayBitmap.Lock())
        {
            int stride = framebuffer.RowBytes;
            byte* ptr = (byte*)framebuffer.Address;
            int bufferSize = stride * _bitmapHeight;
            new Span<byte>(ptr, bufferSize).Clear();
        }

        // Composite background if available (uses its own lock)
        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            Debug.WriteLine($"[CreateCoverageBitmap] Compositing background from {_backgroundImagePath}");
            CompositeBackgroundIntoBitmap();
            SyncSkBitmapFromDisplay();
            _bitmapHasContent = true;
        }
        else
        {
            Debug.WriteLine($"[CreateCoverageBitmap] No background, initialized to black (transparent until coverage painted)");
            _backgroundComposited = false;
            _bitmapHasContent = false;
        }

        _bitmapExplicitlyInitialized = true;
    }

    /// <summary>
    /// Update coverage bitmap if needed. Called outside of render pass via Dispatcher.
    /// Does NOT create the bitmap - only updates existing bitmap with coverage cells.
    /// </summary>
    private void UpdateCoverageBitmapIfNeeded()
    {
        Debug.WriteLine($"[UpdateCovBitmapIfNeeded] boundsProvider={_coverageBoundsProvider != null}, cellsProvider={_coverageAllCellsProvider != null}, needsRebuild={_bitmapNeedsFullRebuild}");

        if (_coverageBoundsProvider == null || _coverageAllCellsProvider == null)
        {
            Debug.WriteLine("[UpdateCovBitmapIfNeeded] No providers, returning early");
            return;
        }

        // Get coverage bounds
        var bounds = _coverageBoundsProvider();
        Debug.WriteLine($"[UpdateCovBitmapIfNeeded] bounds={bounds != null}, explicit={_bitmapExplicitlyInitialized}");
        if (bounds == null)
        {
            // No coverage data - but if bitmap was explicitly initialized (with background),
            // preserve it so the background stays visible
            Debug.WriteLine($"[UpdateCovBitmapIfNeeded] bounds=null, preserving bitmap (explicit={_bitmapExplicitlyInitialized})");
            if (_coverageWriteableBitmap != null && !_bitmapExplicitlyInitialized)
            {
                Debug.WriteLine("[Timing] CovBitmap: Clearing bitmap (no coverage)");
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
            // Cap at ~25M pixels (~100MB BGRA8888) for smooth GPU rendering
            const long MAX_PIXELS = 25_000_000;
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

        // Apply user resolution multiplier (1.0=High, 1.5=Medium, 2.0=Low)
        cellSize = ApplyResolutionMultiplier(cellSize);

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
            Debug.WriteLine($"[Timing] CovBitmap: Full rebuild {cellCount} cells in {sw.ElapsedMilliseconds}ms");
            _bitmapNeedsFullRebuild = false;
            _bitmapNeedsIncrementalUpdate = false;
        }
        else if (_bitmapNeedsIncrementalUpdate)
        {
            // Incremental update - only add new cells (fast, O(new cells) not O(total coverage))
            int cellCount = UpdateCoverageBitmapIncremental();
            if (cellCount > 0)
            {
                Debug.WriteLine($"[Timing] CovBitmap: Incremental {cellCount} cells");
            }
            _bitmapNeedsIncrementalUpdate = false;
        }
    }

    /// <summary>
    /// Generate a medium-resolution bitmap (4x downsampled) for mid-zoom rendering.
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
            Debug.WriteLine("[Background] No overlap between background and coverage bounds");
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
            Debug.WriteLine($"[Background] Failed to decode background: {ex.Message}");
            _backgroundComposited = false;
            return;
        }

        if (bgPixelData == null)
        {
            Debug.WriteLine("[Background] Failed to get background pixel data");
            _backgroundComposited = false;
            return;
        }

        // Lock coverage bitmap for writing
        using var covBuffer = _coverageWriteableBitmap.Lock();
        ushort* covPixels = (ushort*)covBuffer.Address;
        int covStride = covBuffer.RowBytes / 2;

        // Composite using GATHER approach with proper coordinate transform chain:
        // Local Plane → WGS84 → Web Mercator → sample background pixel
        // This is the inverse of: Web Mercator → WGS84 → Local Plane (used for boundary points)
        int pixelsWritten = 0;
        double halfCell = _actualBitmapCellSize / 2.0;

        // Web Mercator constants
        const double R = 6378137.0; // Earth radius for EPSG:3857
        const double DEG_TO_RAD = Math.PI / 180.0;

        // Pre-compute Mercator scaling factors
        double mercXRange = _bgMercatorMaxX - _bgMercatorMinX;
        double mercYRange = _bgMercatorMaxY - _bgMercatorMinY;
        bool useMercator = _useMercatorSampling && mercXRange > 0 && mercYRange > 0;

        // Helper function matching LocalPlane.MetersPerDegreeLon(lat)
        static double MetersPerDegreeLon(double lat)
        {
            double latRad = lat * Math.PI / 180.0;
            return 111412.84 * Math.Cos(latRad)
                - 93.5 * Math.Cos(3.0 * latRad)
                + 0.118 * Math.Cos(5.0 * latRad);
        }

        // Use direct local-to-pixel mapping (linear)
        // This is consistent with how the background bounds are computed
        useMercator = false;

        for (int cy = 0; cy < _bitmapHeight; cy++)
        {
            // Row 0 = south edge of coverage bitmap (_bitmapMinN)
            // destRect places bitmap at (_bitmapMinE, _bitmapMinN), so row 0 maps to south
            double worldN = _bitmapMinN + cy * _actualBitmapCellSize + halfCell;
            if (worldN < overlapMinN || worldN >= overlapMaxN) continue;

            for (int cx = 0; cx < _bitmapWidth; cx++)
            {
                double worldE = _bitmapMinE + cx * _actualBitmapCellSize + halfCell;
                if (worldE < overlapMinE || worldE >= overlapMaxE) continue;

                int bgX, bgY;

                if (useMercator)
                {
                    // Step 1: Local Plane → WGS84 (matching LocalPlane.ConvertGeoCoordToWgs84)
                    double lat = _fieldOriginLat + (worldN / _metersPerDegreeLat);
                    double lon = _fieldOriginLon + (worldE / MetersPerDegreeLon(lat)); // Use lat-dependent formula!

                    // Step 2: WGS84 → Web Mercator (EPSG:3857)
                    double mercX = R * lon * DEG_TO_RAD;
                    double latRad = lat * DEG_TO_RAD;
                    double mercY = R * Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));

                    // Step 3: Web Mercator → background image pixel
                    bgX = (int)((mercX - _bgMercatorMinX) / mercXRange * bgWidth);
                    bgY = (int)((_bgMercatorMaxY - mercY) / mercYRange * bgHeight);
                }
                else
                {
                    // Fallback: linear sampling (when Mercator bounds not available)
                    bgX = (int)((worldE - _bgMinX) * bgPixelsPerMeterX);
                    bgY = (int)((_bgMaxY - worldN) * bgPixelsPerMeterY);
                }

                if (bgX < 0 || bgX >= bgWidth || bgY < 0 || bgY >= bgHeight) continue;

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
        Debug.WriteLine($"[Background] Composited {pixelsWritten} pixels into coverage bitmap in {sw.ElapsedMilliseconds}ms");
        _backgroundComposited = true;

        // Sync display bitmap so background shows with proper transparency
        SyncDisplayBitmap();
        SyncSkBitmapFromDisplay();
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

    // CoverageBitmapDrawOp removed — coverage now drawn directly in MapCompositionHandler

    // DrawCoverageBitmap removed — coverage now drawn directly in MapCompositionHandler
    // EnsureCoverageBitmapReady is called from SendStateToHandler before sending state
    private void EnsureCoverageBitmapReady()
    {
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
        {
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
                        SendStateToHandler(); // Resend after bitmap created
                    }, DispatcherPriority.Background);
                }
            }
            return;
        }

        // Composite background into bitmap on first draw
        if (!_backgroundComposited)
        {
            if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
            {
                CompositeBackgroundIntoBitmap();
                SyncSkBitmapFromDisplay();
            }
            else
            {
                using (var fb = _coverageWriteableBitmap.Lock())
                {
                    unsafe
                    {
                        int count = _bitmapWidth * _bitmapHeight;
                        ushort* pixels = (ushort*)fb.Address;
                        for (int i = 0; i < count; i++)
                            pixels[i] = 0;
                    }
                }
                _coverageSkBitmap?.Erase(SKColor.Empty);
                _backgroundComposited = true;
            }
        }
    }

    /// <summary>
    /// Update coverage bitmap with all cells (full rebuild).
    /// Writes directly to framebuffer - no managed buffer allocation.
    /// </summary>
    private unsafe int UpdateCoverageBitmapFull()
    {
        Debug.WriteLine($"[UpdateCovBitmapFull] Called: control={GetHashCode()}, bitmap={_coverageWriteableBitmap != null}, provider={_coverageAllCellsProvider != null}, bgPath={_backgroundImagePath}");

        if (_coverageWriteableBitmap == null || _coverageAllCellsProvider == null)
            return 0;

        // Step 1: Clear to black
        using (var framebuffer = _coverageWriteableBitmap.Lock())
        {
            int bufferSize = framebuffer.RowBytes * _bitmapHeight;
            new Span<byte>((byte*)framebuffer.Address, bufferSize).Clear();
        }

        // Step 2: Composite background if available (uses its own lock)
        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            Debug.WriteLine($"[UpdateCovBitmapFull] Compositing background from {_backgroundImagePath}");
            CompositeBackgroundIntoBitmap();
            SyncSkBitmapFromDisplay();
        }
        else
        {
            Debug.WriteLine($"[UpdateCovBitmapFull] No background to composite");
        }

        // Step 3: Write coverage cells
        int cellCount = 0;
        using (var framebuffer = _coverageWriteableBitmap.Lock())
        {
            int stride = framebuffer.RowBytes;
            byte* ptr = (byte*)framebuffer.Address;

            foreach (var (cellX, cellY, color) in _coverageAllCellsProvider(
                _actualBitmapCellSize, _bitmapMinE, _bitmapMaxE, _bitmapMinN, _bitmapMaxN))
            {
                int px = cellX;
                int py = cellY;

                if (px >= 0 && px < _bitmapWidth && py >= 0 && py < _bitmapHeight)
                {
                    ushort* pixel = (ushort*)(ptr + py * stride + px * 2);
                    ushort rgb565 = (ushort)(
                        ((color.R >> 3) << 11) |
                        ((color.G >> 2) << 5) |
                        (color.B >> 3));
                    *pixel = rgb565;
                    cellCount++;
                }
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

        using var dataFb = _coverageWriteableBitmap.Lock();
        byte* dataPtr = (byte*)dataFb.Address;
        int dataStride = dataFb.RowBytes;

        // Also update display bitmap (Bgra8888) for transparent rendering
        var dispFb = _coverageDisplayBitmap?.Lock();
        uint* dispPtr = dispFb != null ? (uint*)dispFb.Address : null;

        int cellCount = 0;
        foreach (var (cellX, cellY, color) in _coverageNewCellsProvider(_actualBitmapCellSize))
        {
            if (cellX >= 0 && cellX < _bitmapWidth && cellY >= 0 && cellY < _bitmapHeight)
            {
                // Write to Rgb565 data bitmap
                ushort* pixel = (ushort*)(dataPtr + cellY * dataStride + cellX * 2);
                ushort rgb565 = (ushort)(
                    ((color.R >> 3) << 11) |
                    ((color.G >> 2) << 5) |
                    (color.B >> 3));
                *pixel = rgb565;

                // Write to Bgra8888 display bitmap (with alpha=255 for opaque)
                if (dispPtr != null)
                {
                    dispPtr[cellY * _bitmapWidth + cellX] = Rgb565ToBgra8888(rgb565);
                }

                // Write to SKBitmap shadow (lock-free rendering)
                _coverageSkBitmap?.SetPixel(cellX, cellY,
                    rgb565 == 0 ? SKColor.Empty : Rgb565ToSKColor(rgb565));
                _coverageSkImageDirty = true;

                _bitmapHasContent = true;
                cellCount++;
            }
        }

        dispFb?.Dispose();
        return cellCount;
    }

    // ========== Direct Pixel Access Methods (for unified bitmap) ==========

    /// <summary>
    /// Get a coverage pixel value at the given local coordinates.
    /// Returns 0 if out of bounds or bitmap not allocated.
    /// </summary>
    public ushort GetCoveragePixel(int localX, int localY)
    {
        if (localX < 0 || localX >= _bitmapWidth ||
            localY < 0 || localY >= _bitmapHeight)
            return 0;

        // Read from SKBitmap (always up-to-date, no Lock needed)
        if (_coverageSkBitmap != null)
        {
            var c = _coverageSkBitmap.GetPixel(localX, localY);
            if (c.Alpha == 0) return 0;
            return (ushort)(((c.Red >> 3) << 11) | ((c.Green >> 2) << 5) | (c.Blue >> 3));
        }

        // Fallback to WriteableBitmap
        if (_coverageWriteableBitmap == null) return 0;
        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            ushort* ptr = (ushort*)framebuffer.Address;
            return ptr[localY * _bitmapWidth + localX];
        }
    }

    /// <summary>
    /// Set a coverage pixel value at the given local coordinates.
    /// </summary>
    public void SetCoveragePixel(int localX, int localY, ushort rgb565)
    {
        if (localX < 0 || localX >= _bitmapWidth ||
            localY < 0 || localY >= _bitmapHeight)
            return;

        if (rgb565 != 0) _bitmapHasContent = true;

        // Write ONLY to SKBitmap (lock-free, read by render thread).
        // WriteableBitmaps are synced lazily before save via SyncWriteableBitmapsFromSk().
        // This eliminates per-pixel Lock/Unlock contention with the compositor.
        _coverageSkBitmap?.SetPixel(localX, localY,
            rgb565 == 0 ? SKColor.Empty : Rgb565ToSKColor(rgb565));
        _writeableBitmapsDirty = true;
        _coverageSkImageDirty = true;
    }

    /// <summary>
    /// Sync WriteableBitmaps from SKBitmap. Called lazily before save/load operations.
    /// </summary>
    private unsafe void SyncWriteableBitmapsFromSk()
    {
        if (!_writeableBitmapsDirty || _coverageSkBitmap == null) return;
        _writeableBitmapsDirty = false;

        var skPixels = (uint*)_coverageSkBitmap.GetPixels();
        int count = _bitmapWidth * _bitmapHeight;

        // Sync to display bitmap (Bgra8888)
        if (_coverageDisplayBitmap != null)
        {
            using var fb = _coverageDisplayBitmap.Lock();
            Buffer.MemoryCopy(skPixels, (void*)fb.Address, count * 4, count * 4);
        }

        // Sync to data bitmap (Rgb565)
        if (_coverageWriteableBitmap != null)
        {
            using var fb = _coverageWriteableBitmap.Lock();
            ushort* dst = (ushort*)fb.Address;
            for (int i = 0; i < count; i++)
            {
                uint bgra = skPixels[i];
                if ((bgra & 0xFF000000) == 0) { dst[i] = 0; continue; }
                byte r = (byte)((bgra >> 16) & 0xFF);
                byte g = (byte)((bgra >> 8) & 0xFF);
                byte b = (byte)(bgra & 0xFF);
                dst[i] = (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
            }
        }
    }

    /// <summary>
    /// Copy the entire display bitmap (Bgra8888) to the SKBitmap shadow.
    /// Call after CompositeBackgroundIntoBitmap or any bulk bitmap operation.
    /// </summary>
    private unsafe void SyncSkBitmapFromDisplay()
    {
        if (_coverageSkBitmap == null || _coverageDisplayBitmap == null) return;
        using var fb = _coverageDisplayBitmap.Lock();
        var src = (uint*)fb.Address;
        var dst = (uint*)_coverageSkBitmap.GetPixels();
        int count = _bitmapWidth * _bitmapHeight;
        Buffer.MemoryCopy(src, dst, count * 4, count * 4);
        _coverageSkImageDirty = true;
    }

    private static SKColor Rgb565ToSKColor(ushort rgb565)
    {
        byte r = (byte)((rgb565 >> 11) << 3);
        byte g = (byte)(((rgb565 >> 5) & 0x3F) << 2);
        byte b = (byte)((rgb565 & 0x1F) << 3);
        return new SKColor(r, g, b, 255);
    }

    /// <summary>
    /// Rebuild the Bgra8888 display bitmap from the Rgb565 data bitmap.
    /// Called after bulk operations (background composite, pixel buffer load).
    /// </summary>
    private unsafe void SyncDisplayBitmap()
    {
        if (_coverageWriteableBitmap == null || _coverageDisplayBitmap == null) return;

        using var dataFb = _coverageWriteableBitmap.Lock();
        using var dispFb = _coverageDisplayBitmap.Lock();

        ushort* src = (ushort*)dataFb.Address;
        uint* dst = (uint*)dispFb.Address;
        int count = _bitmapWidth * _bitmapHeight;
        for (int i = 0; i < count; i++)
            dst[i] = Rgb565ToBgra8888(src[i]);
    }

    /// <summary>
    /// Convert Rgb565 to Bgra8888. Black (0x0000) maps to transparent (alpha=0),
    /// all other colors get full opacity (alpha=255).
    /// </summary>
    private static uint Rgb565ToBgra8888(ushort rgb565)
    {
        if (rgb565 == 0) return 0; // transparent

        byte r = (byte)((rgb565 >> 11) << 3);
        byte g = (byte)(((rgb565 >> 5) & 0x3F) << 2);
        byte b = (byte)((rgb565 & 0x1F) << 3);
        return (uint)(b | (g << 8) | (r << 16) | (0xFF << 24));
    }

    private static double ApplyResolutionMultiplier(double cellSize)
    {
        var multiplier = AgValoniaGPS.Models.Configuration.ConfigurationStore.Instance.Display.DisplayResolutionMultiplier;
        if (multiplier <= 1.0) return cellSize;

        return cellSize * multiplier;
    }

    /// <summary>
    /// Clear all coverage pixels - resets to background image or black.
    /// </summary>
    public void ClearCoveragePixels()
    {
        if (_coverageWriteableBitmap == null)
            return;

        // Clear data bitmap (Rgb565)
        using (var framebuffer = _coverageWriteableBitmap.Lock())
        {
            int bufferSize = framebuffer.RowBytes * _bitmapHeight;
            unsafe
            {
                new Span<byte>((byte*)framebuffer.Address, bufferSize).Clear();
            }
        }

        // Clear SKBitmap shadow (used by render thread)
        _coverageSkBitmap?.Erase(SKColor.Empty);

        // Clear display bitmap (Bgra8888) so stale pixels don't show on save
        if (_coverageDisplayBitmap != null)
        {
            using (var framebuffer = _coverageDisplayBitmap.Lock())
            {
                int bufferSize = framebuffer.RowBytes * _bitmapHeight;
                unsafe
                {
                    new Span<byte>((byte*)framebuffer.Address, bufferSize).Clear();
                }
            }
        }

        // Re-composite background if available (uses its own lock)
        if (!string.IsNullOrEmpty(_backgroundImagePath) && File.Exists(_backgroundImagePath))
        {
            Debug.WriteLine($"[ClearCoveragePixels] Re-compositing background from {_backgroundImagePath}");
            CompositeBackgroundIntoBitmap();
            SyncSkBitmapFromDisplay();
        }

        SendStateToHandler();
    }

    /// <summary>
    /// Get the coverage pixel buffer as a ushort array (for save operations).
    /// Returns null if bitmap not allocated.
    /// </summary>
    public ushort[]? GetCoveragePixelBuffer()
    {
        if (_coverageWriteableBitmap == null || _bitmapWidth == 0 || _bitmapHeight == 0)
            return null;

        // Sync SKBitmap pixels to WriteableBitmap before reading
        SyncWriteableBitmapsFromSk();

        var pixels = new ushort[_bitmapWidth * _bitmapHeight];
        using var framebuffer = _coverageWriteableBitmap.Lock();
        unsafe
        {
            // Bitmap is always Rgb565 - direct copy
            ushort* src = (ushort*)framebuffer.Address;
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = src[i];
        }
        return pixels;
    }

    /// <summary>
    /// Get display bitmap dimensions and resolution.
    /// Returns null if bitmap not allocated.
    /// </summary>
    public (int Width, int Height, double CellSize)? GetDisplayBitmapInfo()
    {
        if (_bitmapWidth == 0 || _bitmapHeight == 0)
            return null;
        return (_bitmapWidth, _bitmapHeight, _actualBitmapCellSize);
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

        // Write to Rgb565 data bitmap
        using (var framebuffer = _coverageWriteableBitmap!.Lock())
        {
            unsafe
            {
                ushort* dst = (ushort*)framebuffer.Address;
                int count = Math.Min(pixels.Length, _bitmapWidth * _bitmapHeight);
                for (int i = 0; i < count; i++)
                {
                    if (pixels[i] != 0)
                        dst[i] = pixels[i];
                }
            }
        }

        // Sync to Bgra8888 display bitmap
        if (_coverageDisplayBitmap != null)
        {
            using var dispFb = _coverageDisplayBitmap.Lock();
            using var dataFb = _coverageWriteableBitmap.Lock();
            unsafe
            {
                ushort* src = (ushort*)dataFb.Address;
                uint* dst = (uint*)dispFb.Address;
                int count = _bitmapWidth * _bitmapHeight;
                for (int i = 0; i < count; i++)
                    dst[i] = Rgb565ToBgra8888(src[i]);
            }
        }

        _bitmapHasContent = true;

        // Sync SKBitmap shadow from display bitmap
        if (_coverageSkBitmap != null && _coverageDisplayBitmap != null)
        {
            using var dispFb = _coverageDisplayBitmap.Lock();
            unsafe
            {
                uint* src = (uint*)dispFb.Address;
                var skPixels = _coverageSkBitmap.GetPixels();
                uint* dst = (uint*)skPixels;
                int count = _bitmapWidth * _bitmapHeight;
                Buffer.MemoryCopy(src, dst, count * 4, count * 4);
            }
        }

        SendStateToHandler();
    }

    private static readonly Pen _coverageWireframePen = new Pen(new SolidColorBrush(Color.FromArgb(180, 150, 150, 150)), 0.2);
    private static readonly Pen _coverageSectionLinePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), 0.3);
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

    // DrawRecordingPoints moved to MapCompositionHandler

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

    // All Draw* methods (DrawTool, DrawVehicle, DrawSvennArrow, DrawGrid, etc.)
    // have been moved to the MapCompositionHandler inner class below.
    // The handler runs on the render thread via CompositionCustomVisualHandler.

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
            _panStartPosition = point.Position;
            _hasDraggedPastThreshold = false;
            _rotationOnPanStart = _rotation; // Save rotation to prevent GPS tick from changing it during drag
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
            // Preserve rotation from pan start -- prevent GPS tick from
            // changing rotation between PointerMoved events
            _rotation = _rotationOnPanStart;

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

            // Check if drag exceeds threshold before entering Free mode
            if (!_hasDraggedPastThreshold)
            {
                double dist = Math.Sqrt(Math.Pow(currentPos.X - _panStartPosition.X, 2) +
                                        Math.Pow(currentPos.Y - _panStartPosition.Y, 2));
                if (dist > DragThreshold)
                    _hasDraggedPastThreshold = true;
            }
            if (_hasDraggedPastThreshold)
            {
                _cameraFollowMode = 2;
                UserPanned?.Invoke();
            }
            _lastMousePosition = currentPos;
            SendStateToHandler();
            e.Handled = true;
        }
        else if (_isRotating)
        {
            double deltaX = currentPos.X - _lastMousePosition.X;
            _rotation += deltaX * 0.01;
            _cameraFollowMode = 2;
            _lastMousePosition = currentPos;
            UserPanned?.Invoke();
            SendStateToHandler();
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
        UserPanned?.Invoke();
        SendStateToHandler();
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
            _zoom = Math.Clamp(_zoom, 0.02, 100.0);
        }
        SendStateToHandler();
    }

    public double GetZoom() => _zoom;

    public (double X, double Y) GetCameraCenter() => (_cameraX, _cameraY);

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
        UserPanned?.Invoke();
        SendStateToHandler();
    }

    public void SetGridVisible(bool visible)
    {
        IsGridVisible = visible;
        SendStateToHandler();
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

    public void SetNorthUp(bool isNorthUp)
    {
        _isNorthUp = isNorthUp;
        if (isNorthUp)
        {
            _rotation = 0;
        }
        else
        {
            _rotation = -_vehicleHeading;
        }
        SendStateToHandler();
    }

    public void SetDayMode(bool isDayMode)
    {
        if (_isDayMode != isDayMode)
        {
            _isDayMode = isDayMode;
            UpdateDayNightColors();
            SendStateToHandler();
        }
    }

    private void UpdateDayNightColors()
    {
        // Day/night colors are now applied in the handler via IsDayMode state flag.
        // Only ground texture needs updating here (passed as Bitmap reference in state).
        _groundTexture = _isDayMode ? _groundTextureDay : _groundTextureNight;
        SendStateToHandler();
    }

    public void SetVehiclePosition(double x, double y, double heading)
    {
        // Mark heading as valid once vehicle has moved from origin
        if (!_hasValidHeading && (Math.Abs(x) > 0.1 || Math.Abs(y) > 0.1))
            _hasValidHeading = true;

        _vehicleX = x;
        _vehicleY = y;
        _vehicleHeading = heading;

        // Camera follow based on mode
        switch (_cameraFollowMode)
        {
            case 0: // NorthUp: center on vehicle, no rotation
                _cameraX = x;
                _cameraY = y;
                _rotation = 0;
                break;
            case 1: // HeadingUp: center on vehicle, rotate with heading
                _cameraX = x;
                _cameraY = y;
                _rotation = -heading;
                break;
            case 2: // Free: don't move camera at all
                break;
        }

        SendStateToHandler();
    }

    public void SetAllPositions(double vehicleX, double vehicleY, double vehicleHeading,
        double toolX, double toolY, double toolHeading, double toolWidth,
        double hitchX, double hitchY, bool toolReady)
    {
        // Mark heading as valid once vehicle has moved from origin
        if (!_hasValidHeading && (Math.Abs(vehicleX) > 0.1 || Math.Abs(vehicleY) > 0.1))
            _hasValidHeading = true;

        _vehicleX = vehicleX;
        _vehicleY = vehicleY;
        _vehicleHeading = vehicleHeading;
        _toolX = toolX;
        _toolY = toolY;
        _toolHeading = toolHeading;
        _toolWidth = toolWidth;
        _hitchX = hitchX;
        _hitchY = hitchY;
        _toolPositionReady = toolReady;

        // Camera follow based on mode
        switch (_cameraFollowMode)
        {
            case 0: // NorthUp
                _cameraX = vehicleX;
                _cameraY = vehicleY;
                _rotation = 0;
                break;
            case 1: // HeadingUp
                _cameraX = vehicleX;
                _cameraY = vehicleY;
                _rotation = -vehicleHeading;
                break;
            case 2: // Free
                break;
        }

        // Detect reversing
        _isReversing = vehicleHeading < 0;

        SendStateToHandler();
    }

    public void SetToolPosition(double x, double y, double heading, double width, double hitchX, double hitchY, bool isReady = true)
    {
        _toolX = x;
        _toolY = y;
        _toolHeading = heading;
        _toolWidth = width;
        _hitchX = hitchX;
        _hitchY = hitchY;
        _toolPositionReady = isReady;
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
        SendStateToHandler();
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
    public int CameraFollowMode
    {
        get => _cameraFollowMode;
        set => _cameraFollowMode = value;
    }

    public bool IsReversing
    {
        get => _isReversing;
        set => _isReversing = value;
    }

    public void SetGuidancePoints(double goalEasting, double goalNorthing, bool isActive)
    {
        _goalEasting = goalEasting;
        _goalNorthing = goalNorthing;
        _guidanceActive = isActive;
        SendStateToHandler();
    }

    public bool AutoPanEnabled
    {
        get => _autoPanEnabled;
        set => _autoPanEnabled = value;
    }

    public void SetFlags(IReadOnlyList<(double Easting, double Northing, string Color, string Name)> flags)
    {
        _flags = flags;
        SendStateToHandler();
    }

    public void SetBoundary(Boundary? boundary)
    {
        var newOuterPoints = boundary?.OuterBoundary?.Points?.Count ?? 0;
        Debug.WriteLine($"[MapControl] SetBoundary called: boundary={boundary != null}, outerPoints={newOuterPoints}");

        // Log boundary vertices to verify they match what we expect
        if (boundary?.OuterBoundary?.Points != null && boundary.OuterBoundary.Points.Count > 0)
        {
            var pts = boundary.OuterBoundary.Points;
            Debug.WriteLine($"[MapControl] SetBoundary vertices:");
            for (int i = 0; i < pts.Count; i++)
            {
                Debug.WriteLine($"[MapControl]   Point {i}: E={pts[i].Easting:F2}, N={pts[i].Northing:F2}");
            }
        }

        _boundary = boundary;
        _boundaryPointsWhenSet = newOuterPoints;
        InitializeCoverageBitmap();
        SendStateToHandler();
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _recordingPoints = new List<(double, double)>(points);
        SendStateToHandler();
    }

    public void ClearRecordingPoints()
    {
        _recordingPoints = null;
        SendStateToHandler();
    }

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY)
    {
        Debug.WriteLine($"[MapControl] SetBackgroundImage: {imagePath}, control={GetHashCode()}");
        Debug.WriteLine($"[MapControl] Background bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");

        _backgroundImagePath = imagePath;
        _bgMinX = minX;
        _bgMaxY = maxY;
        _bgMaxX = maxX;
        _bgMinY = minY;

        // Load the bitmap for potential direct drawing (fallback)
        _backgroundImage?.Dispose();
        _backgroundImage = null;

        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                _backgroundImage = new Bitmap(imagePath);
                Debug.WriteLine($"[MapControl] Loaded background image: {_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height}");
                Debug.WriteLine($"[DrawingContextMapControl] Loaded background image: {imagePath} ({_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height})");
                Debug.WriteLine($"  Bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");

                // If coverage bitmap already exists, composite the background into it immediately
                // This handles the case where boundary is set before background (new field creation)
                if (_coverageWriteableBitmap != null && _bitmapWidth > 0 && _bitmapHeight > 0)
                {
                    Debug.WriteLine("[MapControl] Coverage bitmap exists, compositing background immediately");
                    CompositeBackgroundIntoBitmap();
            SyncSkBitmapFromDisplay();
                    _backgroundComposited = true;
                }
                else
                {
                    // No coverage bitmap yet - will composite when bitmap is created
                    _backgroundComposited = false;
                    Debug.WriteLine("[MapControl] No coverage bitmap yet, will composite when created");
                }
                SendStateToHandler();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DrawingContextMapControl] Failed to load background image: {ex.Message}");
                _backgroundComposited = false;
            }
        }
        else
        {
            Debug.WriteLine($"[DrawingContextMapControl] Background image path invalid or not found: {imagePath}");
            _backgroundComposited = false;
        }
    }

    public void SetBackgroundImageWithMercator(string imagePath, double minX, double maxY, double maxX, double minY,
        double mercMinX, double mercMaxX, double mercMinY, double mercMaxY,
        double originLat, double originLon)
    {
        // Store Mercator bounds for proper sampling
        _bgMercatorMinX = mercMinX;
        _bgMercatorMaxX = mercMaxX;
        _bgMercatorMinY = mercMinY;
        _bgMercatorMaxY = mercMaxY;
        _fieldOriginLat = originLat;
        _fieldOriginLon = originLon;
        _useMercatorSampling = true;

        // Pre-compute meters per degree for this origin
        double originLatRad = originLat * Math.PI / 180.0;
        _metersPerDegreeLat = 111132.92 - 559.82 * Math.Cos(2.0 * originLatRad)
            + 1.175 * Math.Cos(4.0 * originLatRad) - 0.0023 * Math.Cos(6.0 * originLatRad);
        _metersPerDegreeLon = 111412.84 * Math.Cos(originLatRad)
            - 93.5 * Math.Cos(3.0 * originLatRad) + 0.118 * Math.Cos(5.0 * originLatRad);

        Debug.WriteLine($"[MapControl] SetBackgroundImageWithMercator: Mercator bounds Y[{mercMinY:F1}, {mercMaxY:F1}]");

        // Call the regular method for the rest
        SetBackgroundImage(imagePath, minX, maxY, maxX, minY);
    }

    public void ClearBackground()
    {
        Debug.WriteLine($"[MapControl] ClearBackground() called - fully clearing background");
        _backgroundImage?.Dispose();
        _backgroundImage = null;
        _backgroundImagePath = null;
        _backgroundComposited = false;
        _useMercatorSampling = false;
        _bgMinX = _bgMaxX = _bgMinY = _bgMaxY = 0;
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _showBoundaryOffsetIndicator = show;
        _boundaryOffsetMeters = offsetMeters;
        SendStateToHandler();
    }

    public void SetHeadlandLine(IReadOnlyList<AgValoniaGPS.Models.Base.Vec3>? headlandPoints)
    {
        _headlandLine = headlandPoints;
        SendStateToHandler();
    }

    public void SetHeadlandPreview(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? previewPoints)
    {
        _headlandPreview = previewPoints;
        SendStateToHandler();
    }

    public void SetHeadlandVisible(bool visible)
    {
        _isHeadlandVisible = visible;
        SendStateToHandler();
    }

    public void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath)
    {
        _youTurnPath = turnPath;
        SendStateToHandler();
    }

    public void SetSelectionMarkers(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? markers)
    {
        _selectionMarkers = markers;
        SendStateToHandler();
    }

    public void SetTramLines(
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? outerTrack,
        IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? innerTrack,
        IReadOnlyList<IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>>? parallelLines)
    {
        _tramOuterTrack = outerTrack;
        _tramInnerTrack = innerTrack;
        _tramParallelLines = parallelLines;
        SendStateToHandler();
    }

    public void SetClipLine(AgValoniaGPS.Models.Base.Vec2? start, AgValoniaGPS.Models.Base.Vec2? end)
    {
        _clipLine = (start.HasValue && end.HasValue) ? (start.Value, end.Value) : null;
        SendStateToHandler();
    }

    public void SetClipPath(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2>? path)
    {
        _clipPath = path;
        SendStateToHandler();
    }

    public void SetActiveTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _activeTrack = track;
        SendStateToHandler();
    }

    public void SetBaseTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _baseTrack = track;
        SendStateToHandler();
    }

    public void SetNextTrack(AgValoniaGPS.Models.Track.Track? track)
    {
        _nextTrack = track;
        SendStateToHandler();
    }

    public void SetIsInYouTurn(bool isInTurn)
    {
        _isInYouTurn = isInTurn;
        SendStateToHandler();
    }

    public void SetPendingPointA(AgValoniaGPS.Models.Position? pointA)
    {
        _pendingPointA = pointA;
        SendStateToHandler();
    }

    public void SetRecordedPaths(IReadOnlyList<AgValoniaGPS.Models.Track.Track> paths)
    {
        _recordedPaths = paths;
        SendStateToHandler();
    }

    public void SetContourStrips(IReadOnlyList<AgValoniaGPS.Models.Track.Track> strips)
    {
        _contourStrips = strips;
        SendStateToHandler();
    }

    // Coverage visualization
    public void SetCoveragePatches(IReadOnlyList<CoveragePatch> patches)
    {
        _profileSw.Restart();

        _coveragePatches = patches;

        // Maintain geometry cache for coverage rendering
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
        Debug.WriteLine($"[MapControl] MarkCoverageFullRebuildNeeded() called, pending={_bitmapUpdatePending}");
        _bitmapNeedsFullRebuild = true;

        // Schedule bitmap update
        if (!_bitmapUpdatePending)
        {
            Debug.WriteLine("[MapControl] Scheduling UpdateCoverageBitmapIfNeeded via Dispatcher");
            _bitmapUpdatePending = true;
            Dispatcher.UIThread.Post(() =>
            {
                Debug.WriteLine("[MapControl] Running UpdateCoverageBitmapIfNeeded() from full rebuild request");
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
        Debug.WriteLine($"[MapControl] InitializeCoverageBitmapWithBounds: E[{minE:F1}, {maxE:F1}] N[{minN:F1}, {maxN:F1}], control={GetHashCode()}");

        double worldWidth = maxE - minE;
        double worldHeight = maxN - minN;

        // Calculate optimal cell size using same logic as UpdateCoverageBitmapIfNeeded
        // This ensures consistency between initialization and rendering
        double cellSize;
        if (USE_RGB565_FULL_RESOLUTION)
        {
            cellSize = MIN_BITMAP_CELL_SIZE;
        }
        else
        {
            // Cap at ~25M pixels (~100MB BGRA8888) for smooth GPU rendering
            const long MAX_PIXELS = 25_000_000;
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

        // Apply user resolution multiplier (1.0=High, 1.5=Medium, 2.0=Low)
        cellSize = ApplyResolutionMultiplier(cellSize);

        int requiredWidth = (int)Math.Ceiling(worldWidth / cellSize);
        int requiredHeight = (int)Math.Ceiling(worldHeight / cellSize);

        // Ensure valid dimensions
        if (requiredWidth <= 0 || requiredHeight <= 0)
        {
            Debug.WriteLine($"[MapControl] Invalid bitmap dimensions: {requiredWidth}x{requiredHeight}");
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
            Debug.WriteLine($"[MapControl] Bitmap already initialized with same bounds, skipping");
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
        SendStateToHandler();

        // Mark bitmap as ready
        _bitmapNeedsFullRebuild = false;
        _bitmapNeedsIncrementalUpdate = false;
        Debug.WriteLine($"[MapControl] Bitmap initialized: {requiredWidth}x{requiredHeight} @ {cellSize}m");
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
        _panStartPosition = position;
        _hasDraggedPastThreshold = false;
        _rotationOnPanStart = _rotation;
    }

    public void StartRotate(Point position)
    {
        _isRotating = true;
        _lastMousePosition = position;
    }

    public void UpdateMouse(Point position)
    {
        if (_isPanning)
        {
            _rotation = _rotationOnPanStart;

            double deltaX = position.X - _lastMousePosition.X;
            double deltaY = position.Y - _lastMousePosition.Y;

            double aspect = Bounds.Width / Bounds.Height;
            double viewWidth = 200.0 * aspect / _zoom;
            double viewHeight = 200.0 / _zoom;

            double worldDeltaX = -deltaX * viewWidth / Bounds.Width;
            double worldDeltaY = deltaY * viewHeight / Bounds.Height;

            double cos = Math.Cos(_rotation);
            double sin = Math.Sin(_rotation);
            _cameraX += worldDeltaX * cos - worldDeltaY * sin;
            _cameraY += worldDeltaX * sin + worldDeltaY * cos;

            if (!_hasDraggedPastThreshold)
            {
                double dist = Math.Sqrt(Math.Pow(position.X - _panStartPosition.X, 2) +
                                        Math.Pow(position.Y - _panStartPosition.Y, 2));
                if (dist > DragThreshold)
                    _hasDraggedPastThreshold = true;
            }
            if (_hasDraggedPastThreshold)
            {
                _cameraFollowMode = 2;
                UserPanned?.Invoke();
            }
            _lastMousePosition = position;
            SendStateToHandler();
        }
        else if (_isRotating)
        {
            double deltaX = position.X - _lastMousePosition.X;
            _rotation += deltaX * 0.01;
            _cameraFollowMode = 2;
            _lastMousePosition = position;
            UserPanned?.Invoke();
            SendStateToHandler();
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

        // Reverse pitch compression (pitch compresses Y in the render transform)
        if (_is3DMode && _cameraPitch > 0.01)
        {
            double pitchFactor = Math.Max(0.3, Math.Cos(_cameraPitch));
            worldOffsetY /= pitchFactor;
        }

        // Apply rotation
        double cos = Math.Cos(_rotation);
        double sin = Math.Sin(_rotation);
        double rotatedX = worldOffsetX * cos - worldOffsetY * sin;
        double rotatedY = worldOffsetX * sin + worldOffsetY * cos;

        // Add camera position
        return (_cameraX + rotatedX, _cameraY + rotatedY);
    }

    // ======================================================================
    // MapCompositionHandler — renders on the compositor render thread
    // ======================================================================

    /// <summary>
    /// Composition handler that performs all map rendering on the render thread.
    /// Receives MapRenderState snapshots via OnMessage and draws in OnRender.
    /// </summary>
    private class MapCompositionHandler : CompositionCustomVisualHandler
    {
        private readonly DrawingContextMapControl _owner;
        private MapRenderState? _state;

        // FPS tracking (compositor frames, not content updates)
        private DateTime _lastFpsUpdate = DateTime.UtcNow;
        private int _compositorFrameCount;
        private double _currentFps;
        private int _renderCounter;
        private bool _loggedSkiaStatus;

        // Immutable pens/brushes for render thread (created once, reused)
        // Background
        private ImmutablePen? _gridPenMinorImm;
        private ImmutablePen? _gridPenMajorImm;
        private readonly ImmutablePen _gridPenAxisXImm;
        private readonly ImmutablePen _gridPenAxisYImm;
        private readonly ImmutablePen _boundaryPenOuterImm;
        private readonly ImmutablePen _boundaryPenInnerImm;
        private readonly ImmutablePen _recordingPenImm;
        private readonly ImmutablePen _headlandPenImm;
        private readonly ImmutablePen _headlandPreviewPenImm;
        private readonly ImmutablePen _clipLinePenImm;
        private readonly ImmutablePen _hitchPenImm;
        private readonly ImmutablePen _svennArrowPenImm;
        private readonly ImmutablePen _sectionOutlinePenImm;

        // SKPaint objects for SkiaSharp drawing (boundary, headland, track, etc.)
        private readonly SKPaint _boundaryOuterPaint;
        private readonly SKPaint _boundaryInnerPaint;
        private readonly SKPaint _headlandPaint;
        private readonly SKPaint _headlandPreviewPaint;
        private readonly SKPaint _recordingLinePaint;
        private readonly SKPaint _clipLinePaint;
        private readonly SKPaint _youTurnPaint;

        // Immutable brushes
        private static readonly IImmutableBrush _vehicleBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(0, 200, 0));
        private static readonly IImmutableBrush _recordingPointBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(255, 128, 0));
        private static readonly IImmutableBrush _pointABrushImm = new ImmutableSolidColorBrush(Color.FromRgb(0, 255, 0));
        private static readonly IImmutableBrush _pointBBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(255, 0, 0));
        private static readonly IImmutableBrush _sectionOffBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(242, 51, 51));
        private static readonly IImmutableBrush _sectionManualOnBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(247, 247, 0));
        private static readonly IImmutableBrush _sectionAutoOnBrushImm = new ImmutableSolidColorBrush(Color.FromRgb(0, 242, 0));

        public MapCompositionHandler(DrawingContextMapControl owner)
        {
            _owner = owner;

            // Create immutable pens
            _gridPenAxisXImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(70, 204, 51, 51)), 0.5);
            _gridPenAxisYImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(70, 51, 204, 51)), 0.5);
            _boundaryPenOuterImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(204, 242, 112, 89)), 1);
            _boundaryPenInnerImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(245, 245, 77)), 1);
            _recordingPenImm = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Cyan), 0.5);
            _headlandPenImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(251, 235, 107)), 1.0);
            _headlandPreviewPenImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(180, 77, 250, 0)), 1.5);
            _clipLinePenImm = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Red), 3);
            _hitchPenImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(255, 255, 0)), 0.15);
            _svennArrowPenImm = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(200, 255, 220, 0)), 0.4);
            _sectionOutlinePenImm = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Black), 0.1);

            // SKPaint for SkiaSharp boundary/headland/track paths
            _boundaryOuterPaint = new SKPaint
            {
                Color = new SKColor(242, 112, 89, 204),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            _boundaryInnerPaint = new SKPaint
            {
                Color = new SKColor(245, 245, 77),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            _headlandPaint = new SKPaint
            {
                Color = new SKColor(251, 235, 107),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            _headlandPreviewPaint = new SKPaint
            {
                Color = new SKColor(77, 250, 0, 180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.5f,
                IsAntialias = true
            };
            _recordingLinePaint = new SKPaint
            {
                Color = new SKColor(0, 255, 255),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.5f,
                IsAntialias = true
            };
            _clipLinePaint = new SKPaint
            {
                Color = SKColors.Red,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 3,
                IsAntialias = true
            };
            _youTurnPaint = new SKPaint
            {
                Color = new SKColor(77, 242, 77),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
        }

        public override Rect GetRenderBounds()
        {
            var s = _state;
            if (s != null && s.BoundsWidth > 0 && s.BoundsHeight > 0)
                return new Rect(0, 0, s.BoundsWidth, s.BoundsHeight);
            return new Rect(0, 0, 4000, 4000); // Large default until state arrives
        }

        public override void OnMessage(object message)
        {
            if (message is MapRenderState state)
            {
                bool firstState = _state == null;
                _state = state;
                Invalidate();
                // Start the compositor frame counter on first state
                if (firstState)
                    RegisterForNextAnimationFrameUpdate();
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            // Invalidate to re-render on every compositor tick — keeps FPS accurate
            // and ensures the latest state is always displayed
            Invalidate();
            // Keep the animation frame loop running
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            _renderCounter++;
            var s = _state;
            if (s == null) return;
            if (s.BoundsWidth <= 0 || s.BoundsHeight <= 0) return;

            // FPS tracking — count actual rendered frames
            _compositorFrameCount++;
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;
            if (elapsed >= 1.0)
            {
                _currentFps = _compositorFrameCount / elapsed;
                _compositorFrameCount = 0;
                _lastFpsUpdate = now;
                var fps = _currentFps;
                Dispatcher.UIThread.Post(() => _owner.ReportFps(fps), DispatcherPriority.Background);
            }


            try
            {
                // Background fill (screen space) — ImmediateDrawingContext only, no SKCanvas lease
                var bgColor = s.IsDayMode
                    ? Color.FromRgb(69, 102, 179)
                    : Color.FromRgb(10, 10, 10);
                drawingContext.FillRectangle(
                    new ImmutableSolidColorBrush(bgColor),
                    new Rect(0, 0, s.BoundsWidth, s.BoundsHeight));


                // Calculate view transform
                double aspect = s.BoundsWidth / s.BoundsHeight;
                double viewWidth = 200.0 * aspect / s.Zoom;
                double viewHeight = 200.0 / s.Zoom;

                var cameraMatrix = GetCameraTransform(s, viewWidth, viewHeight);
                using var cameraScope = drawingContext.PushPreTransform(cameraMatrix);

                // Ground texture
                if (s.GroundTexture != null && s.FieldTextureVisible)
                {
                    DrawGroundTexture(drawingContext, s, viewWidth, viewHeight);
                }

                // Background image (if not composited into coverage)
                if (s.BackgroundImage != null && !s.BackgroundComposited && s.FieldTextureVisible)
                {
                    DrawBackgroundImage(drawingContext, s);
                }

                // Grid
                if (s.IsGridVisible)
                {
                    DrawGrid(drawingContext, s, viewWidth, viewHeight);
                }

                // === ALL remaining drawing via SKCanvas ===
                // dc drawing after SKCanvas lease is unreliable, so everything
                // after grid/texture goes through SKCanvas.
                var skiaFeature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (!_loggedSkiaStatus)
                {
                    _loggedSkiaStatus = true;
                    Debug.WriteLine($"[MapControl:OnRender] SkiaFeature={skiaFeature != null}, owner={_owner.Name}, boundary={s.Boundary != null}");
                }
                if (skiaFeature != null)
                {
                    using var skiaLease = skiaFeature.Lease();
                    var canvas = skiaLease.SkCanvas;

                    // Each draw section is wrapped so a failure in one
                    // doesn't prevent vehicle/tool from rendering.
                    try
                    {
                        // Coverage bitmap
                        if (s.CoverageSkBitmap != null && (s.BitmapHasContent || s.BitmapExplicitlyInitialized)
                            && s.BitmapWidth > 0 && s.BitmapHeight > 0)
                            DrawCoverageBitmap(drawingContext, canvas, s);

                        // Boundary, headland, paths
                        if (s.Boundary != null)
                            DrawBoundary(canvas, s);
                        if (s.IsHeadlandVisible && s.HeadlandLine != null && s.HeadlandLine.Count > 2)
                            DrawHeadlandLine(canvas, s);
                        if (s.HeadlandPreview != null && s.HeadlandPreview.Count > 2)
                            DrawHeadlandPreview(canvas, s);
                        if (s.RecordingPoints != null && s.RecordingPoints.Count > 0)
                            DrawRecordingPointsSk(canvas, s);
                        if (s.ClipLine.HasValue || (s.ClipPath != null && s.ClipPath.Count >= 2))
                            DrawClipLineSk(canvas, s);
                        if (s.YouTurnPath != null && s.YouTurnPath.Count > 1)
                            DrawYouTurnPathSk(canvas, s);

                        // Tram lines
                        if (s.TramDisplayMode != AgValoniaGPS.Models.Configuration.TramDisplayMode.Off)
                            DrawTramLinesSk(canvas, s);

                        // Tracks
                        if (s.ActiveTrack != null || s.PendingPointA != null
                            || s.RecordedPaths.Count > 0 || s.ContourStrips.Count > 0)
                            DrawTrackSk(canvas, s);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OnRender] Draw error (non-fatal): {ex.Message}");
                    }

                    // Vehicle/tool always draws even if above fails
                    if (s.ExtraGuidelines && s.ActiveTrack != null && s.ActiveTrack.Points.Count >= 2)
                        DrawExtraGuidelinesSk(canvas, s);
                    if (s.ShowVehicle && s.ToolWidth > 0.1)
                        DrawToolSk(canvas, s);
                    if (s.ShowVehicle)
                        DrawVehicleSk(canvas, s);
                    if (s.ShowVehicle && s.SvennArrowVisible)
                        DrawSvennArrowSk(canvas, s);
                    if (s.GuidanceActive && s.ShowVehicle)
                        DrawGuidanceLookAheadSk(canvas, s);
                    if (s.SelectionMarkers != null && s.SelectionMarkers.Count > 0)
                        DrawSelectionMarkersSk(canvas, s);
                    if (s.Flags.Count > 0)
                        DrawFlagsSk(canvas, s);
                    if (s.ShowBoundaryOffsetIndicator)
                        DrawBoundaryOffsetIndicatorSk(canvas, s);
                }

            }
            finally { }

            // Draw HUD elements in screen space (outside camera transform)
            if (s.HeadlandDistanceVisible && s.HasHeadland && s.HeadlandProximityDistance < double.MaxValue)
            {
                DrawHeadlandProximityHud(drawingContext, s);
            }

        }

        private static Matrix GetCameraTransform(MapRenderState s, double viewWidth, double viewHeight)
        {
            double scaleX = s.BoundsWidth / viewWidth;
            double scaleY = -s.BoundsHeight / viewHeight;

            if (s.Is3DMode && s.CameraPitch > 0.01)
            {
                double pitchFactor = Math.Cos(s.CameraPitch);
                scaleY *= Math.Max(0.3, pitchFactor);
            }

            var matrix = Matrix.Identity;
            matrix = matrix * Matrix.CreateTranslation(s.BoundsWidth / 2, s.BoundsHeight / 2);
            matrix = Matrix.CreateScale(scaleX, scaleY) * matrix;

            if (Math.Abs(s.Rotation) > 0.001)
            {
                matrix = Matrix.CreateRotation(-s.Rotation) * matrix;
            }

            matrix = Matrix.CreateTranslation(-s.CameraX, -s.CameraY) * matrix;
            return matrix;
        }

        private void DrawGroundTexture(ImmediateDrawingContext dc, MapRenderState s, double viewWidth, double viewHeight)
        {
            const double TILE_SIZE = 100.0;
            double centerX = s.CameraX;
            double centerY = s.CameraY;
            double diagonal = Math.Sqrt(viewWidth * viewWidth + viewHeight * viewHeight) / 2 + TILE_SIZE;

            int startTileX = (int)Math.Floor((centerX - diagonal) / TILE_SIZE);
            int endTileX = (int)Math.Ceiling((centerX + diagonal) / TILE_SIZE);
            int startTileY = (int)Math.Floor((centerY - diagonal) / TILE_SIZE);
            int endTileY = (int)Math.Ceiling((centerY + diagonal) / TILE_SIZE);

            int maxTiles = 50;
            if (endTileX - startTileX > maxTiles || endTileY - startTileY > maxTiles)
            {
                var viewRect = new Rect(centerX - diagonal, -(centerY + diagonal), diagonal * 2, diagonal * 2);
                dc.DrawBitmap(s.GroundTexture!, viewRect);
                return;
            }

            for (int tx = startTileX; tx < endTileX; tx++)
            {
                for (int ty = startTileY; ty < endTileY; ty++)
                {
                    double worldX = tx * TILE_SIZE;
                    double worldY = ty * TILE_SIZE;
                    dc.DrawBitmap(s.GroundTexture!, new Rect(worldX, worldY, TILE_SIZE, TILE_SIZE));
                }
            }
        }

        private void DrawBackgroundImage(ImmediateDrawingContext dc, MapRenderState s)
        {
            if (s.BackgroundImage == null) return;

            double width = s.BgMaxX - s.BgMinX;
            double height = s.BgMaxY - s.BgMinY;
            double centerX = (s.BgMinX + s.BgMaxX) / 2;
            double centerY = (s.BgMinY + s.BgMaxY) / 2;

            var flipTransform = Matrix.CreateTranslation(-centerX, -centerY) *
                               Matrix.CreateScale(1, -1) *
                               Matrix.CreateTranslation(centerX, centerY);

            using (dc.PushPreTransform(flipTransform))
            {
                var destRect = new Rect(s.BgMinX, s.BgMinY, width, height);
                dc.DrawBitmap(s.BackgroundImage, destRect);
            }
        }

        private void DrawGrid(ImmediateDrawingContext dc, MapRenderState s, double viewWidth, double viewHeight)
        {
            double gridSize = 2000.0;
            double toolW = s.ToolWidth > 0.5 ? s.ToolWidth : 6.0;
            double viewSpan = Math.Max(viewWidth, viewHeight);

            double spacing, majorEvery;
            if (viewSpan < toolW * 30) { spacing = toolW; majorEvery = toolW * 10; }
            else if (viewSpan < toolW * 100) { spacing = toolW * 5; majorEvery = toolW * 50; }
            else { spacing = toolW * 10; majorEvery = toolW * 100; }

            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;
            double minorThickness = Math.Max(0.3 * worldPerPixel, 0.05);
            double majorThickness = Math.Max(0.6 * worldPerPixel, 0.1);

            // Create grid pens based on day/night mode
            IImmutableBrush minorBrush, majorBrush;
            if (s.IsDayMode)
            {
                minorBrush = new ImmutableSolidColorBrush(Color.FromArgb(120, 40, 40, 40));
                majorBrush = new ImmutableSolidColorBrush(Color.FromArgb(180, 30, 30, 30));
            }
            else
            {
                minorBrush = new ImmutableSolidColorBrush(Color.FromArgb(80, 180, 180, 180));
                majorBrush = new ImmutableSolidColorBrush(Color.FromArgb(120, 200, 200, 200));
            }
            var gridPenMinor = new ImmutablePen(minorBrush, minorThickness);
            var gridPenMajor = new ImmutablePen(majorBrush, majorThickness);

            double minX = Math.Max(s.CameraX - viewWidth, -gridSize);
            double maxX = Math.Min(s.CameraX + viewWidth, gridSize);
            double minY = Math.Max(s.CameraY - viewHeight, -gridSize);
            double maxY = Math.Min(s.CameraY + viewHeight, gridSize);

            double startX = Math.Floor(minX / spacing) * spacing;
            double startY = Math.Floor(minY / spacing) * spacing;

            for (double x = startX; x <= maxX; x += spacing)
            {
                if (x < -gridSize || x > gridSize) continue;
                bool isMajor = Math.Abs(x % majorEvery) < 0.1;
                bool isAxis = Math.Abs(x) < 0.1;
                var pen = isAxis ? _gridPenAxisYImm : (isMajor ? gridPenMajor : gridPenMinor);
                dc.DrawLine(pen, new Point(x, Math.Max(minY, -gridSize)), new Point(x, Math.Min(maxY, gridSize)));
            }

            for (double y = startY; y <= maxY; y += spacing)
            {
                if (y < -gridSize || y > gridSize) continue;
                bool isMajor = Math.Abs(y % majorEvery) < 0.1;
                bool isAxis = Math.Abs(y) < 0.1;
                var pen = isAxis ? _gridPenAxisXImm : (isMajor ? gridPenMajor : gridPenMinor);
                dc.DrawLine(pen, new Point(Math.Max(minX, -gridSize), y), new Point(Math.Min(maxX, gridSize), y));
            }
        }

        private void DrawCoverageBitmap(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            if (s.CoverageSkBitmap == null || s.BitmapWidth == 0 || s.BitmapHeight == 0)
                return;
            if (canvas == null) return;

            double worldWidth = s.BitmapMaxE - s.BitmapMinE;
            double worldHeight = s.BitmapMaxN - s.BitmapMinN;

            var src = new SKRect(0, 0, s.CoverageSkBitmap.Width, s.CoverageSkBitmap.Height);
            var dst = new SKRect(
                (float)s.BitmapMinE, (float)s.BitmapMinN,
                (float)(s.BitmapMinE + worldWidth), (float)(s.BitmapMinN + worldHeight));

            using var paint = new SKPaint
            {
                FilterQuality = s.Zoom < 0.5 ? SKFilterQuality.Low : SKFilterQuality.High
            };

            // Draw SKBitmap directly — no SKImage.FromBitmap copy needed.
            // Pipeline writes pixels on bg thread, we read on render thread.
            // Atomic 4-byte pixel reads mean worst case is a partially-updated frame.
            canvas.DrawBitmap(s.CoverageSkBitmap, src, dst, paint);
        }

        private void DrawBoundary(SKCanvas canvas, MapRenderState s)
        {
            if (s.Boundary == null) return;

            // Draw outer boundary
            if (s.Boundary.OuterBoundary != null && s.Boundary.OuterBoundary.IsValid
                && s.Boundary.OuterBoundary.Points.Count > 1)
            {
                var points = s.Boundary.OuterBoundary.Points;
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < points.Count; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                path.Close();
                canvas.DrawPath(path, _boundaryOuterPaint);
            }

            // Draw inner boundaries
            foreach (var inner in s.Boundary.InnerBoundaries)
            {
                if (inner.IsValid && inner.Points.Count > 1)
                {
                    var points = inner.Points;
                    using var path = new SKPath();
                    path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                    for (int i = 1; i < points.Count; i++)
                        path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                    path.Close();
                    canvas.DrawPath(path, _boundaryInnerPaint);
                }
            }

            // Draw headland polygon
            if (s.Boundary.HeadlandPolygon != null && s.Boundary.HeadlandPolygon.IsValid
                && s.Boundary.HeadlandPolygon.Points.Count > 1)
            {
                var points = s.Boundary.HeadlandPolygon.Points;
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < points.Count; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                path.Close();
                canvas.DrawPath(path, _boundaryInnerPaint);
            }
        }

        private void DrawHeadlandLine(SKCanvas canvas, MapRenderState s)
        {
            if (s.HeadlandLine == null || s.HeadlandLine.Count < 3) return;
            using var path = new SKPath();
            path.MoveTo((float)s.HeadlandLine[0].Easting, (float)s.HeadlandLine[0].Northing);
            for (int i = 1; i < s.HeadlandLine.Count; i++)
                path.LineTo((float)s.HeadlandLine[i].Easting, (float)s.HeadlandLine[i].Northing);
            path.Close();
            canvas.DrawPath(path, _headlandPaint);
        }

        private void DrawHeadlandPreview(SKCanvas canvas, MapRenderState s)
        {
            if (s.HeadlandPreview == null || s.HeadlandPreview.Count < 3) return;
            using var path = new SKPath();
            path.MoveTo((float)s.HeadlandPreview[0].Easting, (float)s.HeadlandPreview[0].Northing);
            for (int i = 1; i < s.HeadlandPreview.Count; i++)
                path.LineTo((float)s.HeadlandPreview[i].Easting, (float)s.HeadlandPreview[i].Northing);
            path.Close();
            canvas.DrawPath(path, _headlandPreviewPaint);
        }

        private void DrawRecordingPoints(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            if (s.RecordingPoints == null || s.RecordingPoints.Count == 0) return;

            // Draw line strip via SkiaSharp
            if (canvas != null && s.RecordingPoints.Count > 1)
            {
                using var path = new SKPath();
                path.MoveTo((float)s.RecordingPoints[0].Easting, (float)s.RecordingPoints[0].Northing);
                for (int i = 1; i < s.RecordingPoints.Count; i++)
                    path.LineTo((float)s.RecordingPoints[i].Easting, (float)s.RecordingPoints[i].Northing);
                canvas.DrawPath(path, _recordingLinePaint);
            }

            // Draw point markers
            foreach (var point in s.RecordingPoints)
            {
                dc.DrawEllipse(_recordingPointBrushImm, null, new Point(point.Easting, point.Northing), 0.75, 0.75);
            }
        }

        private void DrawSelectionMarkers(ImmediateDrawingContext dc, MapRenderState s)
        {
            if (s.SelectionMarkers == null || s.SelectionMarkers.Count == 0) return;
            double markerRadius = 4.0;
            var orangeBrush = new ImmutableSolidColorBrush(Color.FromRgb(255, 165, 0));
            var blueBrush = new ImmutableSolidColorBrush(Color.FromRgb(0, 150, 255));
            var outlinePen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), 2);

            for (int i = 0; i < s.SelectionMarkers.Count; i++)
            {
                var marker = s.SelectionMarkers[i];
                var brush = i == 0 ? (IImmutableBrush)orangeBrush : blueBrush;
                dc.DrawEllipse(brush, outlinePen, new Point(marker.Easting, marker.Northing), markerRadius, markerRadius);
            }
        }

        private void DrawClipLine(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            // Curved clip path
            if (s.ClipPath != null && s.ClipPath.Count >= 2)
            {
                for (int i = 0; i < s.ClipPath.Count - 1; i++)
                {
                    dc.DrawLine(_clipLinePenImm,
                        new Point(s.ClipPath[i].Easting, s.ClipPath[i].Northing),
                        new Point(s.ClipPath[i + 1].Easting, s.ClipPath[i + 1].Northing));
                }
                return;
            }

            // Straight clip line
            if (!s.ClipLine.HasValue) return;
            dc.DrawLine(_clipLinePenImm,
                new Point(s.ClipLine.Value.Start.Easting, s.ClipLine.Value.Start.Northing),
                new Point(s.ClipLine.Value.End.Easting, s.ClipLine.Value.End.Northing));
        }

        private void DrawExtraGuidelines(ImmediateDrawingContext dc, MapRenderState s)
        {
            if (s.ActiveTrack == null || s.ActiveTrack.Points.Count < 2) return;
            int count = s.ExtraGuidelinesCount;
            double spacing = s.ToolWidth > 0.1 ? s.ToolWidth : 6.0;
            double viewHeight = 200.0 / s.Zoom;
            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;
            double lineThickness = 1 * worldPerPixel;

            var guidelinePen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(60, 255, 165, 0)), lineThickness);

            var track = s.ActiveTrack;
            if (track.Points.Count == 2)
            {
                var pA = track.Points[0];
                var pB = track.Points[^1];
                double dx = pB.Easting - pA.Easting;
                double dy = pB.Northing - pA.Northing;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.01) return;
                double px = -dy / length, py = dx / length;
                double nx = dx / length, ny = dy / length;
                double ext = 500.0;

                for (int i = 1; i <= count; i++)
                {
                    double offset = i * spacing;
                    dc.DrawLine(guidelinePen,
                        new Point(pA.Easting + px * offset - nx * ext, pA.Northing + py * offset - ny * ext),
                        new Point(pB.Easting + px * offset + nx * ext, pB.Northing + py * offset + ny * ext));
                    dc.DrawLine(guidelinePen,
                        new Point(pA.Easting - px * offset - nx * ext, pA.Northing - py * offset - ny * ext),
                        new Point(pB.Easting - px * offset + nx * ext, pB.Northing - py * offset + ny * ext));
                }
            }
            else
            {
                for (int i = 1; i <= count; i++)
                {
                    double offset = i * spacing;
                    for (int j = 0; j < track.Points.Count - 1; j++)
                    {
                        var p1 = track.Points[j];
                        var p2 = track.Points[j + 1];
                        double dx = p2.Easting - p1.Easting;
                        double dy = p2.Northing - p1.Northing;
                        double segLen = Math.Sqrt(dx * dx + dy * dy);
                        if (segLen < 0.001) continue;
                        double px = -dy / segLen, py = dx / segLen;
                        dc.DrawLine(guidelinePen,
                            new Point(p1.Easting + px * offset, p1.Northing + py * offset),
                            new Point(p2.Easting + px * offset, p2.Northing + py * offset));
                        dc.DrawLine(guidelinePen,
                            new Point(p1.Easting - px * offset, p1.Northing - py * offset),
                            new Point(p2.Easting - px * offset, p2.Northing - py * offset));
                    }
                }
            }
        }

        private void DrawTrack(ImmediateDrawingContext dc, MapRenderState s)
        {
            double viewHeight = 200.0 / s.Zoom;
            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;
            double pointRadius = 4 * worldPerPixel;
            double lineThickness = 2 * worldPerPixel;

            var trackBrush = new ImmutableSolidColorBrush(Color.FromRgb(242, 179, 128));
            var trackPenSolid = new ImmutablePen(trackBrush, lineThickness);
            var trackExtendPen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(128, 242, 179, 128)), lineThickness * 0.5);
            var pointOutlinePen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), lineThickness * 0.5);

            var nextLinePen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromRgb(255, 191, 89)), lineThickness);
            var recordedPathPen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(200, 250, 235, 117)), lineThickness * 0.75);
            var contourStripPen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(200, 250, 51, 250)), lineThickness * 0.75);

            var startBrush = new ImmutableSolidColorBrush(Color.FromRgb(0, 220, 0));
            var endBrush = new ImmutableSolidColorBrush(Color.FromRgb(220, 0, 0));
            double markerRadius = pointRadius * 1.5;

            // Recorded paths
            foreach (var path in s.RecordedPaths)
            {
                if (path.IsVisible && path.Points.Count >= 2)
                {
                    for (int i = 0; i < path.Points.Count - 1; i++)
                    {
                        dc.DrawLine(recordedPathPen,
                            new Point(path.Points[i].Easting, path.Points[i].Northing),
                            new Point(path.Points[i + 1].Easting, path.Points[i + 1].Northing));
                    }
                    var startPt = path.Points[0];
                    var endPt = path.Points[^1];
                    dc.DrawEllipse(startBrush, pointOutlinePen,
                        new Point(startPt.Easting, startPt.Northing), markerRadius, markerRadius);
                    dc.DrawEllipse(endBrush, pointOutlinePen,
                        new Point(endPt.Easting, endPt.Northing), markerRadius, markerRadius);
                }
            }

            // Contour strips
            foreach (var strip in s.ContourStrips)
            {
                if (strip.IsVisible && strip.Points.Count >= 2)
                {
                    for (int i = 0; i < strip.Points.Count - 1; i++)
                    {
                        dc.DrawLine(contourStripPen,
                            new Point(strip.Points[i].Easting, strip.Points[i].Northing),
                            new Point(strip.Points[i + 1].Easting, strip.Points[i + 1].Northing));
                    }
                }
            }

            // Pending Point A
            if (s.PendingPointA != null)
            {
                dc.DrawEllipse(_pointABrushImm, pointOutlinePen,
                    new Point(s.PendingPointA.Easting, s.PendingPointA.Northing), pointRadius, pointRadius);
            }

            // Next track (during U-turn)
            if (s.IsInYouTurn && s.NextTrack != null)
            {
                DrawSingleTrack(dc, s.NextTrack, nextLinePen, trackExtendPen, pointOutlinePen, pointRadius, false);
            }

            // Base track
            if (s.BaseTrack != null && s.ActiveTrack != null && s.BaseTrack != s.ActiveTrack)
            {
                var basePen = new ImmutablePen(
                    new ImmutableSolidColorBrush(Color.FromArgb(180, 252, 252, 0)), lineThickness * 0.75);
                DrawSingleTrack(dc, s.BaseTrack, basePen, basePen, pointOutlinePen, pointRadius, true);
            }

            // Active track with pass area
            if (s.ActiveTrack != null)
            {
                double toolWidth = s.ToolWidth > 0 ? s.ToolWidth : 6.0;
                var passPen = new ImmutablePen(
                    new ImmutableSolidColorBrush(Color.FromArgb(30, 200, 200, 50)), toolWidth);

                if (s.ActiveTrack.Points.Count == 2)
                {
                    var a = s.ActiveTrack.Points[0];
                    var b = s.ActiveTrack.Points[^1];
                    double dx = b.Easting - a.Easting;
                    double dy = b.Northing - a.Northing;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0.01)
                    {
                        double nx = dx / len, ny = dy / len;
                        dc.DrawLine(passPen,
                            new Point(a.Easting - nx * 2000, a.Northing - ny * 2000),
                            new Point(b.Easting + nx * 2000, b.Northing + ny * 2000));
                    }
                }
                else
                {
                    for (int i = 0; i < s.ActiveTrack.Points.Count - 1; i++)
                    {
                        dc.DrawLine(passPen,
                            new Point(s.ActiveTrack.Points[i].Easting, s.ActiveTrack.Points[i].Northing),
                            new Point(s.ActiveTrack.Points[i + 1].Easting, s.ActiveTrack.Points[i + 1].Northing));
                    }
                }

                DrawSingleTrack(dc, s.ActiveTrack, trackPenSolid, trackExtendPen, pointOutlinePen, pointRadius, true);
            }
        }

        private static void DrawSingleTrack(ImmediateDrawingContext dc,
            AgValoniaGPS.Models.Track.Track track,
            ImmutablePen mainPen, ImmutablePen extendPen, ImmutablePen pointOutlinePen,
            double pointRadius, bool lineOnly)
        {
            if (track.Points.Count < 2) return;

            var pointA = new Point(track.Points[0].Easting, track.Points[0].Northing);
            var pointB = new Point(track.Points[^1].Easting, track.Points[^1].Northing);

            if (track.Points.Count == 2)
            {
                double dx = pointB.X - pointA.X;
                double dy = pointB.Y - pointA.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length > 0.01)
                {
                    double nx = dx / length, ny = dy / length;
                    double ext = 2000.0;
                    var extA = new Point(pointA.X - nx * ext, pointA.Y - ny * ext);
                    var extB = new Point(pointB.X + nx * ext, pointB.Y + ny * ext);

                    if (lineOnly)
                    {
                        dc.DrawLine(mainPen, extA, extB);
                    }
                    else
                    {
                        dc.DrawLine(extendPen, extA, extB);
                        dc.DrawLine(mainPen, pointA, pointB);
                    }
                }
            }
            else
            {
                for (int i = 0; i < track.Points.Count - 1; i++)
                {
                    dc.DrawLine(mainPen,
                        new Point(track.Points[i].Easting, track.Points[i].Northing),
                        new Point(track.Points[i + 1].Easting, track.Points[i + 1].Northing));
                }
            }

            if (!lineOnly)
            {
                dc.DrawEllipse(_pointABrushImm, pointOutlinePen, pointA, pointRadius, pointRadius);
                dc.DrawEllipse(_pointBBrushImm, pointOutlinePen, pointB, pointRadius, pointRadius);
            }
        }

        private void DrawYouTurnPath(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            if (s.YouTurnPath == null || s.YouTurnPath.Count < 2) return;

            // Draw path via SkiaSharp
            if (canvas != null)
            {
                using var path = new SKPath();
                path.MoveTo((float)s.YouTurnPath[0].Easting, (float)s.YouTurnPath[0].Northing);
                for (int i = 1; i < s.YouTurnPath.Count; i++)
                    path.LineTo((float)s.YouTurnPath[i].Easting, (float)s.YouTurnPath[i].Northing);
                canvas.DrawPath(path, _youTurnPaint);
            }

            // Start/end markers
            double markerSize = 2.0;
            double halfMarker = markerSize / 2.0;
            var startBrush = new ImmutableSolidColorBrush(Color.FromRgb(0, 200, 0));
            var endBrush = new ImmutableSolidColorBrush(Color.FromRgb(200, 0, 0));

            dc.FillRectangle(startBrush, new Rect(
                s.YouTurnPath[0].Easting - halfMarker,
                s.YouTurnPath[0].Northing - halfMarker,
                markerSize, markerSize));

            var endPt = s.YouTurnPath[^1];
            dc.FillRectangle(endBrush, new Rect(
                endPt.Easting - halfMarker,
                endPt.Northing - halfMarker,
                markerSize, markerSize));
        }

        private void DrawTool(ImmediateDrawingContext dc, MapRenderState s)
        {
            if (s.ToolWidth < 0.1) return;
            double toolDepth = 2.0;

            // Hitch bar
            double barEndX = s.HitchX + Math.Sin(s.VehicleHeading) * s.HitchLength;
            double barEndY = s.HitchY + Math.Cos(s.VehicleHeading) * s.HitchLength;
            var rearPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Black), 0.3);
            dc.DrawLine(rearPen, new Point(barEndX, barEndY), new Point(s.HitchX, s.HitchY));

            // V-shape hitch
            double hitchHalfW = s.ToolWidth / 2.0;
            double cosH = Math.Cos(-s.ToolHeading);
            double sinH = Math.Sin(-s.ToolHeading);
            var leftEnd = new Point(s.ToolX + (-hitchHalfW) * cosH, s.ToolY + (-hitchHalfW) * sinH);
            var rightEnd = new Point(s.ToolX + hitchHalfW * cosH, s.ToolY + hitchHalfW * sinH);
            var apex = new Point(s.HitchX, s.HitchY);
            dc.DrawLine(_hitchPenImm, apex, leftEnd);
            dc.DrawLine(_hitchPenImm, apex, rightEnd);

            // Sections
            using (dc.PushPreTransform(Matrix.CreateTranslation(s.ToolX, s.ToolY)))
            using (dc.PushPreTransform(Matrix.CreateRotation(-s.ToolHeading)))
            {
                if (s.NumSections > 0)
                {
                    double sectionGap = 0.05;
                    for (int i = 0; i < s.NumSections; i++)
                    {
                        double left = s.SectionLeft[i] + sectionGap / 2;
                        double right = s.SectionRight[i] - sectionGap / 2;
                        double width = right - left;
                        if (width < 0.01) continue;

                        IImmutableBrush brush = s.SectionButtonState[i] switch
                        {
                            0 => _sectionOffBrushImm,
                            2 => _sectionManualOnBrushImm,
                            _ => _sectionAutoOnBrushImm
                        };
                        dc.DrawRectangle(brush, _sectionOutlinePenImm,
                            new Rect(left, -toolDepth / 2, width, toolDepth), 0, 0, default);
                    }
                }
                else
                {
                    double halfWidth = s.ToolWidth / 2.0;
                    var toolBrush = new ImmutableSolidColorBrush(Color.FromArgb(191, 0, 242, 0));
                    var toolPen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(247, 247, 0)), 0.1);
                    dc.DrawRectangle(toolBrush, toolPen,
                        new Rect(-halfWidth, -toolDepth / 2, s.ToolWidth, toolDepth), 0, 0, default);
                }

                // Center marker
                var centerPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), 0.1);
                dc.DrawLine(centerPen, new Point(0, -toolDepth / 2), new Point(0, toolDepth / 2));
            }
        }

        private void DrawVehicle(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            double size = 5.0;

            using (dc.PushPreTransform(Matrix.CreateTranslation(s.VehicleX, s.VehicleY)))
            using (dc.PushPreTransform(Matrix.CreateRotation(-s.VehicleHeading)))
            {
                if (s.VehicleImage is Bitmap vehicleBitmap)
                {
                    using (dc.PushPreTransform(Matrix.CreateScale(1, -1)))
                    {
                        dc.DrawBitmap(vehicleBitmap, new Rect(-size / 2, -size / 2, size, size));
                    }
                }
                else
                {
                    // Fallback triangle using ImmediateDrawingContext (DrawLine)
                    var vehiclePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0, 200, 0)), 0.3);
                    var vehicleBrush = new ImmutableSolidColorBrush(Color.FromArgb(180, 0, 200, 0));
                    // Triangle: tip at top (0, size/2), base at (-size/3, -size/2) and (size/3, -size/2)
                    var p1 = new Point(0, size / 2);
                    var p2 = new Point(-size / 3, -size / 2);
                    var p3 = new Point(size / 3, -size / 2);
                    dc.DrawLine(vehiclePen, p1, p2);
                    dc.DrawLine(vehiclePen, p2, p3);
                    dc.DrawLine(vehiclePen, p3, p1);
                    // Fill center with ellipse approximation
                    dc.DrawEllipse(vehicleBrush, null, new Point(0, -size / 6), size / 3, size / 3);
                }

                // Heading unknown indicator — draw "?" using dc (skip if no canvas)
                if (!s.HasValidHeading)
                {
                    var questionPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Red), 0.3);
                    dc.DrawLine(questionPen, new Point(size / 2 + 1, size / 2), new Point(size / 2 + 1, -size / 4));
                    dc.DrawEllipse(new ImmutableSolidColorBrush(Colors.Red), null,
                        new Point(size / 2 + 1, -size / 2), 0.2, 0.2);
                }

                // Reverse indicator
                if (s.IsReversing)
                {
                    double arrowSize = 2.0;
                    var arrowBrush = new ImmutableSolidColorBrush(Color.FromArgb(200, 255, 220, 0));
                    dc.FillRectangle(arrowBrush, new Rect(-arrowSize * 0.7, -arrowSize * 2.5, arrowSize * 1.4, arrowSize * 1.3));
                }

                // Antenna position
                var antennaBrush = new ImmutableSolidColorBrush(Color.FromRgb(40, 120, 255));
                dc.DrawEllipse(antennaBrush, null, new Point(s.AntennaOffset, s.AntennaPivot), 0.25, 0.25);
            }
        }

        private void DrawSvennArrow(ImmediateDrawingContext dc, MapRenderState s)
        {
            double aheadDistance = 8.0;
            double wingSpan = 3.0;
            double wingDepth = 3.0;

            using (dc.PushPreTransform(Matrix.CreateTranslation(s.VehicleX, s.VehicleY)))
            using (dc.PushPreTransform(Matrix.CreateRotation(-s.VehicleHeading)))
            {
                var tip = new Point(0, aheadDistance);
                var leftWing = new Point(-wingSpan, aheadDistance - wingDepth);
                var rightWing = new Point(wingSpan, aheadDistance - wingDepth);
                dc.DrawLine(_svennArrowPenImm, tip, leftWing);
                dc.DrawLine(_svennArrowPenImm, tip, rightWing);
            }
        }

        private void DrawFlags(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            double viewHeight = 200.0 / s.Zoom;
            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;
            double flagRadius = 10 * worldPerPixel;
            double poleHeight = 28 * worldPerPixel;
            double poleWidth = 2 * worldPerPixel;

            var polePen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), poleWidth);

            for (int i = 0; i < s.Flags.Count; i++)
            {
                var flag = s.Flags[i];
                var center = new Point(flag.Easting, flag.Northing);

                IImmutableBrush fillBrush = flag.Color switch
                {
                    "Red" => new ImmutableSolidColorBrush(Colors.Red),
                    "Green" => new ImmutableSolidColorBrush(Color.FromRgb(0, 204, 0)),
                    "Yellow" => new ImmutableSolidColorBrush(Color.FromRgb(255, 204, 0)),
                    "Blue" => new ImmutableSolidColorBrush(Color.FromRgb(32, 128, 224)),
                    "Orange" => new ImmutableSolidColorBrush(Color.FromRgb(255, 136, 0)),
                    "Purple" => new ImmutableSolidColorBrush(Color.FromRgb(153, 51, 204)),
                    "Cyan" => new ImmutableSolidColorBrush(Color.FromRgb(0, 187, 204)),
                    "Pink" => new ImmutableSolidColorBrush(Color.FromRgb(255, 102, 170)),
                    "White" => new ImmutableSolidColorBrush(Colors.White),
                    "Black" => new ImmutableSolidColorBrush(Color.FromRgb(51, 51, 51)),
                    _ => new ImmutableSolidColorBrush(Colors.Red)
                };

                // Counter-rotate to keep flag upright
                using (dc.PushPreTransform(
                    Matrix.CreateTranslation(-center.X, -center.Y) *
                    Matrix.CreateRotation(s.Rotation) *
                    Matrix.CreateTranslation(center.X, center.Y)))
                {
                    // Pole
                    var poleTop = new Point(center.X, center.Y + poleHeight);
                    dc.DrawLine(polePen, center, poleTop);

                    // Flag marker
                    var outlinePen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.White), worldPerPixel * 0.5);
                    dc.DrawEllipse(fillBrush, outlinePen, poleTop, flagRadius, flagRadius);

                    // Flag name via SkiaSharp
                    if (!string.IsNullOrEmpty(flag.Name) && canvas != null)
                    {
                        float fontSize = (float)(16 * worldPerPixel);
                        using var font = new SKFont(SKTypeface.Default, fontSize);
                        using var paint = new SKPaint(font) { Color = SKColors.White };
                        canvas.Save();
                        canvas.Scale(1, -1, (float)poleTop.X, (float)poleTop.Y);
                        canvas.DrawText(flag.Name,
                            (float)(poleTop.X + flagRadius + worldPerPixel * 2),
                            (float)poleTop.Y + fontSize / 2,
                            font, paint);
                        canvas.Restore();
                    }
                }
            }
        }

        private void DrawGuidanceLookAhead(ImmediateDrawingContext dc, MapRenderState s)
        {
            double viewHeight = 200.0 / s.Zoom;
            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;

            var vehiclePos = new Point(s.VehicleX, s.VehicleY);
            var goalPos = new Point(s.GoalEasting, s.GoalNorthing);

            var linePen = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(160, 0, 200, 255)), 1.0 * worldPerPixel);
            dc.DrawLine(linePen, vehiclePos, goalPos);

            var goalBrush = new ImmutableSolidColorBrush(Color.FromArgb(200, 0, 200, 255));
            double dotRadius = 3 * worldPerPixel;
            dc.DrawEllipse(goalBrush, null, goalPos, dotRadius, dotRadius);
        }

        private void DrawBoundaryOffsetIndicator(ImmediateDrawingContext dc, SKCanvas? canvas, MapRenderState s)
        {
            double refX = s.VehicleX;
            double refY = s.VehicleY;
            double markerSize = 1.0;
            var cyanBrush = new ImmutableSolidColorBrush(Color.FromRgb(0, 204, 204));
            dc.FillRectangle(cyanBrush,
                new Rect(refX - markerSize / 2, refY - markerSize / 2, markerSize, markerSize));

            if (Math.Abs(s.BoundaryOffsetMeters) > 0.01)
            {
                double perpAngle = s.VehicleHeading + Math.PI / 2.0;
                double offsetX = refX + s.BoundaryOffsetMeters * Math.Sin(perpAngle);
                double offsetY = refY + s.BoundaryOffsetMeters * Math.Cos(perpAngle);

                var yellowPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Yellow), 0.5);
                dc.DrawLine(yellowPen, new Point(refX, refY), new Point(offsetX, offsetY));
            }
        }

        private void DrawHeadlandProximityHud(ImmediateDrawingContext dc, MapRenderState s)
        {
            double distance = s.HeadlandProximityDistance;

            string text = s.IsMetric
                ? $"{distance:F1} m"
                : $"{(distance * 39.3700787):F0} in";

            bool warning = s.HeadlandProximityWarning;
            float fontSize = (float)Math.Clamp(s.BoundsHeight / 20.0, 14, 36);

            // Acquire SKCanvas lease for HUD drawing (screen space, no camera transform)
            var skiaFeature = dc.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (skiaFeature == null) return;
            using var skiaLease = skiaFeature.Lease();
            var canvas = skiaLease.SkCanvas;

            // Measure text
            using var font = new SKFont(SKTypeface.Default, fontSize);
            using var textPaint = new SKPaint(font)
            {
                Color = warning ? new SKColor(255, 60, 60) : new SKColor(255, 242, 64),
                IsAntialias = true
            };
            float textWidth = textPaint.MeasureText(text);
            float textHeight = fontSize * 1.2f;
            float x = (float)(s.BoundsWidth - textWidth) / 2;
            float y = 8;

            // Background box
            var bgColor = warning ? new SKColor(80, 0, 0, 180) : new SKColor(40, 40, 0, 180);
            using var bgPaint = new SKPaint { Color = bgColor, Style = SKPaintStyle.Fill };
            var bgRect = new SKRoundRect(new SKRect(x - 12, y - 4, x + textWidth + 12, y + textHeight + 4), 6);
            canvas.DrawRoundRect(bgRect, bgPaint);

            // Text
            canvas.DrawText(text, x, y + fontSize, font, textPaint);
        }


        // ═══════════════════════════════════════════════════════════════
        // SKCanvas-only drawing methods (no ImmediateDrawingContext)
        // Used because dc drawing after SKCanvas lease is unreliable.
        // ═══════════════════════════════════════════════════════════════

        private SKBitmap? _vehicleSkBitmap; // Cached SKBitmap version of vehicle image

        private void DrawVehicleSk(SKCanvas canvas, MapRenderState s)
        {
            float size = 5.0f;
            float vx = (float)s.VehicleX, vy = (float)s.VehicleY;

            canvas.Save();
            canvas.Translate(vx, vy);
            canvas.RotateRadians(-(float)s.VehicleHeading);

            // Try to draw vehicle image, fall back to triangle
            if (_vehicleSkBitmap == null && s.VehicleImage is Avalonia.Media.Imaging.Bitmap avBitmap)
            {
                // Convert Avalonia Bitmap to SKBitmap once
                try
                {
                    using var ms = new System.IO.MemoryStream();
                    avBitmap.Save(ms);
                    ms.Position = 0;
                    _vehicleSkBitmap = SKBitmap.Decode(ms);
                }
                catch { /* Fall back to triangle */ }
            }

            if (_vehicleSkBitmap != null)
            {
                // Y-flip because world coordinates have Y-up but bitmap is Y-down
                canvas.Scale(1, -1);
                var dst = new SKRect(-size / 2, -size / 2, size / 2, size / 2);
                canvas.DrawBitmap(_vehicleSkBitmap, dst);
                canvas.Scale(1, -1); // Restore for antenna dot
            }
            else
            {
                // Fallback triangle
                using var vehiclePaint = new SKPaint { Color = new SKColor(0, 200, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
                using var path = new SKPath();
                path.MoveTo(0, size / 2);
                path.LineTo(-size / 3, -size / 2);
                path.LineTo(size / 3, -size / 2);
                path.Close();
                canvas.DrawPath(path, vehiclePaint);
            }

            // Antenna dot
            using var antennaPaint = new SKPaint { Color = new SKColor(40, 120, 255), Style = SKPaintStyle.Fill };
            canvas.DrawCircle((float)s.AntennaOffset, (float)s.AntennaPivot, 0.25f, antennaPaint);

            canvas.Restore();
        }

        private void DrawToolSk(SKCanvas canvas, MapRenderState s)
        {
            float tx = (float)s.ToolX, ty = (float)s.ToolY;
            float toolDepth = 2.0f;

            // Hitch bar (rear axle to hitch point)
            float barEndX = (float)(s.HitchX + Math.Sin(s.VehicleHeading) * s.HitchLength);
            float barEndY = (float)(s.HitchY + Math.Cos(s.VehicleHeading) * s.HitchLength);
            using var rearPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f };
            canvas.DrawLine(barEndX, barEndY, (float)s.HitchX, (float)s.HitchY, rearPaint);

            // V-shape hitch (hitch point to tool ends)
            float hitchHalfW = (float)(s.ToolWidth / 2.0);
            float cosH = (float)Math.Cos(-s.ToolHeading);
            float sinH = (float)Math.Sin(-s.ToolHeading);
            using var hitchPaint = new SKPaint { Color = new SKColor(255, 255, 0), Style = SKPaintStyle.Stroke, StrokeWidth = 0.15f };
            canvas.DrawLine((float)s.HitchX, (float)s.HitchY,
                tx + (-hitchHalfW) * cosH, ty + (-hitchHalfW) * sinH, hitchPaint);
            canvas.DrawLine((float)s.HitchX, (float)s.HitchY,
                tx + hitchHalfW * cosH, ty + hitchHalfW * sinH, hitchPaint);

            canvas.Save();
            canvas.Translate(tx, ty);
            canvas.RotateRadians(-(float)s.ToolHeading);

            if (s.NumSections > 0)
            {
                // Section colors: 0=Off(red), 2=ManualOn(yellow), default=AutoOn(green)
                float sectionGap = 0.05f;
                for (int i = 0; i < s.NumSections; i++)
                {
                    float left = (float)s.SectionLeft[i] + sectionGap / 2;
                    float right = (float)s.SectionRight[i] - sectionGap / 2;
                    float width = right - left;
                    if (width < 0.01f) continue;

                    SKColor secColor = s.SectionButtonState[i] switch
                    {
                        0 => new SKColor(242, 51, 51),   // Off = red
                        2 => new SKColor(247, 247, 0),    // ManualOn = yellow
                        _ => new SKColor(0, 242, 0)       // AutoOn = green
                    };

                    using var secPaint = new SKPaint { Color = secColor, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(left, -toolDepth / 2, width, toolDepth, secPaint);

                    using var outlinePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f };
                    canvas.DrawRect(left, -toolDepth / 2, width, toolDepth, outlinePaint);
                }
            }
            else
            {
                // No sections — draw single tool bar
                float halfWidth = (float)(s.ToolWidth / 2);
                using var toolPaint = new SKPaint { Color = new SKColor(0, 242, 0, 191), Style = SKPaintStyle.Fill };
                canvas.DrawRect(-halfWidth, -toolDepth / 2, (float)s.ToolWidth, toolDepth, toolPaint);
            }

            // Center line
            using var centerPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f };
            canvas.DrawLine(0, -toolDepth / 2, 0, toolDepth / 2, centerPaint);

            canvas.Restore();
        }

        private void DrawTrackSk(SKCanvas canvas, MapRenderState s)
        {
            // Active track (magenta)
            if (s.ActiveTrack != null && s.ActiveTrack.Points.Count >= 2)
            {
                using var trackPaint = new SKPaint
                {
                    Color = new SKColor(252, 86, 186),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0.5f,
                    IsAntialias = true
                };
                DrawTrackPointsSk(canvas, s.ActiveTrack.Points, trackPaint);
            }

            // Base track (purple)
            if (s.BaseTrack != null && s.BaseTrack.Points.Count >= 2)
            {
                using var basePaint = new SKPaint
                {
                    Color = new SKColor(180, 100, 255),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0.3f,
                    IsAntialias = true
                };
                DrawTrackPointsSk(canvas, s.BaseTrack.Points, basePaint);
            }

            // Next track for U-turn (cyan)
            if (s.IsInYouTurn && s.NextTrack != null && s.NextTrack.Points.Count >= 2)
            {
                using var nextPaint = new SKPaint
                {
                    Color = new SKColor(0, 200, 200),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 0.4f,
                    IsAntialias = true
                };
                DrawTrackPointsSk(canvas, s.NextTrack.Points, nextPaint);
            }

            // Pending point A
            if (s.PendingPointA != null)
            {
                using var pointPaint = new SKPaint { Color = new SKColor(0, 255, 0), Style = SKPaintStyle.Fill };
                canvas.DrawCircle((float)s.PendingPointA.Easting, (float)s.PendingPointA.Northing, 1.0f, pointPaint);
            }
        }

        private static void DrawTrackPointsSk(SKCanvas canvas, IReadOnlyList<AgValoniaGPS.Models.Base.Vec3> points, SKPaint paint)
        {
            if (points.Count < 2) return;
            using var path = new SKPath();
            path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
            for (int i = 1; i < points.Count; i++)
                path.LineTo((float)points[i].Easting, (float)points[i].Northing);
            canvas.DrawPath(path, paint);
        }

        private void DrawGuidanceLookAheadSk(SKCanvas canvas, MapRenderState s)
        {
            using var goalPaint = new SKPaint { Color = new SKColor(255, 100, 100), Style = SKPaintStyle.Fill };
            canvas.DrawCircle((float)s.GoalEasting, (float)s.GoalNorthing, 0.5f, goalPaint);

            using var linePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 0, 150),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.2f
            };
            canvas.DrawLine((float)s.VehicleX, (float)s.VehicleY,
                           (float)s.GoalEasting, (float)s.GoalNorthing, linePaint);
        }

        private void DrawRecordingPointsSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.RecordingPoints == null) return;
            using var linePaint = new SKPaint { Color = new SKColor(0, 255, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, IsAntialias = true };
            using var pointPaint = new SKPaint { Color = new SKColor(255, 128, 0), Style = SKPaintStyle.Fill };
            for (int i = 1; i < s.RecordingPoints.Count; i++)
            {
                var (e1, n1) = s.RecordingPoints[i - 1];
                var (e2, n2) = s.RecordingPoints[i];
                canvas.DrawLine((float)e1, (float)n1, (float)e2, (float)n2, linePaint);
            }
            if (s.RecordingPoints.Count > 0)
            {
                var (e, n) = s.RecordingPoints[s.RecordingPoints.Count - 1];
                canvas.DrawCircle((float)e, (float)n, 0.3f, pointPaint);
            }
        }

        private void DrawClipLineSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.ClipLine.HasValue)
            {
                var (start, end) = s.ClipLine.Value;
                canvas.DrawLine((float)start.Easting, (float)start.Northing,
                    (float)end.Easting, (float)end.Northing, _clipLinePaint);
            }
            if (s.ClipPath != null && s.ClipPath.Count >= 2)
            {
                for (int i = 1; i < s.ClipPath.Count; i++)
                    canvas.DrawLine((float)s.ClipPath[i-1].Easting, (float)s.ClipPath[i-1].Northing,
                        (float)s.ClipPath[i].Easting, (float)s.ClipPath[i].Northing, _clipLinePaint);
            }
        }

        private void DrawYouTurnPathSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.YouTurnPath == null || s.YouTurnPath.Count < 2) return;
            using var path = new SKPath();
            path.MoveTo((float)s.YouTurnPath[0].Easting, (float)s.YouTurnPath[0].Northing);
            for (int i = 1; i < s.YouTurnPath.Count; i++)
                path.LineTo((float)s.YouTurnPath[i].Easting, (float)s.YouTurnPath[i].Northing);
            canvas.DrawPath(path, _youTurnPaint);
        }

        /// <summary>
        /// Draw tram lines: two-pass rendering matching legacy AgOpenGPS.
        /// Pass 1: black outline, Pass 2: pink/salmon fill.
        /// </summary>
        private void DrawTramLinesSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.TramDisplayMode == AgValoniaGPS.Models.Configuration.TramDisplayMode.Off) return;

            bool hasBoundary = (s.TramOuterTrack?.Count > 1 || s.TramInnerTrack?.Count > 1);
            bool hasLines = s.TramParallelLines?.Count > 0;
            if (!hasBoundary && !hasLines) return;

            byte alpha = (byte)(s.TramAlpha * 255);

            using var outlinePaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, alpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.0f,
                IsAntialias = true
            };
            using var fillPaint = new SKPaint
            {
                Color = new SKColor(237, 184, 187, alpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.0f,
                IsAntialias = true
            };

            // Mode All or LinesOnly: draw parallel tram lines
            if ((s.TramDisplayMode == AgValoniaGPS.Models.Configuration.TramDisplayMode.All
                || s.TramDisplayMode == AgValoniaGPS.Models.Configuration.TramDisplayMode.LinesOnly) && hasLines)
            {
                foreach (var line in s.TramParallelLines!)
                {
                    if (line.Count < 2) continue;
                    using var path = new SKPath();
                    path.MoveTo((float)line[0].Easting, (float)line[0].Northing);
                    for (int i = 1; i < line.Count; i++)
                        path.LineTo((float)line[i].Easting, (float)line[i].Northing);
                    canvas.DrawPath(path, outlinePaint);
                    canvas.DrawPath(path, fillPaint);
                }
            }

            // Mode All or OuterOnly: draw boundary tracks
            if ((s.TramDisplayMode == AgValoniaGPS.Models.Configuration.TramDisplayMode.All
                || s.TramDisplayMode == AgValoniaGPS.Models.Configuration.TramDisplayMode.OuterOnly) && hasBoundary)
            {
                void DrawTramTrack(IReadOnlyList<AgValoniaGPS.Models.Base.Vec2> track)
                {
                    if (track.Count < 2) return;
                    using var path = new SKPath();
                    path.MoveTo((float)track[0].Easting, (float)track[0].Northing);
                    for (int i = 1; i < track.Count; i++)
                        path.LineTo((float)track[i].Easting, (float)track[i].Northing);
                    canvas.DrawPath(path, outlinePaint);
                    canvas.DrawPath(path, fillPaint);
                }

                if (s.TramOuterTrack != null) DrawTramTrack(s.TramOuterTrack);
                if (s.TramInnerTrack != null) DrawTramTrack(s.TramInnerTrack);
            }
        }

        private void DrawFlagsSk(SKCanvas canvas, MapRenderState s)
        {
            foreach (var (easting, northing, color, name) in s.Flags)
            {
                var skColor = color switch
                {
                    "Red" => SKColors.Red,
                    "Green" => SKColors.Green,
                    "Blue" => SKColors.Blue,
                    "Yellow" => SKColors.Yellow,
                    _ => SKColors.White
                };
                using var paint = new SKPaint { Color = skColor, Style = SKPaintStyle.Fill };
                canvas.DrawCircle((float)easting, (float)northing, 0.8f, paint);
                using var outlinePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f };
                canvas.DrawCircle((float)easting, (float)northing, 0.8f, outlinePaint);
            }
        }

        private void DrawSvennArrowSk(SKCanvas canvas, MapRenderState s)
        {
            float aheadDistance = 8.0f;
            float wingSpan = 3.0f;
            float wingDepth = 3.0f;

            canvas.Save();
            canvas.Translate((float)s.VehicleX, (float)s.VehicleY);
            canvas.RotateRadians(-(float)s.VehicleHeading);

            using var paint = new SKPaint
            {
                Color = new SKColor(255, 220, 0, 200),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.4f,
                IsAntialias = true
            };
            canvas.DrawLine(0, aheadDistance, -wingSpan, aheadDistance - wingDepth, paint);
            canvas.DrawLine(0, aheadDistance, wingSpan, aheadDistance - wingDepth, paint);

            canvas.Restore();
        }

        private void DrawExtraGuidelinesSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.ActiveTrack == null || s.ActiveTrack.Points.Count < 2) return;
            int count = s.ExtraGuidelinesCount;
            double spacing = s.ToolWidth > 0.1 ? s.ToolWidth : 6.0;

            using var paint = new SKPaint
            {
                Color = new SKColor(255, 165, 0, 60),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.3f,
                IsAntialias = true
            };

            var track = s.ActiveTrack;
            if (track.Points.Count == 2)
            {
                var pA = track.Points[0];
                var pB = track.Points[track.Points.Count - 1];
                double dx = pB.Easting - pA.Easting;
                double dy = pB.Northing - pA.Northing;
                double length = Math.Sqrt(dx * dx + dy * dy);
                if (length < 0.01) return;
                double px = -dy / length, py = dx / length;
                double nx = dx / length, ny = dy / length;
                double ext = 500.0;

                for (int i = 1; i <= count; i++)
                {
                    double offset = i * spacing;
                    canvas.DrawLine(
                        (float)(pA.Easting + px * offset - nx * ext), (float)(pA.Northing + py * offset - ny * ext),
                        (float)(pB.Easting + px * offset + nx * ext), (float)(pB.Northing + py * offset + ny * ext), paint);
                    canvas.DrawLine(
                        (float)(pA.Easting - px * offset - nx * ext), (float)(pA.Northing - py * offset - ny * ext),
                        (float)(pB.Easting - px * offset + nx * ext), (float)(pB.Northing - py * offset + ny * ext), paint);
                }
            }
            else
            {
                for (int i = 1; i <= count; i++)
                {
                    double offset = i * spacing;
                    var posPoints = Models.Guidance.CurveProcessing.CreateOffsetCurve(track.Points, offset);
                    var negPoints = Models.Guidance.CurveProcessing.CreateOffsetCurve(track.Points, -offset);
                    if (posPoints.Count >= 2) DrawTrackPointsSk(canvas, posPoints, paint);
                    if (negPoints.Count >= 2) DrawTrackPointsSk(canvas, negPoints, paint);
                }
            }
        }

        private void DrawSelectionMarkersSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.SelectionMarkers == null) return;
            for (int i = 0; i < s.SelectionMarkers.Count; i++)
            {
                var marker = s.SelectionMarkers[i];
                var color = i == 0 ? new SKColor(255, 165, 0) : new SKColor(0, 150, 255);
                using var fillPaint = new SKPaint { Color = color, Style = SKPaintStyle.Fill };
                using var outlinePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f };
                canvas.DrawCircle((float)marker.Easting, (float)marker.Northing, 4.0f, fillPaint);
                canvas.DrawCircle((float)marker.Easting, (float)marker.Northing, 4.0f, outlinePaint);
            }
        }

        private void DrawBoundaryOffsetIndicatorSk(SKCanvas canvas, MapRenderState s)
        {
            if (!s.ShowBoundaryOffsetIndicator) return;
            // Simple indicator at vehicle position
            using var paint = new SKPaint
            {
                Color = new SKColor(255, 165, 0, 180),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.3f
            };
            float radius = (float)Math.Abs(s.BoundaryOffsetMeters);
            if (radius > 0.1f)
                canvas.DrawCircle((float)s.VehicleX, (float)s.VehicleY, radius, paint);
        }
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

// CoverageDrawOperation removed — coverage drawn directly in MapCompositionHandler
