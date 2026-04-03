// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using AgValoniaGPS.Models.Configuration;

namespace AgValoniaGPS.Views.Controls.Panels;

/// <summary>
/// Light bar panel showing cross-track error as a row of LED-like dots.
/// Green dots = steer right, red dots = steer left.
/// 8 dots per side, configurable cm per dot via AutoSteerConfig.CmPerPixel.
/// </summary>
public partial class LightBarPanel : UserControl
{
    private const int DotsPerSide = 8;
    private const double DotSpacing = 32;
    private const double DotRadius = 6;
    private const double CenterX = 260; // Half of canvas width (520)

    private readonly Ellipse[] _dots = new Ellipse[DotsPerSide * 2 + 1]; // left 8 + center + right 8
    private double _smoothedError; // EWMA-filtered cross-track error in cm

    // Dot colors
    private static readonly IBrush DimBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128));
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(50, 220, 50));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.FromRgb(220, 50, 50));
    private static readonly IBrush CenterOnBrush = new SolidColorBrush(Color.FromRgb(255, 255, 80));
    private static readonly IBrush CenterOffBrush = new SolidColorBrush(Color.FromArgb(120, 200, 200, 80));

    public LightBarPanel()
    {
        InitializeComponent();
        CreateDots();
    }

    private void CreateDots()
    {
        var canvas = this.FindControl<Canvas>("DotCanvas");
        if (canvas == null) return;

        for (int i = 0; i < _dots.Length; i++)
        {
            int idx = i - DotsPerSide; // -8 to +8, 0 = center
            var dot = new Ellipse
            {
                Width = DotRadius * 2,
                Height = DotRadius * 2,
                Fill = DimBrush
            };

            double x = CenterX + idx * DotSpacing - DotRadius;
            double y = (20 - DotRadius * 2) / 2; // Center vertically

            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            canvas.Children.Add(dot);
            _dots[i] = dot;
        }
    }

    /// <summary>
    /// Update the light bar with a new cross-track error value.
    /// Positive = right of line (steer left to correct = red dots on left).
    /// Negative = left of line (steer right to correct = green dots on right).
    /// </summary>
    public void UpdateCrossTrackError(double errorMeters)
    {
        var config = ConfigurationStore.Instance.AutoSteer;
        if (!config.LightbarEnabled)
        {
            IsVisible = false;
            return;
        }
        IsVisible = true;

        double cmPerDot = Math.Max(config.CmPerPixel, 1);
        double errorCm = errorMeters * 100.0;

        // EWMA smoothing (50/50 weight like legacy)
        _smoothedError = _smoothedError * 0.5 + errorCm * 0.5;

        // How many dots to light up
        double dotsToLight = _smoothedError / cmPerDot;
        int clampedDots = (int)Math.Clamp(dotsToLight, -DotsPerSide, DotsPerSide);

        // Update dots
        for (int i = 0; i < _dots.Length; i++)
        {
            int idx = i - DotsPerSide; // -8 to +8

            if (idx == 0)
            {
                // Center dot: bright when on-track (within 1 dot)
                _dots[i].Fill = Math.Abs(dotsToLight) < 1.0 ? CenterOnBrush : CenterOffBrush;
            }
            else if (idx > 0 && clampedDots < 0)
            {
                // Right side: green when error is negative (left of line, steer right)
                _dots[i].Fill = idx <= Math.Abs(clampedDots) ? GreenBrush : DimBrush;
            }
            else if (idx < 0 && clampedDots > 0)
            {
                // Left side: red when error is positive (right of line, steer left)
                _dots[i].Fill = Math.Abs(idx) <= clampedDots ? RedBrush : DimBrush;
            }
            else
            {
                _dots[i].Fill = DimBrush;
            }
        }

        // Update numeric display
        var xteText = this.FindControl<TextBlock>("XteText");
        if (xteText != null)
        {
            bool isMetric = ConfigurationStore.Instance.IsMetric;
            if (isMetric)
            {
                int displayCm = (int)Math.Round(_smoothedError);
                xteText.Text = $"{displayCm} cm";
            }
            else
            {
                double inches = _smoothedError / 2.54;
                xteText.Text = $"{inches:F1} in";
            }
        }
    }
}
