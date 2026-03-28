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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace AgValoniaGPS.Views.Controls.Panels;

public partial class HeadingChartPanel : UserControl
{
    private bool _isDragging;
    private Point _lastScreenPoint;
    private bool _configured;

    public event EventHandler<PointerPressedEventArgs>? DragStarted;
    public event EventHandler<Vector>? DragMoved;
    public event EventHandler<PointerReleasedEventArgs>? DragEnded;

    public static IServiceProvider? ServiceProvider { get; set; }

    public HeadingChartPanel()
    {
        InitializeComponent();
        var dragHandle = this.FindControl<Grid>("DragHandle");
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += DragHandle_PointerPressed;
            dragHandle.PointerMoved += DragHandle_PointerMoved;
            dragHandle.PointerReleased += DragHandle_PointerReleased;
        }

        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(IsVisible) && e.NewValue is true && !_configured)
            {
                var chartData = ServiceProvider?.GetService<IChartDataService>();
                if (chartData != null) ConfigureChart(chartData);
            }
        };
    }

    public void ConfigureChart(IChartDataService chartData)
    {
        if (_configured) return;
        _configured = true;

        var chart = this.FindControl<ChartControl>("HeadingChart");
        if (chart == null) return;

        chart.Configure(
            title: "Heading",
            yAxisLabel: "deg",
            minY: 0,
            maxY: 360,
            gridStepY: 45,
            timeWindow: chartData.TimeWindowSeconds,
            currentTimeProvider: () => chartData.CurrentTime,
            autoScaleY: true);

        chart.AddSeries(chartData.HeadingError);
        chart.AddSeries(chartData.ImuHeading);
        chart.AddSeries(chartData.GpsHeading);
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid handle)
        {
            var root = this.VisualRoot as Visual;
            _lastScreenPoint = root != null ? e.GetPosition(root) : e.GetPosition(this);
            e.Pointer.Capture(handle);
        }
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            var root = this.VisualRoot as Visual;
            var currentPoint = root != null ? e.GetPosition(root) : e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _lastScreenPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _lastScreenPoint.Y, 2));
            if (!_isDragging && distance > 5.0)
            {
                _isDragging = true;
                DragStarted?.Invoke(this, null!);
            }
            if (_isDragging)
            {
                var delta = currentPoint - _lastScreenPoint;
                DragMoved?.Invoke(this, delta);
                _lastScreenPoint = currentPoint;
            }
            e.Handled = true;
        }
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            if (_isDragging) DragEnded?.Invoke(this, e);
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
