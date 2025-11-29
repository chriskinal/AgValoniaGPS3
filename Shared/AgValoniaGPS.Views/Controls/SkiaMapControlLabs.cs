using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Labs.Controls;
using Avalonia.Threading;
using SkiaSharp;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// SkiaSharp-based map control using Avalonia.Labs.SKCanvasView for proper DPI handling.
/// This control handles high-DPI/Retina displays automatically.
/// </summary>
public class SkiaMapControlLabs : SKCanvasView, IMapControl
{
    // Avalonia property for grid visibility
    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<SkiaMapControlLabs, bool>(
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
    private double _vehicleX = 0.0;
    private double _vehicleY = 0.0;
    private double _vehicleHeading = 0.0;

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
    private readonly SKPaint _vehiclePaint;

    public SkiaMapControlLabs()
    {
        Console.WriteLine("[SkiaMapControlLabs] Constructor starting...");

        // Make control focusable
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        // Initialize paints
        _gridPaint = new SKPaint
        {
            Color = new SKColor(76, 76, 76, 76),
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _gridMajorPaint = new SKPaint
        {
            Color = new SKColor(76, 76, 76, 128),
            StrokeWidth = 1,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _axisXPaint = new SKPaint
        {
            Color = new SKColor(204, 51, 51, 204),
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _axisYPaint = new SKPaint
        {
            Color = new SKColor(51, 204, 51, 204),
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
            Color = new SKColor(255, 128, 0),
            StrokeWidth = 4,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _offsetIndicatorPaint = new SKPaint
        {
            Color = new SKColor(0, 204, 204),
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        _offsetArrowPaint = new SKPaint
        {
            Color = new SKColor(255, 230, 0),
            StrokeWidth = 2,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke
        };

        _vehiclePaint = new SKPaint
        {
            Color = SKColors.LimeGreen,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        // Start render loop
        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += (s, e) => InvalidateVisual();
        timer.Start();

        // Wire up mouse events
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        Console.WriteLine("[SkiaMapControlLabs] Constructor completed.");
    }

    protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
    {
        base.OnPaintSurface(e);

        var canvas = e.Surface.Canvas;
        var info = e.Info;

        // Use logical dimensions from control's Bounds
        // SKCanvasView handles DPI scaling internally - we work in DIPs
        float width = (float)Bounds.Width;
        float height = (float)Bounds.Height;

        if (width <= 0 || height <= 0)
            return;

        // Clear background
        canvas.Clear(new SKColor(25, 25, 25));

        // Calculate view transformation using logical dimensions
        float aspect = width / height;
        float viewWidth = 200.0f * aspect / (float)_zoom;
        float viewHeight = 200.0f / (float)_zoom;

        canvas.Save();

        // Center the canvas using logical dimensions
        canvas.Translate(width / 2, height / 2);

        // Apply rotation
        canvas.RotateDegrees((float)(-_rotation * 180.0 / Math.PI));

        // Calculate scale: DIPs per meter
        float scaleX = width / viewWidth;
        float scaleY = height / viewHeight;
        canvas.Scale(scaleX, -scaleY);

        // Translate to camera position
        canvas.Translate(-(float)_cameraX, -(float)_cameraY);

        // Draw background image
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

        canvas.Restore();
    }

    private void DrawBackgroundImage(SKCanvas canvas)
    {
        if (_backgroundBitmap == null) return;

        canvas.Save();
        canvas.Scale(1, -1);
        canvas.Translate(0, -(float)(_backgroundMinY + _backgroundMaxY));

        var rect = new SKRect(
            (float)_backgroundMinX,
            (float)_backgroundMinY,
            (float)_backgroundMaxX,
            (float)_backgroundMaxY);

        canvas.DrawBitmap(_backgroundBitmap, rect);
        canvas.Restore();
    }

    private void DrawGrid(SKCanvas canvas, float viewWidth, float viewHeight)
    {
        float gridSize = 500.0f;
        float spacing = 10.0f;

        float minX = Math.Max((float)_cameraX - viewWidth, -gridSize);
        float maxX = Math.Min((float)_cameraX + viewWidth, gridSize);
        float minY = Math.Max((float)_cameraY - viewHeight, -gridSize);
        float maxY = Math.Min((float)_cameraY + viewHeight, gridSize);

        float startX = (float)Math.Floor(minX / spacing) * spacing;
        float startY = (float)Math.Floor(minY / spacing) * spacing;

        for (float x = startX; x <= maxX; x += spacing)
        {
            var paint = (Math.Abs(x % 50.0f) < 0.1f) ? _gridMajorPaint : _gridPaint;
            canvas.DrawLine(x, Math.Max(minY, -gridSize), x, Math.Min(maxY, gridSize), paint);
        }

        for (float y = startY; y <= maxY; y += spacing)
        {
            var paint = (Math.Abs(y % 50.0f) < 0.1f) ? _gridMajorPaint : _gridPaint;
            canvas.DrawLine(Math.Max(minX, -gridSize), y, Math.Min(maxX, gridSize), y, paint);
        }

        canvas.DrawLine(-gridSize, 0, gridSize, 0, _axisXPaint);
        canvas.DrawLine(0, -gridSize, 0, gridSize, _axisYPaint);
    }

    private void DrawBoundary(SKCanvas canvas)
    {
        if (_currentBoundary == null) return;

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

        float pointRadius = 0.5f;
        foreach (var point in _recordingPoints)
        {
            canvas.DrawCircle((float)point.Easting, (float)point.Northing, pointRadius, _recordingPointPaint);
        }
    }

    private void DrawVehicle(SKCanvas canvas)
    {
        float size = 5.0f;

        canvas.Save();
        canvas.Translate((float)_vehicleX, (float)_vehicleY);
        canvas.RotateDegrees((float)(_vehicleHeading * 180.0 / Math.PI));
        canvas.Scale(1, -1);

        using var path = new SKPath();
        path.MoveTo(0, size / 2);
        path.LineTo(-size / 3, -size / 2);
        path.LineTo(size / 3, -size / 2);
        path.Close();

        canvas.DrawPath(path, _vehiclePaint);
        canvas.Restore();
    }

    private void DrawBoundaryOffsetIndicator(SKCanvas canvas)
    {
        float refX = (float)_vehicleX;
        float refY = (float)_vehicleY;

        float perpAngle = (float)_vehicleHeading + (float)(Math.PI / 2.0);
        float offsetX = refX + (float)(_boundaryOffsetMeters * Math.Sin(perpAngle));
        float offsetY = refY + (float)(_boundaryOffsetMeters * Math.Cos(perpAngle));

        float squareSize = 0.3f;
        canvas.DrawRect(refX - squareSize, refY - squareSize, squareSize * 2, squareSize * 2, _offsetIndicatorPaint);

        if (Math.Abs(_boundaryOffsetMeters) > 0.01)
        {
            canvas.DrawLine(refX, refY, offsetX, offsetY, _offsetArrowPaint);

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

    #region IMapControl Implementation

    public void Toggle3DMode() { }
    public void Set3DMode(bool is3D) { }
    public bool Is3DMode => false;
    public void SetPitch(double deltaRadians) { }
    public void SetPitchAbsolute(double pitchRadians) { }

    public void PanTo(double x, double y)
    {
        _cameraX = x;
        _cameraY = y;
        InvalidateVisual();
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
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading background image: {ex.Message}");
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
        _vehiclePaint.Dispose();
    }
}
