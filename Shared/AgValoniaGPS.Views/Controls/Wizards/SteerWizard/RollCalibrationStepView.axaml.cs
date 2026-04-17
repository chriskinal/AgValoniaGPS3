using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

using AgValoniaGPS.ViewModels.Wizards.SteerWizard;

namespace AgValoniaGPS.Views.Controls.Wizards.SteerWizard;

public partial class RollCalibrationStepView : UserControl
{
    private Canvas? _gaugeCanvas;

    public RollCalibrationStepView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _gaugeCanvas = this.FindControl<Canvas>("GaugeCanvas");
        DrawGauge(0);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is INotifyPropertyChanged oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (DataContext is RollCalibrationStepViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            DrawGauge(vm.LiveRoll);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RollCalibrationStepViewModel.LiveRoll) &&
            DataContext is RollCalibrationStepViewModel vm)
        {
            DrawGauge(vm.LiveRoll);
        }
    }

    private void DrawGauge(double rollAngle)
    {
        if (_gaugeCanvas == null) return;

        _gaugeCanvas.Children.Clear();

        double width = 280;
        double height = 160;
        double centerX = width / 2;
        double centerY = height - 10;
        double radius = 120;
        double innerRadius = radius - 20;

        // Clamp roll angle for display
        double clampedRoll = Math.Max(-15, Math.Min(15, rollAngle));

        // Draw arc segments with color coding
        // Green zone: -3 to +3 degrees
        // Yellow zone: -7 to -3 and +3 to +7
        // Red zone: beyond +/- 7
        DrawArcSegment(centerX, centerY, radius, innerRadius, -15, -7, Brushes.IndianRed, 0.4);
        DrawArcSegment(centerX, centerY, radius, innerRadius, -7, -3, Brushes.Goldenrod, 0.5);
        DrawArcSegment(centerX, centerY, radius, innerRadius, -3, 3, Brushes.MediumSeaGreen, 0.6);
        DrawArcSegment(centerX, centerY, radius, innerRadius, 3, 7, Brushes.Goldenrod, 0.5);
        DrawArcSegment(centerX, centerY, radius, innerRadius, 7, 15, Brushes.IndianRed, 0.4);

        // Draw tick marks and labels
        double[] majorTicks = { -10, -5, 0, 5, 10 };
        foreach (double tick in majorTicks)
        {
            double angleRad = DegreesToGaugeRad(tick);
            double outerX = centerX + (radius + 5) * Math.Cos(angleRad);
            double outerY = centerY + (radius + 5) * Math.Sin(angleRad);
            double tickInnerX = centerX + (radius - 25) * Math.Cos(angleRad);
            double tickInnerY = centerY + (radius - 25) * Math.Sin(angleRad);

            var tickLine = new Line
            {
                StartPoint = new Point(tickInnerX, tickInnerY),
                EndPoint = new Point(outerX, outerY),
                Stroke = Application.Current?.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
                         ?? Brushes.Gray,
                StrokeThickness = 2
            };
            _gaugeCanvas.Children.Add(tickLine);

            // Label
            double labelX = centerX + (radius + 18) * Math.Cos(angleRad);
            double labelY = centerY + (radius + 18) * Math.Sin(angleRad);

            var label = new TextBlock
            {
                Text = tick.ToString(),
                FontSize = 11,
                Foreground = Application.Current?.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
                             ?? Brushes.Gray,
            };
            Canvas.SetLeft(label, labelX - 8);
            Canvas.SetTop(label, labelY - 8);
            _gaugeCanvas.Children.Add(label);
        }

        // Draw needle
        double needleAngleRad = DegreesToGaugeRad(clampedRoll);
        double needleLength = radius - 5;
        double needleEndX = centerX + needleLength * Math.Cos(needleAngleRad);
        double needleEndY = centerY + needleLength * Math.Sin(needleAngleRad);

        var needle = new Line
        {
            StartPoint = new Point(centerX, centerY),
            EndPoint = new Point(needleEndX, needleEndY),
            Stroke = Application.Current?.FindResource("SystemControlHighlightAccentBrush") as IBrush
                     ?? Brushes.DodgerBlue,
            StrokeThickness = 3,
            StrokeLineCap = PenLineCap.Round
        };
        _gaugeCanvas.Children.Add(needle);

        // Center dot
        var centerDot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = Application.Current?.FindResource("SystemControlHighlightAccentBrush") as IBrush
                   ?? Brushes.DodgerBlue,
        };
        Canvas.SetLeft(centerDot, centerX - 5);
        Canvas.SetTop(centerDot, centerY - 5);
        _gaugeCanvas.Children.Add(centerDot);
    }

    /// <summary>
    /// Convert roll degrees to gauge radians.
    /// Roll -15 maps to left of arc (pi), 0 maps to straight up (-pi/2), +15 maps to right (0).
    /// Gauge spans from pi to 0 (top semicircle).
    /// </summary>
    private static double DegreesToGaugeRad(double degrees)
    {
        // Map -15..+15 to pi..0 (left to right across top)
        double fraction = (degrees + 15.0) / 30.0; // 0 to 1
        double angle = Math.PI * (1 - fraction); // pi to 0
        return -angle; // negate for screen coords (y-down)
    }

    private void DrawArcSegment(double cx, double cy, double outerR, double innerR,
                                 double fromDeg, double toDeg, IBrush fill, double opacity)
    {
        // Draw as a series of small filled rectangles approximating the arc
        int steps = Math.Max(2, (int)Math.Abs(toDeg - fromDeg));
        for (int i = 0; i < steps; i++)
        {
            double d1 = fromDeg + (toDeg - fromDeg) * i / steps;
            double d2 = fromDeg + (toDeg - fromDeg) * (i + 1) / steps;

            double a1 = DegreesToGaugeRad(d1);
            double a2 = DegreesToGaugeRad(d2);

            var polygon = new Avalonia.Controls.Shapes.Polygon
            {
                Points =
                [
                    new Point(cx + innerR * Math.Cos(a1), cy + innerR * Math.Sin(a1)),
                    new Point(cx + outerR * Math.Cos(a1), cy + outerR * Math.Sin(a1)),
                    new Point(cx + outerR * Math.Cos(a2), cy + outerR * Math.Sin(a2)),
                    new Point(cx + innerR * Math.Cos(a2), cy + innerR * Math.Sin(a2))
                ],
                Fill = fill,
                Opacity = opacity
            };
            _gaugeCanvas!.Children.Add(polygon);
        }
    }
}
