using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// SkiaSharp-based map control for cross-platform rendering (iOS, Android, Desktop).
/// Implements 2D rendering only - no 3D perspective mode.
/// </summary>
public class SkiaMapControl : Control, IMapControl
{
    // Avalonia property for grid visibility
    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<SkiaMapControl, bool>(
            nameof(IsGridVisible),
            defaultValue: false);

    public bool IsGridVisible
    {
        get => GetValue(IsGridVisibleProperty);
        set => SetValue(IsGridVisibleProperty, value);
    }

    // Camera/viewport properties
    private double _cameraX = 0.0;
    private double _cameraY = 0.0;
    private double _zoom = 1.0;
    private double _rotation = 0.0; // Radians

    // GPS/Vehicle position
    private double _vehicleX = 0.0;      // Meters (world coordinates)
    private double _vehicleY = 0.0;      // Meters (world coordinates)
    private double _vehicleHeading = 0.0; // Radians

    // Mouse interaction state
    private bool _isPanning = false;
    private bool _isRotating = false;
    private Point _lastMousePosition;

    // Boundary data
    private Boundary? _currentBoundary;

    // Recording points
    private List<(double Easting, double Northing)> _recordingPoints = new();

    // Background image
    private SKBitmap? _backgroundBitmap;
    private double _backgroundMinX, _backgroundMaxX, _backgroundMinY, _backgroundMaxY;
    private bool _hasBackgroundImage = false;

    // Vehicle texture
    private SKBitmap? _vehicleBitmap;

    // Boundary offset indicator
    private double _boundaryOffsetMeters = 0.0;
    private bool _showBoundaryOffsetIndicator = false;

    // Paints (reused for efficiency)
    private readonly SKPaint _gridPaint;
    private readonly SKPaint _gridMajorPaint;
    private readonly SKPaint _axisXPaint;
    private readonly SKPaint _axisYPaint;
    private readonly SKPaint _boundaryOuterPaint;
    private readonly SKPaint _boundaryInnerPaint;
    private readonly SKPaint _recordingLinePaint;
    private readonly SKPaint _recordingPointPaint;
    private readonly SKPaint _offsetIndicatorPaint;
    private readonly SKPaint _offsetArrowPaint;

    public SkiaMapControl()
    {
        // Make control focusable and set to accept all pointer events
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        // Initialize paints
        _gridPaint = new SKPaint
        {
            Color = new SKColor(76, 76, 76, 76), // Gray, semi-transparent
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _gridMajorPaint = new SKPaint
        {
            Color = new SKColor(76, 76, 76, 128), // Brighter gray
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _axisXPaint = new SKPaint
        {
            Color = new SKColor(204, 51, 51, 204), // Red
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _axisYPaint = new SKPaint
        {
            Color = new SKColor(51, 204, 51, 204), // Green
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _boundaryOuterPaint = new SKPaint
        {
            Color = SKColors.Yellow,
            StrokeWidth = 3,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _boundaryInnerPaint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 3,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _recordingLinePaint = new SKPaint
        {
            Color = SKColors.Cyan,
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _recordingPointPaint = new SKPaint
        {
            Color = new SKColor(255, 128, 0), // Orange
            StrokeWidth = 4,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _offsetIndicatorPaint = new SKPaint
        {
            Color = new SKColor(0, 204, 204), // Teal
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _offsetArrowPaint = new SKPaint
        {
            Color = new SKColor(255, 230, 0), // Yellow
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        // Start render loop
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        timer.Tick += (s, e) => InvalidateVisual();
        timer.Start();

        // Wire up mouse events for camera control
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    private static bool _loggedRenderPath = false;

    public override void Render(Avalonia.Media.DrawingContext context)
    {
        try
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // Get the rendering scale factor for high-DPI displays
            double scale = 1.0;
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                scale = topLevel.RenderScaling;
            }

            // On iOS/mobile, RenderScaling may return 1.0 even on Retina displays
            // Force 2x scale for crisp rendering on high-DPI mobile screens
            if (_useWriteableBitmapFallback && scale < 1.5)
            {
                scale = 2.0; // Most iOS devices are 2x or 3x Retina
            }

            // Calculate pixel dimensions accounting for DPI scaling
            int pixelWidth = (int)(bounds.Width * scale);
            int pixelHeight = (int)(bounds.Height * scale);

            if (!_loggedRenderPath)
            {
                Console.WriteLine($"[SkiaMapControl] Render: useWriteableBitmapFallback={_useWriteableBitmapFallback}, bounds={bounds.Width}x{bounds.Height}, scale={scale}, pixels={pixelWidth}x{pixelHeight}");
                Console.WriteLine($"[SkiaMapControl] IsIOS={OperatingSystem.IsIOS()}, IsAndroid={OperatingSystem.IsAndroid()}, IsMacOS={OperatingSystem.IsMacOS()}");
                _loggedRenderPath = true;
            }

            if (_useWriteableBitmapFallback)
            {
                // Use WriteableBitmap fallback (for iOS and other platforms without SkiaSharp lease)
                RenderToWriteableBitmap(context, bounds, pixelWidth, pixelHeight, scale);
            }
            else
            {
                // Try to use SkiaSharp custom draw operation first (best quality on Desktop)
                context.Custom(new SkiaMapDrawOperation(this, new Rect(0, 0, bounds.Width, bounds.Height)));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SkiaMapControl] Render error: {ex}");
            // Fallback: draw simple background if custom operation fails
            try
            {
                context.DrawRectangle(Brushes.DarkSlateGray, null, Bounds);
            }
            catch { }
        }
    }

    /// <summary>
    /// Custom draw operation for SkiaSharp rendering
    /// </summary>
    private class SkiaMapDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaMapControl _control;
        private readonly Rect _bounds;

        public SkiaMapDrawOperation(SkiaMapControl control, Rect bounds)
        {
            _control = control;
            _bounds = bounds;
        }

        public Rect Bounds => _bounds;

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) =>
            other is SkiaMapDrawOperation op && op._bounds == _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
            {
                // SkiaSharp lease not available - this shouldn't happen if platform detection is correct
                // Just return and let the WriteableBitmap path handle it
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            _control.RenderToCanvas(canvas, (float)_bounds.Width, (float)_bounds.Height);
        }
    }

    /// <summary>
    /// Render the map to a SkiaSharp canvas
    /// </summary>
    internal void RenderToCanvas(SKCanvas canvas, float width, float height)
    {
        // Use pixel dimensions for both drawing and view calculations
        RenderToCanvasInternal(canvas, width, height, width, height);
    }

    /// <summary>
    /// Render the map to a SkiaSharp canvas with separate pixel and logical dimensions
    /// </summary>
    /// <param name="canvas">The canvas to render to</param>
    /// <param name="pixelWidth">Width in pixels (for drawing)</param>
    /// <param name="pixelHeight">Height in pixels (for drawing)</param>
    /// <param name="logicalWidth">Logical width (for view/zoom calculations)</param>
    /// <param name="logicalHeight">Logical height (for view/zoom calculations)</param>
    internal void RenderToCanvasInternal(SKCanvas canvas, float pixelWidth, float pixelHeight, float logicalWidth, float logicalHeight)
    {
        // Clear background
        canvas.Clear(new SKColor(25, 25, 25)); // Dark gray

        // Calculate view transformation using LOGICAL dimensions for correct aspect/zoom
        float aspect = logicalWidth / logicalHeight;
        float viewWidth = 200.0f * aspect / (float)_zoom;
        float viewHeight = 200.0f / (float)_zoom;

        // Save canvas state
        canvas.Save();

        // Center the canvas using PIXEL dimensions
        canvas.Translate(pixelWidth / 2, pixelHeight / 2);

        // Apply rotation (around center)
        canvas.RotateDegrees((float)(-_rotation * 180.0 / Math.PI));

        // Calculate scale: pixels per meter (using PIXEL dimensions for drawing size)
        float scaleX = pixelWidth / viewWidth;
        float scaleY = pixelHeight / viewHeight;
        canvas.Scale(scaleX, -scaleY); // Flip Y axis (world Y goes up, screen Y goes down)

        // Translate to camera position
        canvas.Translate(-(float)_cameraX, -(float)_cameraY);

        // Draw background image (if any)
        if (_hasBackgroundImage && _backgroundBitmap != null)
        {
            DrawBackgroundImage(canvas);
        }

        // Draw grid
        if (IsGridVisible)
        {
            DrawGrid(canvas, viewWidth, viewHeight);
        }

        // Draw boundary
        if (_currentBoundary != null)
        {
            DrawBoundary(canvas);
        }

        // Draw recording points
        if (_recordingPoints.Count > 0)
        {
            DrawRecordingPoints(canvas);
        }

        // Draw vehicle
        DrawVehicle(canvas);

        // Draw boundary offset indicator
        if (_showBoundaryOffsetIndicator)
        {
            DrawBoundaryOffsetIndicator(canvas);
        }

        // Restore canvas state
        canvas.Restore();
    }

    private void DrawBackgroundImage(SKCanvas canvas)
    {
        if (_backgroundBitmap == null) return;

        var destRect = new SKRect(
            (float)_backgroundMinX,
            (float)_backgroundMinY,
            (float)_backgroundMaxX,
            (float)_backgroundMaxY);

        // Note: Since we flipped the Y axis, we need to handle this correctly
        canvas.Save();
        canvas.Scale(1, -1);
        canvas.Translate(0, -(float)(_backgroundMinY + _backgroundMaxY));

        var adjustedRect = new SKRect(
            (float)_backgroundMinX,
            (float)_backgroundMinY,
            (float)_backgroundMaxX,
            (float)_backgroundMaxY);

        canvas.DrawBitmap(_backgroundBitmap, adjustedRect);
        canvas.Restore();
    }

    private void DrawGrid(SKCanvas canvas, float viewWidth, float viewHeight)
    {
        float gridSize = 500.0f; // 500m x 500m grid
        float spacing = 10.0f;   // 10m spacing

        // Calculate visible range based on camera position
        float minX = (float)_cameraX - viewWidth;
        float maxX = (float)_cameraX + viewWidth;
        float minY = (float)_cameraY - viewHeight;
        float maxY = (float)_cameraY + viewHeight;

        // Clamp to grid bounds
        minX = Math.Max(minX, -gridSize);
        maxX = Math.Min(maxX, gridSize);
        minY = Math.Max(minY, -gridSize);
        maxY = Math.Min(maxY, gridSize);

        // Round to nearest spacing
        float startX = (float)Math.Floor(minX / spacing) * spacing;
        float startY = (float)Math.Floor(minY / spacing) * spacing;

        // Draw vertical lines
        for (float x = startX; x <= maxX; x += spacing)
        {
            var paint = (Math.Abs(x % 50.0f) < 0.1f) ? _gridMajorPaint : _gridPaint;
            canvas.DrawLine(x, Math.Max(minY, -gridSize), x, Math.Min(maxY, gridSize), paint);
        }

        // Draw horizontal lines
        for (float y = startY; y <= maxY; y += spacing)
        {
            var paint = (Math.Abs(y % 50.0f) < 0.1f) ? _gridMajorPaint : _gridPaint;
            canvas.DrawLine(Math.Max(minX, -gridSize), y, Math.Min(maxX, gridSize), y, paint);
        }

        // Draw axis lines
        canvas.DrawLine(-gridSize, 0, gridSize, 0, _axisXPaint);
        canvas.DrawLine(0, -gridSize, 0, gridSize, _axisYPaint);
    }

    private void DrawBoundary(SKCanvas canvas)
    {
        if (_currentBoundary == null) return;

        // Draw outer boundary
        if (_currentBoundary.OuterBoundary != null && _currentBoundary.OuterBoundary.IsValid)
        {
            var points = _currentBoundary.OuterBoundary.Points;
            if (points.Count >= 3)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < points.Count; i++)
                {
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                }
                path.Close();
                canvas.DrawPath(path, _boundaryOuterPaint);
            }
        }

        // Draw inner boundaries
        foreach (var innerBoundary in _currentBoundary.InnerBoundaries)
        {
            if (innerBoundary.IsValid)
            {
                var points = innerBoundary.Points;
                if (points.Count >= 3)
                {
                    using var path = new SKPath();
                    path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                    for (int i = 1; i < points.Count; i++)
                    {
                        path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                    }
                    path.Close();
                    canvas.DrawPath(path, _boundaryInnerPaint);
                }
            }
        }
    }

    private void DrawRecordingPoints(SKCanvas canvas)
    {
        if (_recordingPoints.Count == 0) return;

        // Draw line connecting points
        if (_recordingPoints.Count > 1)
        {
            using var path = new SKPath();
            path.MoveTo((float)_recordingPoints[0].Easting, (float)_recordingPoints[0].Northing);
            for (int i = 1; i < _recordingPoints.Count; i++)
            {
                path.LineTo((float)_recordingPoints[i].Easting, (float)_recordingPoints[i].Northing);
            }
            canvas.DrawPath(path, _recordingLinePaint);
        }

        // Draw point markers
        float pointRadius = 0.5f; // meters
        foreach (var point in _recordingPoints)
        {
            canvas.DrawCircle((float)point.Easting, (float)point.Northing, pointRadius, _recordingPointPaint);
        }
    }

    private void DrawVehicle(SKCanvas canvas)
    {
        float size = 5.0f; // Vehicle size in meters

        canvas.Save();

        // Translate to vehicle position
        canvas.Translate((float)_vehicleX, (float)_vehicleY);

        // Rotate based on heading
        // Negate heading because AgOpenGPS uses compass convention (clockwise, 0=North)
        // Also need to compensate for the flipped Y axis
        canvas.RotateDegrees((float)(_vehicleHeading * 180.0 / Math.PI));

        // Flip the vehicle vertically since we're drawing in a flipped coordinate system
        canvas.Scale(1, -1);

        if (_vehicleBitmap != null)
        {
            // Draw textured vehicle
            var destRect = new SKRect(-size / 2, -size / 2, size / 2, size / 2);
            canvas.DrawBitmap(_vehicleBitmap, destRect);
        }
        else
        {
            // Fallback: draw a simple triangle
            using var path = new SKPath();
            path.MoveTo(0, size / 2);           // Front
            path.LineTo(-size / 3, -size / 2);  // Back left
            path.LineTo(size / 3, -size / 2);   // Back right
            path.Close();

            using var paint = new SKPaint
            {
                Color = SKColors.LimeGreen,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawPath(path, paint);
        }

        canvas.Restore();
    }

    private void DrawBoundaryOffsetIndicator(SKCanvas canvas)
    {
        // Calculate reference point and offset point
        float refX = (float)_vehicleX;
        float refY = (float)_vehicleY;

        // Calculate perpendicular direction
        float perpAngle = (float)_vehicleHeading + (float)(Math.PI / 2.0);
        float offsetX = refX + (float)(_boundaryOffsetMeters * Math.Sin(perpAngle));
        float offsetY = refY + (float)(_boundaryOffsetMeters * Math.Cos(perpAngle));

        // Draw reference square
        float squareSize = 0.3f;
        canvas.DrawRect(refX - squareSize, refY - squareSize, squareSize * 2, squareSize * 2, _offsetIndicatorPaint);

        // Draw arrow if offset is non-zero
        if (Math.Abs(_boundaryOffsetMeters) > 0.01)
        {
            // Arrow line
            canvas.DrawLine(refX, refY, offsetX, offsetY, _offsetArrowPaint);

            // Arrowhead
            float arrowSize = 0.4f;
            float dx = offsetX - refX;
            float dy = offsetY - refY;
            float len = (float)Math.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f)
            {
                dx /= len;
                dy /= len;
                float px = -dy;
                float py = dx;

                using var arrowPath = new SKPath();
                arrowPath.MoveTo(offsetX, offsetY);
                arrowPath.LineTo(offsetX - dx * arrowSize + px * arrowSize * 0.5f,
                                 offsetY - dy * arrowSize + py * arrowSize * 0.5f);
                arrowPath.LineTo(offsetX - dx * arrowSize - px * arrowSize * 0.5f,
                                 offsetY - dy * arrowSize - py * arrowSize * 0.5f);
                arrowPath.Close();

                using var fillPaint = new SKPaint
                {
                    Color = new SKColor(255, 230, 0),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawPath(arrowPath, fillPaint);
            }
        }
    }

    /// <summary>
    /// Fallback rendering using Avalonia's drawing API when SkiaSharp lease is not available (e.g., iOS)
    /// This method renders to a cached WriteableBitmap using SkiaSharp directly
    /// </summary>
    internal void RenderWithAvaloniaFallback(ImmediateDrawingContext context)
    {
        // ImmediateDrawingContext doesn't support direct bitmap drawing
        // The rendering will be handled in the main Render method via WriteableBitmap
        // This is just a placeholder that indicates the custom operation couldn't render
    }

    // Cached WriteableBitmap for iOS fallback rendering
    private Avalonia.Media.Imaging.WriteableBitmap? _renderBitmap;
    private int _lastRenderWidth = 0;
    private int _lastRenderHeight = 0;

    // Always use WriteableBitmap on mobile platforms (iOS, Android)
    // Desktop can use ISkiaSharpApiLeaseFeature for better performance
    private static readonly bool _useWriteableBitmapFallback =
        OperatingSystem.IsIOS() || OperatingSystem.IsAndroid() || OperatingSystem.IsTvOS() || OperatingSystem.IsWatchOS();

    /// <summary>
    /// Render using WriteableBitmap fallback (for platforms without SkiaSharp lease)
    /// </summary>
    private void RenderToWriteableBitmap(Avalonia.Media.DrawingContext context, Rect bounds, int pixelWidth, int pixelHeight, double scale)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return;
        }

        try
        {
            // Create bitmap at full pixel resolution for crisp rendering
            // DPI should match the scale factor (96 * scale = effective DPI)
            // This tells Avalonia the bitmap's native resolution so it displays 1:1 on screen
            double dpi = 96 * scale;

            if (_renderBitmap == null || _lastRenderWidth != pixelWidth || _lastRenderHeight != pixelHeight)
            {
                Console.WriteLine($"[SkiaMapControl] Creating WriteableBitmap {pixelWidth}x{pixelHeight} (scale={scale}, dpi={dpi}, logical={bounds.Width}x{bounds.Height})");
                _renderBitmap?.Dispose();

                _renderBitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                    new Avalonia.PixelSize(pixelWidth, pixelHeight),
                    new Avalonia.Vector(dpi, dpi),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul);
                _lastRenderWidth = pixelWidth;
                _lastRenderHeight = pixelHeight;
            }

            // Lock the bitmap and render to it using SkiaSharp
            using (var frameBuffer = _renderBitmap.Lock())
            {
                var info = new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(info, frameBuffer.Address, frameBuffer.RowBytes);
                if (surface != null)
                {
                    var canvas = surface.Canvas;
                    // Render using pixel dimensions for drawing, logical dimensions for view calculations
                    // This ensures:
                    // 1. Drawing fills the entire pixel buffer (crisp at full resolution)
                    // 2. View calculations (aspect ratio, zoom) use logical dimensions for correct proportions
                    RenderToCanvasInternal(canvas, (float)pixelWidth, (float)pixelHeight, (float)bounds.Width, (float)bounds.Height);
                }
            }

            // Draw the bitmap to fill the logical bounds
            context.DrawImage(_renderBitmap, new Rect(0, 0, bounds.Width, bounds.Height));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SkiaMapControl] RenderToWriteableBitmap error: {ex}");
        }
    }

    #region IMapControl Implementation

    public void Toggle3DMode()
    {
        // No-op for 2D-only control
        Console.WriteLine("SkiaMapControl: 3D mode not supported");
    }

    public void Set3DMode(bool is3D)
    {
        // No-op for 2D-only control
        if (is3D)
        {
            Console.WriteLine("SkiaMapControl: 3D mode not supported");
        }
    }

    public bool Is3DMode => false;

    public void PanTo(double x, double y)
    {
        _cameraX = x;
        _cameraY = y;
        InvalidateVisual();
    }

    public void SetPitch(double deltaRadians)
    {
        // No-op for 2D-only control
    }

    public void SetPitchAbsolute(double pitchRadians)
    {
        // No-op for 2D-only control
    }

    public void Pan(double deltaX, double deltaY)
    {
        _cameraX += deltaX;
        _cameraY += deltaY;
        InvalidateVisual();
    }

    public void Zoom(double factor)
    {
        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 0.1, 100.0);
        InvalidateVisual();
    }

    public double GetZoom() => _zoom;

    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _cameraX = x;
        _cameraY = y;
        _zoom = zoom;
        _rotation = rotation;
        InvalidateVisual();
    }

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
        InvalidateVisual();
    }

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
        if (_isPanning)
        {
            double deltaX = position.X - _lastMousePosition.X;
            double deltaY = position.Y - _lastMousePosition.Y;

            float aspect = (float)Bounds.Width / (float)Bounds.Height;
            double worldDeltaX = -deltaX * (200.0 * aspect / _zoom) / Bounds.Width;
            double worldDeltaY = deltaY * (200.0 / _zoom) / Bounds.Height;

            Pan(worldDeltaX, worldDeltaY);
            _lastMousePosition = position;
        }
        else if (_isRotating)
        {
            double deltaX = position.X - _lastMousePosition.X;
            double rotationDelta = deltaX * 0.01;
            Rotate(rotationDelta);
            _lastMousePosition = position;
        }
    }

    public void EndPanRotate()
    {
        _isPanning = false;
        _isRotating = false;
    }

    public void SetBoundary(Boundary? boundary)
    {
        _currentBoundary = boundary;
        InvalidateVisual();
    }

    public void SetVehiclePosition(double x, double y, double heading)
    {
        _vehicleX = x;
        _vehicleY = y;
        _vehicleHeading = heading;
        InvalidateVisual();
    }

    public void SetGridVisible(bool visible)
    {
        IsGridVisible = visible;
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _recordingPoints = points.ToList();
        InvalidateVisual();
    }

    public void ClearRecordingPoints()
    {
        _recordingPoints.Clear();
        InvalidateVisual();
    }

    public void SetBackgroundImage(string imagePath, double minX, double maxY, double maxX, double minY)
    {
        try
        {
            if (File.Exists(imagePath))
            {
                _backgroundBitmap?.Dispose();
                using var stream = File.OpenRead(imagePath);
                _backgroundBitmap = SKBitmap.Decode(stream);
                _backgroundMinX = minX;
                _backgroundMaxX = maxX;
                _backgroundMinY = minY;
                _backgroundMaxY = maxY;
                _hasBackgroundImage = true;
                Console.WriteLine($"SkiaMapControl: Background image loaded from {imagePath}");
            }
            else
            {
                Console.WriteLine($"SkiaMapControl: Background image not found: {imagePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SkiaMapControl: Error loading background image: {ex.Message}");
        }
        InvalidateVisual();
    }

    public void ClearBackground()
    {
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        _hasBackgroundImage = false;
        InvalidateVisual();
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        _showBoundaryOffsetIndicator = show;
        _boundaryOffsetMeters = offsetMeters;
        InvalidateVisual();
    }

    #endregion

    #region Mouse Event Handlers

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

            float aspect = (float)Bounds.Width / (float)Bounds.Height;
            double worldDeltaX = -deltaX * (200.0 * aspect / _zoom) / Bounds.Width;
            double worldDeltaY = deltaY * (200.0 / _zoom) / Bounds.Height;

            Pan(worldDeltaX, worldDeltaY);
            _lastMousePosition = currentPos;
            e.Handled = true;
        }
        else if (_isRotating)
        {
            double deltaX = currentPos.X - _lastMousePosition.X;
            double rotationDelta = deltaX * 0.01;
            Rotate(rotationDelta);
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
        Zoom(zoomFactor);
        e.Handled = true;
    }

    #endregion

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // Cleanup resources
        _vehicleBitmap?.Dispose();
        _backgroundBitmap?.Dispose();
        _gridPaint.Dispose();
        _gridMajorPaint.Dispose();
        _axisXPaint.Dispose();
        _axisYPaint.Dispose();
        _boundaryOuterPaint.Dispose();
        _boundaryInnerPaint.Dispose();
        _recordingLinePaint.Dispose();
        _recordingPointPaint.Dispose();
        _offsetIndicatorPaint.Dispose();
        _offsetArrowPaint.Dispose();
    }
}
