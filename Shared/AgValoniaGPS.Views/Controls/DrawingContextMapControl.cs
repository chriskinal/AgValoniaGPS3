using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AgValoniaGPS.Models;

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
    void SetGridVisible(bool visible);
    void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points);
    void ClearRecordingPoints();
    void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY);
    void ClearBackground();

    // Boundary recording indicator
    void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0);

    // Grid visibility property
    bool IsGridVisible { get; set; }
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

    // Camera/viewport state
    private double _cameraX = 0.0;
    private double _cameraY = 0.0;
    private double _zoom = 1.0;
    private double _rotation = 0.0;
    private double _cameraPitch = 0.0;
    private double _cameraDistance = 100.0;
    private bool _is3DMode = false;

    // Vehicle state
    private double _vehicleX = 0.0;
    private double _vehicleY = 0.0;
    private double _vehicleHeading = 0.0;

    // Mouse interaction
    private bool _isPanning = false;
    private bool _isRotating = false;
    private Point _lastMousePosition;

    // Boundary data
    private Boundary? _boundary;
    private List<(double Easting, double Northing)>? _recordingPoints;
    private bool _showBoundaryOffsetIndicator = false;
    private double _boundaryOffsetMeters = 0.0;

    // Background image (not implemented yet for DrawingContext - would need Bitmap loading)
    private string? _backgroundImagePath;

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

    // Render timer
    private readonly DispatcherTimer _renderTimer;

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
        _recordingPen = new Pen(Brushes.Cyan, 2);
        _vehicleBrush = new SolidColorBrush(Color.FromRgb(0, 200, 0));
        _vehiclePen = new Pen(Brushes.DarkGreen, 2);
        _recordingPointBrush = new SolidColorBrush(Color.FromRgb(255, 128, 0));

        // Render timer for continuous updates (10 FPS for iOS simulator compatibility)
        // Note: iOS simulator has ARM64->x64 translation overhead; increase FPS when testing on real device
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _renderTimer.Tick += (s, e) => InvalidateVisual();
        _renderTimer.Start();

        // Wire up mouse events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
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

            // Draw recording points
            if (_recordingPoints != null && _recordingPoints.Count > 0)
            {
                DrawRecordingPoints(context);
            }

            // Draw vehicle
            DrawVehicle(context);

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

        // Draw line strip
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

        // Draw point markers
        foreach (var point in _recordingPoints)
        {
            context.DrawEllipse(_recordingPointBrush, null, new Point(point.Easting, point.Northing), 1.5, 1.5);
        }
    }

    private void DrawVehicle(DrawingContext context)
    {
        // Draw vehicle as a triangle pointing in heading direction
        // Size in meters (typical tractor ~5m)
        double size = 5.0;

        // Save transform and apply vehicle rotation
        using (context.PushTransform(Matrix.CreateTranslation(_vehicleX, _vehicleY)))
        using (context.PushTransform(Matrix.CreateRotation(-_vehicleHeading + Math.PI / 2))) // Adjust for "north up" convention
        {
            // Triangle pointing up (forward)
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

            var yellowPen = new Pen(Brushes.Yellow, 2);
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

    // Mouse event handlers
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);

        if (point.Properties.IsLeftButtonPressed)
        {
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
        // Background image not implemented yet for DrawingContext
        // Would need to load a Bitmap and render it
        _backgroundImagePath = imagePath;
        Console.WriteLine($"Background image not yet implemented for DrawingContext: {imagePath}");
    }

    public void ClearBackground()
    {
        _backgroundImagePath = null;
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _showBoundaryOffsetIndicator = show;
        _boundaryOffsetMeters = offsetMeters;
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
}
