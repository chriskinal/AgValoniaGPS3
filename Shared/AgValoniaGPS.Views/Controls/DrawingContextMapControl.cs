using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AgValoniaGPS.Models;
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
        Console.WriteLine("[DrawingContextMapControl] Constructor starting...");

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
        _headlandPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 255, 128)), 1.5); // Green headland line
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
        // Increment frame count and trigger render
        _frameCount++;
        InvalidateVisual();

        // Check if it's time to update FPS (every second)
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastFpsUpdate).TotalSeconds;
        if (elapsed >= 1.0)
        {
            _currentFps = _frameCount / elapsed;
            _frameCount = 0;
            _lastFpsUpdate = now;
            // Fire event - we're in timer callback, not Render(), so this is safe
            FpsUpdated?.Invoke(_currentFps);
        }
    }

    public override void Render(DrawingContext context)
    {
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
            Console.WriteLine("[DrawingContextMapControl] Loaded tractor image successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DrawingContextMapControl] Failed to load tractor image: {ex.Message}");
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

        // Calculate heading and extend the line in both directions
        double dx = pointB.X - pointA.X;
        double dy = pointB.Y - pointA.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length > 0.01) // Avoid division by zero
        {
            // Normalize direction
            double nx = dx / length;
            double ny = dy / length;

            // Extend line 500 meters in each direction
            double extendDistance = 500.0;
            var extendA = new Point(pointA.X - nx * extendDistance, pointA.Y - ny * extendDistance);
            var extendB = new Point(pointB.X + nx * extendDistance, pointB.Y + ny * extendDistance);

            // Draw extended line (semi-transparent)
            context.DrawLine(extendPen, extendA, extendB);
        }

        // Draw main track line
        context.DrawLine(mainPen, pointA, pointB);

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
        _zoom = Math.Clamp(_zoom, 0.1, 100.0);
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
            _zoom = Math.Clamp(_zoom, 0.1, 100.0);
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
                Console.WriteLine($"[DrawingContextMapControl] Loaded background image: {imagePath} ({_backgroundImage.PixelSize.Width}x{_backgroundImage.PixelSize.Height})");
                Console.WriteLine($"  Bounds: minX={minX:F1}, maxY={maxY:F1}, maxX={maxX:F1}, minY={minY:F1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DrawingContextMapControl] Failed to load background image: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($"[DrawingContextMapControl] Background image path invalid or not found: {imagePath}");
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
