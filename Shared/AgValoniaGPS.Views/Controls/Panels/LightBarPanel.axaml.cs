// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Views.Controls.Panels;

/// <summary>
/// Light bar / steer bar panel matching legacy AgOpenGPS rendering.
///
/// Light bar mode: arrow indicators showing cross-track error direction.
///   Green arrows (right side) = steer right to correct (negative XTE).
///   Orange-red arrows (left side) = steer left to correct (positive XTE).
///
/// Steer bar mode: arrow indicators showing demanded steer angle (cyan).
///
/// Features matching legacy:
///   - 8 arrows per side with halo effect (black outline + colored fill)
///   - EWMA smoothing (50/50 fast, 98/2 long-term)
///   - Dynamic center gap for text background
///   - Numeric XTE with direction arrows ("< 25" or "50 >")
///   - Secondary long-term average text (when < 150mm)
///   - Dead zone indicator line
///   - Conditional visibility (only when track/contour/playback active)
///   - CmPerPixel configurable dot sensitivity
/// </summary>
public partial class LightBarPanel : UserControl
{
    private const int DotsPerSide = 8;
    private const double ArrowSpacing = 32;
    private const double ArrowSizeFill = 10.0;
    private const double ArrowSizeHalo = 14.0;
    private const double CenterX = 280; // Half of canvas width (560)
    private const double CenterY = 12;  // Vertical center of canvas

    // EWMA state (in mm, matching legacy)
    private double _avgPivDistance;     // Fast 50/50 EWMA for visual display
    private double _longAvgPivDistance; // Slow 98/2 EWMA for secondary text

    // Arrow shapes: halo (black outline) behind fill (colored)
    private readonly Polygon[] _haloArrows = new Polygon[DotsPerSide * 2];
    private readonly Polygon[] _fillArrows = new Polygon[DotsPerSide * 2];

    // Background rail dots
    private readonly Ellipse[] _railDots = new Ellipse[DotsPerSide * 2];

    // Colors matching legacy
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 250, 0));       // legacy (0, 0.98, 0)
    private static readonly IBrush OrangeRedBrush = new SolidColorBrush(Color.FromRgb(250, 77, 0));   // legacy (0.98, 0.30, 0)
    private static readonly IBrush CyanBrush = new SolidColorBrush(Color.FromRgb(50, 200, 220));
    private static readonly IBrush HaloBrush = new SolidColorBrush(Colors.Black);
    private static readonly IBrush RailBrush = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));

    private bool _shapesCreated;

    public LightBarPanel()
    {
        InitializeComponent();
    }

    private void EnsureShapes()
    {
        if (_shapesCreated) return;
        var canvas = this.FindControl<Canvas>("DotCanvas");
        if (canvas == null) return;

        // Compute center gap (legacy: wide = width/18, min 64, + 20 margin)
        double canvasWidth = canvas.Width;
        double wide = canvasWidth / 18.0;
        if (wide < 64) wide = 64;
        double shift = Math.Max(0, (wide + 20) - (2 * ArrowSpacing));

        for (int i = 0; i < DotsPerSide; i++)
        {
            int slot = i + 1; // 1-based (slot 1 = nearest to center)

            // Right side arrows (positive x)
            double rxCenter = (slot + 1) * ArrowSpacing + shift;
            CreateArrowPair(canvas, i * 2, rxCenter, CenterY, true);

            // Left side arrows (negative x, mirrored)
            double lxCenter = -(slot + 1) * ArrowSpacing - shift + CenterX * 2;
            // Actually use CenterX offset
            double rxWorld = CenterX + (slot + 1) * ArrowSpacing * 0.5 + shift * 0.5;
            double lxWorld = CenterX - (slot + 1) * ArrowSpacing * 0.5 - shift * 0.5;

            CreateArrowPairAt(canvas, i * 2, lxWorld, CenterY, false);      // left side
            CreateArrowPairAt(canvas, i * 2 + 1, rxWorld, CenterY, true);   // right side (replace above)
        }

        _shapesCreated = true;
    }

    private void CreateArrows(Canvas canvas)
    {
        canvas.Children.Clear();

        // Compute center gap matching legacy
        double canvasWidth = canvas.Width;
        double wide = canvasWidth / 18.0;
        if (wide < 64) wide = 64;
        double shift = Math.Max(0, (wide + 20) - (2 * ArrowSpacing));

        for (int side = 0; side < 2; side++)
        {
            bool isRight = side == 1;
            for (int i = 0; i < DotsPerSide; i++)
            {
                int slot = i + 1;
                double sign = isRight ? 1 : -1;
                double cx = CenterX + sign * ((slot + 1) * ArrowSpacing * 0.5 + shift * 0.5);
                int idx = side * DotsPerSide + i;

                // Background rail dot
                var rail = new Ellipse { Width = 6, Height = 6, Fill = RailBrush };
                Canvas.SetLeft(rail, cx - 3);
                Canvas.SetTop(rail, CenterY - 3);
                canvas.Children.Add(rail);
                _railDots[idx] = rail;

                // Halo arrow (black outline, slightly larger)
                var halo = CreateArrowPolygon(cx, CenterY, ArrowSizeHalo, isRight);
                halo.Fill = HaloBrush;
                halo.IsVisible = false;
                canvas.Children.Add(halo);
                _haloArrows[idx] = halo;

                // Fill arrow (colored, on top)
                var fill = CreateArrowPolygon(cx, CenterY, ArrowSizeFill, isRight);
                fill.IsVisible = false;
                canvas.Children.Add(fill);
                _fillArrows[idx] = fill;
            }
        }
    }

    private Polygon CreateArrowPolygon(double cx, double cy, double size, bool pointRight)
    {
        // Triangle arrow pointing left or right
        double halfH = size * 0.6;
        var poly = new Polygon();

        if (pointRight)
        {
            poly.Points = new Points
            {
                new Point(cx - size * 0.4, cy - halfH),
                new Point(cx + size * 0.6, cy),
                new Point(cx - size * 0.4, cy + halfH)
            };
        }
        else
        {
            poly.Points = new Points
            {
                new Point(cx + size * 0.4, cy - halfH),
                new Point(cx - size * 0.6, cy),
                new Point(cx + size * 0.4, cy + halfH)
            };
        }

        return poly;
    }

    /// <summary>
    /// Update light bar with current guidance values.
    /// Called from DrawingContextMapControl or platform code on each GPS update.
    /// </summary>
    public void Update(double crossTrackErrorMeters, double steerAngleDegrees,
                       bool hasActiveGuidance, bool isInDeadZone)
    {
        var config = ConfigurationStore.Instance.AutoSteer;
        bool lightBar = config.LightbarEnabled;
        bool steerBar = config.SteerBarEnabled;

        if (!lightBar && !steerBar || !hasActiveGuidance)
        {
            IsVisible = false;
            return;
        }
        IsVisible = true;

        // Lazy-create shapes on first visible update
        var canvas = this.FindControl<Canvas>("DotCanvas");
        if (canvas != null && canvas.Children.Count == 0)
            CreateArrows(canvas);

        if (steerBar)
            UpdateSteerBar(steerAngleDegrees, config);
        else
            UpdateLightBar(crossTrackErrorMeters, config, isInDeadZone);
    }

    private void UpdateLightBar(double errorMeters, AutoSteerConfig config, bool isInDeadZone)
    {
        double cmPerDot = Math.Max(config.CmPerPixel, 1);

        // Convert to mm for EWMA (legacy uses mm internally)
        // Sign convention: negative = left of track (green arrows on right = steer right)
        //                  positive = right of track (red arrows on left = steer left)
        double errorMm = errorMeters * 1000.0;

        // Fast EWMA (50/50) for visual display
        _avgPivDistance = _avgPivDistance * 0.5 + errorMm * 0.5;

        // Slow EWMA (98/2) for long-term secondary text
        _longAvgPivDistance = _longAvgPivDistance * 0.98 + Math.Abs(_avgPivDistance) * 0.02;
        if (_longAvgPivDistance > 150) _longAvgPivDistance = 150;

        // Convert to display units (legacy: mm * 0.1 = cm for metric, mm * 0.03937 = inches)
        bool isMetric = ConfigurationStore.Instance.IsMetric;
        double displayValue = _avgPivDistance * (isMetric ? 0.1 : 0.03937);

        // Clamp display
        displayValue = Math.Clamp(displayValue, -999, 999);

        // Dot calculation (legacy uses display units / cmPerDot)
        double dotsToLight = displayValue / cmPerDot;
        int clampedDots = (int)Math.Clamp(dotsToLight, -DotsPerSide, DotsPerSide);

        // Update arrows
        for (int side = 0; side < 2; side++)
        {
            bool isRight = side == 1;
            for (int i = 0; i < DotsPerSide; i++)
            {
                int idx = side * DotsPerSide + i;
                int slot = i + 1;
                bool lit;

                if (isRight)
                {
                    // Right side lights when XTE is negative (steer right)
                    lit = clampedDots < 0 && slot <= Math.Abs(clampedDots);
                    if (lit) _fillArrows[idx].Fill = GreenBrush;
                }
                else
                {
                    // Left side lights when XTE is positive (steer left)
                    lit = clampedDots > 0 && slot <= clampedDots;
                    if (lit) _fillArrows[idx].Fill = OrangeRedBrush;
                }

                _haloArrows[idx].IsVisible = lit;
                _fillArrows[idx].IsVisible = lit;
            }
        }

        // Main text: legacy format with direction arrows
        UpdateMainText(displayValue, isMetric);

        // Secondary long-term text
        UpdateLongTermText(isMetric);

        // Dead zone indicator
        var deadZoneLine = this.FindControl<Border>("DeadZoneLine");
        if (deadZoneLine != null)
            deadZoneLine.IsVisible = isInDeadZone;
    }

    private void UpdateMainText(double displayValue, bool isMetric)
    {
        var xteText = this.FindControl<TextBlock>("XteText");
        if (xteText == null) return;

        // Legacy format: "> 0 <" when on-track, "< 25" or "50 >" with direction
        string text;
        if (Math.Abs(displayValue) < 1.0)
        {
            text = "> 0 <";
        }
        else
        {
            int absVal = (int)Math.Abs(displayValue);
            text = displayValue < 0
                ? $"{absVal} >"    // negative = steer right
                : $"< {absVal}";  // positive = steer left
        }

        xteText.Text = text;

        // Dynamic color matching legacy: green when on-track, red when off
        double absMm = Math.Abs(_avgPivDistance);
        double green = Math.Min(absMm, 400);
        double greenComp = (0.4 - green * 0.001) + 0.58;
        double redComp = 0.002 * Math.Min(absMm, 400);
        byte r = (byte)(Math.Clamp(redComp, 0, 1) * 255);
        byte g = (byte)(Math.Clamp(greenComp, 0, 1) * 255);
        xteText.Foreground = new SolidColorBrush(Color.FromRgb(r, g, 77));
    }

    private void UpdateLongTermText(bool isMetric)
    {
        var longAvgText = this.FindControl<TextBlock>("LongAvgText");
        if (longAvgText == null) return;

        if (_longAvgPivDistance < 150)
        {
            double val = Math.Abs(_longAvgPivDistance * (isMetric ? 0.1 : 0.03937));
            longAvgText.Text = val.ToString("N1");
            longAvgText.IsVisible = true;
        }
        else
        {
            longAvgText.IsVisible = false;
        }
    }

    private void UpdateSteerBar(double steerAngleDegrees, AutoSteerConfig config)
    {
        // Fast EWMA for steer display
        double smoothed = _avgPivDistance * 0.8 + steerAngleDegrees * 0.2;
        _avgPivDistance = smoothed;

        double maxAngle = Math.Max(ConfigurationStore.Instance.Vehicle.MaxSteerAngle, 1);
        double dotsToLight = smoothed / maxAngle * DotsPerSide;
        int clampedDots = (int)Math.Clamp(dotsToLight, -DotsPerSide, DotsPerSide);

        for (int side = 0; side < 2; side++)
        {
            bool isRight = side == 1;
            for (int i = 0; i < DotsPerSide; i++)
            {
                int idx = side * DotsPerSide + i;
                int slot = i + 1;
                bool lit;

                if (isRight)
                {
                    lit = clampedDots > 0 && slot <= clampedDots;
                }
                else
                {
                    lit = clampedDots < 0 && slot <= Math.Abs(clampedDots);
                }

                if (lit) _fillArrows[idx].Fill = CyanBrush;
                _haloArrows[idx].IsVisible = lit;
                _fillArrows[idx].IsVisible = lit;
            }
        }

        var xteText = this.FindControl<TextBlock>("XteText");
        if (xteText != null)
        {
            xteText.Text = $"{smoothed:F1} deg";
            xteText.Foreground = new SolidColorBrush(Colors.White);
        }

        var longAvgText = this.FindControl<TextBlock>("LongAvgText");
        if (longAvgText != null) longAvgText.IsVisible = false;

        var deadZoneLine = this.FindControl<Border>("DeadZoneLine");
        if (deadZoneLine != null) deadZoneLine.IsVisible = false;
    }

    // Unused stubs - kept for compatibility
    private void CreateArrowPair(Canvas c, int i, double x, double y, bool r) { }
    private void CreateArrowPairAt(Canvas c, int i, double x, double y, bool r) { }
}
