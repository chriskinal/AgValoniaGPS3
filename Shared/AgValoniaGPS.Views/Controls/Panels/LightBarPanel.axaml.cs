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
/// Light bar / steer bar panel.
/// Light bar mode: shows cross-track error as LED dots (green right / red left).
/// Steer bar mode: shows demanded steer angle as LED dots (cyan).
/// 8 dots per side, configurable cm per dot via AutoSteerConfig.CmPerPixel.
/// Works with or without autosteer hardware - just needs an active track.
/// </summary>
public partial class LightBarPanel : UserControl
{
    private const int DotsPerSide = 8;
    private const double DotSpacing = 32;
    private const double DotRadius = 6;
    private const double CenterX = 260; // Half of canvas width (520)

    private readonly Ellipse[] _dots = new Ellipse[DotsPerSide * 2 + 1];
    private double _smoothedXte;
    private double _smoothedSteer;

    // Dot colors
    private static readonly IBrush DimBrush = new SolidColorBrush(Color.FromArgb(80, 128, 128, 128));
    private static readonly IBrush GreenBrush = new SolidColorBrush(Color.FromRgb(50, 220, 50));
    private static readonly IBrush RedBrush = new SolidColorBrush(Color.FromRgb(220, 50, 50));
    private static readonly IBrush CyanBrush = new SolidColorBrush(Color.FromRgb(50, 200, 220));
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
            double y = (20 - DotRadius * 2) / 2;

            Canvas.SetLeft(dot, x);
            Canvas.SetTop(dot, y);
            canvas.Children.Add(dot);
            _dots[i] = dot;
        }
    }

    /// <summary>
    /// Update light bar / steer bar with current guidance values.
    /// Called on every CrossTrackError property change.
    /// </summary>
    public void Update(double crossTrackErrorMeters, double steerAngleDegrees)
    {
        var config = ConfigurationStore.Instance.AutoSteer;
        bool lightBar = config.LightbarEnabled;
        bool steerBar = config.SteerBarEnabled;

        if (!lightBar && !steerBar)
        {
            IsVisible = false;
            return;
        }
        IsVisible = true;

        if (steerBar)
            UpdateSteerBar(steerAngleDegrees, config);
        else
            UpdateLightBar(crossTrackErrorMeters, config);
    }

    // Legacy compat: single-param method for existing call sites
    public void UpdateCrossTrackError(double errorMeters)
    {
        Update(errorMeters, 0);
    }

    private void UpdateLightBar(double errorMeters, AutoSteerConfig config)
    {
        double cmPerDot = Math.Max(config.CmPerPixel, 1);
        double errorCm = errorMeters * 100.0;

        _smoothedXte = _smoothedXte * 0.5 + errorCm * 0.5;

        double dotsToLight = _smoothedXte / cmPerDot;
        int clampedDots = (int)Math.Clamp(dotsToLight, -DotsPerSide, DotsPerSide);

        for (int i = 0; i < _dots.Length; i++)
        {
            int idx = i - DotsPerSide;

            if (idx == 0)
                _dots[i].Fill = Math.Abs(dotsToLight) < 1.0 ? CenterOnBrush : CenterOffBrush;
            else if (idx > 0 && clampedDots < 0)
                _dots[i].Fill = idx <= Math.Abs(clampedDots) ? GreenBrush : DimBrush;
            else if (idx < 0 && clampedDots > 0)
                _dots[i].Fill = Math.Abs(idx) <= clampedDots ? RedBrush : DimBrush;
            else
                _dots[i].Fill = DimBrush;
        }

        UpdateText(_smoothedXte, "cm", "in", 2.54);
    }

    private void UpdateSteerBar(double steerAngleDegrees, AutoSteerConfig config)
    {
        _smoothedSteer = _smoothedSteer * 0.5 + steerAngleDegrees * 0.5;

        // Scale: max steer angle maps to all dots
        double maxAngle = Math.Max(ConfigurationStore.Instance.Vehicle.MaxSteerAngle, 1);
        double dotsToLight = _smoothedSteer / maxAngle * DotsPerSide;
        int clampedDots = (int)Math.Clamp(dotsToLight, -DotsPerSide, DotsPerSide);

        for (int i = 0; i < _dots.Length; i++)
        {
            int idx = i - DotsPerSide;

            if (idx == 0)
                _dots[i].Fill = Math.Abs(dotsToLight) < 0.5 ? CenterOnBrush : CenterOffBrush;
            else if (idx > 0 && clampedDots > 0)
                _dots[i].Fill = idx <= clampedDots ? CyanBrush : DimBrush;
            else if (idx < 0 && clampedDots < 0)
                _dots[i].Fill = Math.Abs(idx) <= Math.Abs(clampedDots) ? CyanBrush : DimBrush;
            else
                _dots[i].Fill = DimBrush;
        }

        var xteText = this.FindControl<TextBlock>("XteText");
        if (xteText != null)
            xteText.Text = $"{_smoothedSteer:F1} deg";
    }

    private void UpdateText(double valueCm, string metricUnit, string imperialUnit, double convFactor)
    {
        var xteText = this.FindControl<TextBlock>("XteText");
        if (xteText == null) return;

        if (ConfigurationStore.Instance.IsMetric)
            xteText.Text = $"{(int)Math.Round(valueCm)} {metricUnit}";
        else
            xteText.Text = $"{valueCm / convFactor:F1} {imperialUnit}";
    }
}
