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
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Reusable real-time chart control using Avalonia DrawingContext rendering.
/// Supports rolling time window, multiple data series, Y-axis labels, and grid lines.
/// Renders at display refresh rate via DispatcherTimer.
/// </summary>
public class ChartControl : Control
{
    private readonly DispatcherTimer _renderTimer;
    private readonly List<ChartSeries> _series = new();
    private readonly Dictionary<uint, Pen> _seriesPens = new();

    // Layout constants
    private const double LeftMargin = 50;
    private const double RightMargin = 10;
    private const double TopMargin = 8;
    private const double BottomMargin = 20;

    // Visual resources (reused each frame)
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.FromArgb(0xDD, 0x10, 0x14, 0x1C));
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromArgb(0x40, 0x88, 0x88, 0x88)), 0.5);
    private static readonly Pen ZeroLinePen = new(new SolidColorBrush(Color.FromArgb(0x80, 0xAA, 0xAA, 0xAA)), 1.0);
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromArgb(0x60, 0x88, 0x88, 0x88)), 1.0);
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xCC, 0xCC, 0xCC));
    private static readonly Typeface LabelTypeface = new("Segoe UI", FontStyle.Normal, FontWeight.Normal);

    // Chart properties
    private double _minY = -40;
    private double _maxY = 40;
    private double _timeWindow = 20.0;
    private Func<double>? _currentTimeProvider;
    private string _title = "";
    private string _yAxisLabel = "";
    private double _gridStepY = 10;
    private bool _autoScaleY;

    public ChartControl()
    {
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // 30 FPS
        };
        _renderTimer.Tick += (_, _) => InvalidateVisual();

        PropertyChanged += OnControlPropertyChanged;
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible))
        {
            bool visible = e.NewValue is true;
            if (visible && !_renderTimer.IsEnabled)
                _renderTimer.Start();
            else if (!visible && _renderTimer.IsEnabled)
                _renderTimer.Stop();
        }
    }

    /// <summary>
    /// Configure the chart display parameters.
    /// </summary>
    public void Configure(string title, string yAxisLabel, double minY, double maxY,
        double gridStepY, double timeWindow, Func<double> currentTimeProvider, bool autoScaleY = false)
    {
        _title = title;
        _yAxisLabel = yAxisLabel;
        _minY = minY;
        _maxY = maxY;
        _gridStepY = gridStepY;
        _timeWindow = timeWindow;
        _currentTimeProvider = currentTimeProvider;
        _autoScaleY = autoScaleY;
    }

    /// <summary>
    /// Add a data series to the chart.
    /// </summary>
    public void AddSeries(ChartSeries series)
    {
        _series.Add(series);
        var color = Color.FromUInt32(series.Color);
        _seriesPens[series.Color] = new Pen(new SolidColorBrush(color), 1.5);
    }

    /// <summary>
    /// Clear all series from the chart.
    /// </summary>
    public void ClearSeries()
    {
        _series.Clear();
        _seriesPens.Clear();
    }

    /// <summary>
    /// Start the render timer.
    /// </summary>
    public void StartRendering()
    {
        if (!_renderTimer.IsEnabled)
            _renderTimer.Start();
    }

    /// <summary>
    /// Stop the render timer.
    /// </summary>
    public void StopRendering()
    {
        if (_renderTimer.IsEnabled)
            _renderTimer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        double w = bounds.Width;
        double h = bounds.Height;

        // Background
        context.DrawRectangle(BackgroundBrush, BorderPen,
            new Rect(0, 0, w, h), 4, 4);

        // Chart area
        double chartLeft = LeftMargin;
        double chartRight = w - RightMargin;
        double chartTop = TopMargin;
        double chartBottom = h - BottomMargin;
        double chartWidth = chartRight - chartLeft;
        double chartHeight = chartBottom - chartTop;

        if (chartWidth <= 0 || chartHeight <= 0) return;

        // Clip to chart area for data rendering
        var chartRect = new Rect(chartLeft, chartTop, chartWidth, chartHeight);

        // Auto-scale Y if enabled
        double minY = _minY;
        double maxY = _maxY;
        double gridStepY = _gridStepY;
        if (_autoScaleY)
        {
            ComputeAutoScale(out minY, out maxY, out gridStepY);
        }

        double yRange = maxY - minY;
        if (yRange <= 0) yRange = 1;

        // Time range
        double currentTime = _currentTimeProvider?.Invoke() ?? 0;
        double timeStart = currentTime - _timeWindow;

        // Draw grid lines (horizontal)
        DrawGridLines(context, chartLeft, chartRight, chartTop, chartBottom, chartHeight,
            minY, maxY, yRange, gridStepY);

        // Draw zero line if in range
        if (minY < 0 && maxY > 0)
        {
            double zeroY = chartBottom - (-minY / yRange * chartHeight);
            context.DrawLine(ZeroLinePen, new Point(chartLeft, zeroY), new Point(chartRight, zeroY));
        }

        // Draw data series (clipped)
        using (context.PushClip(chartRect))
        {
            foreach (var series in _series)
            {
                DrawSeries(context, series, chartLeft, chartTop, chartWidth, chartHeight,
                    chartBottom, timeStart, _timeWindow, minY, yRange);
            }
        }

        // Draw border around chart area
        context.DrawRectangle(null, BorderPen, chartRect);

        // Draw title
        if (!string.IsNullOrEmpty(_title))
        {
            var titleText = new FormattedText(_title, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 11, LabelBrush);
            context.DrawText(titleText, new Point(chartLeft + 4, chartTop + 1));
        }

        // Draw legend
        DrawLegend(context, chartRight, chartTop);

        // Draw time labels
        DrawTimeLabels(context, chartLeft, chartRight, chartBottom, timeStart);
    }

    private void ComputeAutoScale(out double minY, out double maxY, out double gridStepY)
    {
        double dataMin = double.MaxValue;
        double dataMax = double.MinValue;

        foreach (var series in _series)
        {
            var points = series.GetPoints();
            foreach (var pt in points)
            {
                if (pt.Value < dataMin) dataMin = pt.Value;
                if (pt.Value > dataMax) dataMax = pt.Value;
            }
        }

        if (dataMin == double.MaxValue)
        {
            minY = _minY;
            maxY = _maxY;
            gridStepY = _gridStepY;
            return;
        }

        // Add 10% padding
        double range = dataMax - dataMin;
        if (range < 1) range = 1;
        double pad = range * 0.1;
        minY = dataMin - pad;
        maxY = dataMax + pad;

        // Compute nice grid step
        gridStepY = ComputeNiceStep(maxY - minY, 5);
    }

    private static double ComputeNiceStep(double range, int targetLines)
    {
        double rawStep = range / targetLines;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double normalized = rawStep / magnitude;

        double niceStep;
        if (normalized <= 1.5) niceStep = 1;
        else if (normalized <= 3.5) niceStep = 2;
        else if (normalized <= 7.5) niceStep = 5;
        else niceStep = 10;

        return niceStep * magnitude;
    }

    private void DrawGridLines(DrawingContext context,
        double chartLeft, double chartRight, double chartTop, double chartBottom,
        double chartHeight, double minY, double maxY, double yRange, double gridStepY)
    {
        // Horizontal grid lines
        double firstGridY = Math.Ceiling(minY / gridStepY) * gridStepY;
        for (double val = firstGridY; val <= maxY; val += gridStepY)
        {
            double y = chartBottom - ((val - minY) / yRange * chartHeight);
            context.DrawLine(GridPen, new Point(chartLeft, y), new Point(chartRight, y));

            // Y-axis label
            string label = val.ToString("F0");
            var labelText = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 10, LabelBrush);
            context.DrawText(labelText, new Point(chartLeft - labelText.Width - 4, y - labelText.Height / 2));
        }
    }

    private void DrawSeries(DrawingContext context, ChartSeries series,
        double chartLeft, double chartTop, double chartWidth, double chartHeight,
        double chartBottom, double timeStart, double timeWindow, double minY, double yRange)
    {
        var points = series.GetPoints();
        if (points.Count < 2) return;

        if (!_seriesPens.TryGetValue(series.Color, out var pen)) return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            bool started = false;
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                if (pt.Timestamp < timeStart) continue;

                double x = chartLeft + ((pt.Timestamp - timeStart) / timeWindow * chartWidth);
                double y = chartBottom - ((pt.Value - minY) / yRange * chartHeight);

                if (!started)
                {
                    ctx.BeginFigure(new Point(x, y), false);
                    started = true;
                }
                else
                {
                    ctx.LineTo(new Point(x, y));
                }
            }

            if (!started) return;
            ctx.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private void DrawLegend(DrawingContext context, double chartRight, double chartTop)
    {
        double x = chartRight - 8;
        double y = chartTop + 2;

        for (int i = _series.Count - 1; i >= 0; i--)
        {
            var series = _series[i];
            var color = Color.FromUInt32(series.Color);
            var brush = new SolidColorBrush(color);

            var nameText = new FormattedText(series.Name, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 9, brush);

            x -= nameText.Width;
            context.DrawText(nameText, new Point(x, y));

            // Color indicator line
            var linePen = new Pen(brush, 2);
            double lineX = x - 14;
            double lineY = y + nameText.Height / 2;
            context.DrawLine(linePen, new Point(lineX, lineY), new Point(lineX + 10, lineY));

            x = lineX - 8; // spacing between legend items
        }
    }

    private void DrawTimeLabels(DrawingContext context,
        double chartLeft, double chartRight, double chartBottom, double timeStart)
    {
        double y = chartBottom + 4;
        int labelCount = 5;
        double chartWidth = chartRight - chartLeft;

        for (int i = 0; i <= labelCount; i++)
        {
            double fraction = (double)i / labelCount;
            double t = timeStart + fraction * _timeWindow;
            double x = chartLeft + fraction * chartWidth;

            // Show relative seconds
            string label = $"{t:F0}s";
            var labelText = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, LabelTypeface, 9, LabelBrush);
            context.DrawText(labelText, new Point(x - labelText.Width / 2, y));

            // Vertical grid line
            context.DrawLine(GridPen, new Point(x, chartBottom - (chartBottom - TopMargin)),
                new Point(x, chartBottom));
        }
    }
}
