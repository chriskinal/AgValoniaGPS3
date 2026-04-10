// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Base;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldBuilderDialogPanel : UserControl
{
    // Drawing state
    private enum DrawMode { None, ABLine, Curve }
    private DrawMode _drawMode = DrawMode.None;
    private readonly List<Vec3> _drawPoints = new();

    // Coordinate transform (set during UpdatePreview)
    private double _minE, _minN, _rangeE, _rangeN;
    private double _scale, _offsetX, _offsetY;
    private double _canvasWidth, _canvasHeight;
    private bool _transformValid;

    public FieldBuilderDialogPanel()
    {
        InitializeComponent();
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible) && IsVisible)
        {
            ShowMainTabs();
            ExitDrawMode();
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
        }
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.State.UI.CloseDialog();
    }

    private void AddTrackBtn_Click(object? sender, RoutedEventArgs e)
    {
        var tabs = this.FindControl<TabControl>("MainTabs");
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (tabs != null) tabs.IsVisible = false;
        if (addPanel != null) addPanel.IsVisible = true;
    }

    private void BackBtn_Click(object? sender, RoutedEventArgs e)
    {
        ExitDrawMode();
        ShowMainTabs();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void ShowMainTabs()
    {
        var tabs = this.FindControl<TabControl>("MainTabs");
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (tabs != null) tabs.IsVisible = true;
        if (addPanel != null) addPanel.IsVisible = false;

        var drawPanel = this.FindControl<Border>("DrawModePanel");
        if (drawPanel != null) drawPanel.IsVisible = false;
    }

    // --- Drawing Mode ---

    private void StartDrawAB_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.ABLine;
        _drawPoints.Clear();
        ShowDrawModeUI("Click point A on the map");

        // Hide add panel, show draw panel
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;
    }

    private void StartDrawCurve_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.Curve;
        _drawPoints.Clear();
        ShowDrawModeUI("Click points on the map, then Finish");

        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;
    }

    private void ShowDrawModeUI(string instruction)
    {
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var finishBtn = this.FindControl<Button>("FinishDrawBtn");
        var undoBtn = this.FindControl<Button>("UndoDrawBtn");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = instruction;
        if (pointCountText != null) pointCountText.Text = "Points: 0";
        if (finishBtn != null) finishBtn.IsVisible = _drawMode == DrawMode.Curve;
        if (undoBtn != null) undoBtn.IsVisible = _drawMode == DrawMode.Curve;

        UpdatePreview();
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_drawMode == DrawMode.None || !_transformValid) return;
        if (DataContext is not MainViewModel vm) return;

        var pos = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));

        // Convert canvas coords back to field coords
        double fieldE = (pos.X - _offsetX) / _scale + _minE;
        double fieldN = (_canvasHeight - pos.Y - _offsetY) / _scale + _minN;

        // Calculate heading from previous point
        double heading = 0;
        if (_drawPoints.Count > 0)
        {
            var last = _drawPoints[^1];
            heading = Math.Atan2(fieldE - last.Easting, fieldN - last.Northing);
        }

        _drawPoints.Add(new Vec3(fieldE, fieldN, heading));

        // Update first point heading if we now have 2 points
        if (_drawPoints.Count == 2)
        {
            _drawPoints[0] = new Vec3(_drawPoints[0].Easting, _drawPoints[0].Northing, heading);
        }

        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");

        if (_drawMode == DrawMode.ABLine)
        {
            if (_drawPoints.Count == 1)
            {
                if (instrText != null) instrText.Text = "Click point B on the map";
                if (pointCountText != null) pointCountText.Text = "Point A set";
            }
            else if (_drawPoints.Count >= 2)
            {
                // Create AB line track
                CreateABLineFromPoints(vm);
                return;
            }
        }
        else if (_drawMode == DrawMode.Curve)
        {
            if (pointCountText != null) pointCountText.Text = $"Points: {_drawPoints.Count}";
            if (instrText != null) instrText.Text = $"Click more points or Finish ({_drawPoints.Count} placed)";
        }

        UpdatePreview();
        e.Handled = true;
    }

    private void CreateABLineFromPoints(MainViewModel vm)
    {
        if (_drawPoints.Count < 2) return;

        var a = _drawPoints[0];
        var b = _drawPoints[1];

        // Use VM's SetABPointCommand logic via Position objects
        var posA = new Position { Easting = a.Easting, Northing = a.Northing };
        var posB = new Position { Easting = b.Easting, Northing = b.Northing };

        // Set up drawing mode in VM and execute
        vm.CurrentABCreationMode = ABCreationMode.DrawAB;
        vm.CurrentABPointStep = ABPointStep.SettingPointA;
        vm.SetABPointCommand?.Execute(posA);
        vm.SetABPointCommand?.Execute(posB);

        ExitDrawMode();
        ShowMainTabs();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void FinishDraw_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        if (_drawMode == DrawMode.Curve && _drawPoints.Count >= 2)
        {
            // Feed points into VM's curve creation
            vm.CurrentABCreationMode = ABCreationMode.DrawCurve;
            foreach (var pt in _drawPoints)
            {
                var pos = new Position { Easting = pt.Easting, Northing = pt.Northing };
                vm.SetABPointCommand?.Execute(pos);
            }
            vm.FinishDrawCurveCommand?.Execute(null);
        }

        ExitDrawMode();
        ShowMainTabs();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void UndoDraw_Click(object? sender, RoutedEventArgs e)
    {
        if (_drawPoints.Count > 0)
        {
            _drawPoints.RemoveAt(_drawPoints.Count - 1);
            var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
            var instrText = this.FindControl<TextBlock>("DrawInstructionText");
            if (pointCountText != null) pointCountText.Text = $"Points: {_drawPoints.Count}";
            if (instrText != null) instrText.Text = $"Click more points or Finish ({_drawPoints.Count} placed)";
            UpdatePreview();
        }
    }

    private void CancelDraw_Click(object? sender, RoutedEventArgs e)
    {
        ExitDrawMode();
        // Go back to add panel
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        if (addPanel != null) addPanel.IsVisible = true;
        if (drawPanel != null) drawPanel.IsVisible = false;
        UpdatePreview();
    }

    private void ExitDrawMode()
    {
        _drawMode = DrawMode.None;
        _drawPoints.Clear();
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        if (drawPanel != null) drawPanel.IsVisible = false;
    }

    // --- Preview Rendering ---

    private void UpdatePreview()
    {
        var canvas = this.FindControl<Canvas>("BoundaryPreview");
        if (canvas == null || DataContext is not MainViewModel vm) return;

        canvas.Children.Clear();
        _transformValid = false;

        var boundary = vm.CurrentBoundary?.OuterBoundary;
        if (boundary?.Points == null || boundary.Points.Count < 3) return;

        _canvasWidth = canvas.Bounds.Width > 0 ? canvas.Bounds.Width : 300;
        _canvasHeight = canvas.Bounds.Height > 0 ? canvas.Bounds.Height : 300;
        if (_canvasWidth < 10 || _canvasHeight < 10) return;

        var pts = boundary.Points;

        _minE = pts.Min(p => p.Easting);
        double maxE = pts.Max(p => p.Easting);
        _minN = pts.Min(p => p.Northing);
        double maxN = pts.Max(p => p.Northing);
        _rangeE = maxE - _minE;
        _rangeN = maxN - _minN;
        if (_rangeE < 1) _rangeE = 1;
        if (_rangeN < 1) _rangeN = 1;

        double margin = 20;
        double scaleX = (_canvasWidth - margin * 2) / _rangeE;
        double scaleY = (_canvasHeight - margin * 2) / _rangeN;
        _scale = Math.Min(scaleX, scaleY);
        _offsetX = (_canvasWidth - _rangeE * _scale) / 2;
        _offsetY = (_canvasHeight - _rangeN * _scale) / 2;
        _transformValid = true;

        Point ToCanvas(double e, double n) => new Point(
            (e - _minE) * _scale + _offsetX,
            _canvasHeight - ((n - _minN) * _scale + _offsetY)
        );

        // Draw boundary polygon
        var boundaryPoly = new Polygon
        {
            Stroke = new SolidColorBrush(Color.FromRgb(230, 180, 50)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(30, 230, 180, 50)),
            Points = pts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
        };
        canvas.Children.Add(boundaryPoly);

        // Draw headland
        if (vm.HasHeadland && vm.CurrentHeadlandLineForPreview != null)
        {
            var headPts = vm.CurrentHeadlandLineForPreview;
            if (headPts.Count >= 3)
            {
                var headlandPoly = new Polygon
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(100, 200, 100)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 },
                    Points = headPts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
                };
                canvas.Children.Add(headlandPoly);
            }
        }

        // Draw tracks as extended lines
        foreach (var track in vm.SavedTracks)
        {
            if (track.Points.Count < 2) continue;

            bool isSelected = track == vm.SelectedTrack;
            var color = new SolidColorBrush(isSelected
                ? Color.FromRgb(50, 200, 255)
                : Color.FromRgb(100, 130, 160));

            List<Point> linePoints;
            if (track.Points.Count == 2)
            {
                var p1 = track.Points[0];
                var p2 = track.Points[1];
                double dx = p2.Easting - p1.Easting;
                double dy = p2.Northing - p1.Northing;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.01) continue;

                double ext = Math.Max(_rangeE, _rangeN) * 2;
                double nx = dx / len, ny = dy / len;
                linePoints = new()
                {
                    ToCanvas(p1.Easting - nx * ext, p1.Northing - ny * ext),
                    ToCanvas(p2.Easting + nx * ext, p2.Northing + ny * ext)
                };
            }
            else
            {
                linePoints = track.Points.Select(p => ToCanvas(p.Easting, p.Northing)).ToList();
            }

            var trackLine = new Polyline
            {
                Stroke = color,
                StrokeThickness = isSelected ? 3 : 1.5,
                Points = linePoints
            };
            canvas.Children.Add(trackLine);

            // A/B markers for selected track
            if (isSelected && track.Points.Count >= 2)
            {
                var first = ToCanvas(track.Points[0].Easting, track.Points[0].Northing);
                var last = ToCanvas(track.Points[track.Points.Count - 1].Easting,
                                    track.Points[track.Points.Count - 1].Northing);

                var markerA = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Red };
                Canvas.SetLeft(markerA, first.X - 5);
                Canvas.SetTop(markerA, first.Y - 5);
                canvas.Children.Add(markerA);

                var markerB = new Ellipse { Width = 10, Height = 10, Fill = Brushes.LimeGreen };
                Canvas.SetLeft(markerB, last.X - 5);
                Canvas.SetTop(markerB, last.Y - 5);
                canvas.Children.Add(markerB);
            }
        }

        // Draw points being placed in draw mode
        if (_drawMode != DrawMode.None && _drawPoints.Count > 0)
        {
            // Draw lines between points
            if (_drawPoints.Count >= 2)
            {
                var drawLine = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 100, 50)),
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                    Points = _drawPoints.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
                };
                canvas.Children.Add(drawLine);
            }

            // Draw point markers
            for (int i = 0; i < _drawPoints.Count; i++)
            {
                var pt = ToCanvas(_drawPoints[i].Easting, _drawPoints[i].Northing);
                var fill = i == 0 ? Brushes.Red : (i == _drawPoints.Count - 1 ? Brushes.Orange : Brushes.Yellow);
                var marker = new Ellipse { Width = 12, Height = 12, Fill = fill, Stroke = Brushes.White, StrokeThickness = 2 };
                Canvas.SetLeft(marker, pt.X - 6);
                Canvas.SetTop(marker, pt.Y - 6);
                canvas.Children.Add(marker);

                // Label A/B for AB line mode
                if (_drawMode == DrawMode.ABLine)
                {
                    var label = new TextBlock
                    {
                        Text = i == 0 ? "A" : "B",
                        FontSize = 14,
                        FontWeight = FontWeight.Bold,
                        Foreground = Brushes.White
                    };
                    Canvas.SetLeft(label, pt.X + 8);
                    Canvas.SetTop(label, pt.Y - 8);
                    canvas.Children.Add(label);
                }
            }
        }
    }
}
