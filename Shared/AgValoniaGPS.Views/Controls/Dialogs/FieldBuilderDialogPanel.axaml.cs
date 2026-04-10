// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private enum DrawMode { None, ABLine, ABLinePreview, Curve }
    private DrawMode _drawMode = DrawMode.None;
    private readonly List<Vec3> _drawPoints = new();

    // Inline confirmation/input
    private Action? _inlineConfirmAction;
    private MainViewModel? _viewModel;

    // Coordinate transform (set during UpdatePreview)
    private double _minE, _minN, _rangeE, _rangeN;
    private double _scale, _offsetX, _offsetY;
    private double _canvasWidth, _canvasHeight;
    private bool _transformValid;

    public FieldBuilderDialogPanel()
    {
        InitializeComponent();
        PropertyChanged += OnPropertyChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainViewModel;

        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTrack) && IsVisible)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
        }
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible) && IsVisible)
        {
            ShowMainTabs();
            ExitDrawMode();
            HideRenamePanel();
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
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        if (tabs != null) tabs.IsVisible = true;
        if (addPanel != null) addPanel.IsVisible = false;
        if (drawPanel != null) drawPanel.IsVisible = false;
    }

    // --- Drawing Mode ---

    private void StartDrawAB_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.ABLine;
        _drawPoints.Clear();
        ShowDrawModeUI("Click point A on the map");

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
        var createBtn = this.FindControl<Button>("CreateABBtn");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = instruction;
        if (pointCountText != null) pointCountText.Text = "Points: 0";
        if (finishBtn != null) finishBtn.IsVisible = _drawMode == DrawMode.Curve;
        if (undoBtn != null) undoBtn.IsVisible = true;
        if (createBtn != null) createBtn.IsVisible = false;

        UpdatePreview();
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_drawMode == DrawMode.None || _drawMode == DrawMode.ABLinePreview || !_transformValid) return;
        if (DataContext is not MainViewModel) return;

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
                // Show preview instead of creating immediately
                _drawMode = DrawMode.ABLinePreview;
                if (instrText != null) instrText.Text = "Preview - click Create or adjust points";
                if (pointCountText != null) pointCountText.Text = "A and B set";

                var createBtn = this.FindControl<Button>("CreateABBtn");
                var finishBtn = this.FindControl<Button>("FinishDrawBtn");
                if (createBtn != null) createBtn.IsVisible = true;
                if (finishBtn != null) finishBtn.IsVisible = false;
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

    private void CreateAB_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && _drawPoints.Count >= 2)
        {
            CreateABLineFromPoints(vm);
        }
    }

    private void CreateABLineFromPoints(MainViewModel vm)
    {
        if (_drawPoints.Count < 2) return;

        var a = _drawPoints[0];
        var b = _drawPoints[1];

        var posA = new Position { Easting = a.Easting, Northing = a.Northing };
        var posB = new Position { Easting = b.Easting, Northing = b.Northing };

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
            var createBtn = this.FindControl<Button>("CreateABBtn");

            // Reset AB preview state if we went back below 2 points
            if (_drawMode == DrawMode.ABLinePreview)
            {
                _drawMode = DrawMode.ABLine;
                if (createBtn != null) createBtn.IsVisible = false;
            }

            if (_drawMode == DrawMode.ABLine)
            {
                if (_drawPoints.Count == 0)
                {
                    if (instrText != null) instrText.Text = "Click point A on the map";
                    if (pointCountText != null) pointCountText.Text = "Points: 0";
                }
                else
                {
                    if (instrText != null) instrText.Text = "Click point B on the map";
                    if (pointCountText != null) pointCountText.Text = "Point A set";
                }
            }
            else
            {
                if (pointCountText != null) pointCountText.Text = $"Points: {_drawPoints.Count}";
                if (instrText != null) instrText.Text = $"Click more points or Finish ({_drawPoints.Count} placed)";
            }

            UpdatePreview();
        }
    }

    private void CancelDraw_Click(object? sender, RoutedEventArgs e)
    {
        ExitDrawMode();
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

    // --- Inline Confirmation ---

    private void ShowInlineConfirmation(string title, string message, Action onConfirm)
    {
        _inlineConfirmAction = onConfirm;
        var titleText = this.FindControl<TextBlock>("InlineConfirmTitle");
        var msgText = this.FindControl<TextBlock>("InlineConfirmMessage");
        var overlay = this.FindControl<Border>("InlineConfirmOverlay");
        if (titleText != null) titleText.Text = title;
        if (msgText != null) msgText.Text = message;
        if (overlay != null) overlay.IsVisible = true;
    }

    private void InlineConfirmYes_Click(object? sender, RoutedEventArgs e)
    {
        var overlay = this.FindControl<Border>("InlineConfirmOverlay");
        if (overlay != null) overlay.IsVisible = false;
        _inlineConfirmAction?.Invoke();
        _inlineConfirmAction = null;
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void InlineConfirmNo_Click(object? sender, RoutedEventArgs e)
    {
        var overlay = this.FindControl<Border>("InlineConfirmOverlay");
        if (overlay != null) overlay.IsVisible = false;
        _inlineConfirmAction = null;
    }

    private void DeleteAllTracks_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.SavedTracks.Count == 0)
        {
            vm.StatusMessage = "No tracks to delete";
            return;
        }
        ShowInlineConfirmation(
            "Delete All Tracks",
            $"Delete all {vm.SavedTracks.Count} tracks? This cannot be undone.",
            () =>
            {
                vm.SavedTracks.Clear();
                vm.SelectedTrack = null;
                vm.StatusMessage = "All tracks deleted";
            });
    }

    // --- Track Renaming ---

    private void RenameTrack_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTrack == null)
        {
            if (DataContext is MainViewModel vm2)
                vm2.StatusMessage = "No track selected";
            return;
        }

        var renameOverlay = this.FindControl<Border>("RenameOverlay");
        var renameInput = this.FindControl<TextBox>("RenameInput");
        if (renameOverlay != null) renameOverlay.IsVisible = true;
        if (renameInput != null)
        {
            renameInput.Text = vm.SelectedTrack.Name;
            renameInput.SelectAll();
            renameInput.Focus();
        }
    }

    private void RenameConfirm_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTrack != null)
        {
            var renameInput = this.FindControl<TextBox>("RenameInput");
            var newName = renameInput?.Text?.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                vm.SelectedTrack.Name = newName;
                vm.StatusMessage = $"Track renamed to: {newName}";
            }
        }
        HideRenamePanel();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void RenameCancel_Click(object? sender, RoutedEventArgs e)
    {
        HideRenamePanel();
    }

    private void RenameInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            RenameConfirm_Click(sender, e);
        else if (e.Key == Key.Escape)
            RenameCancel_Click(sender, e);
    }

    private void HideRenamePanel()
    {
        var renameOverlay = this.FindControl<Border>("RenameOverlay");
        if (renameOverlay != null) renameOverlay.IsVisible = false;
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

        // Draw tracks
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
                var last = ToCanvas(track.Points[^1].Easting, track.Points[^1].Northing);

                AddMarker(canvas, first, Brushes.Red, "A");
                AddMarker(canvas, last, Brushes.LimeGreen, "B");
            }
        }

        // Draw points being placed in draw mode
        if (_drawMode != DrawMode.None && _drawPoints.Count > 0)
        {
            // Draw preview line between points
            if (_drawPoints.Count >= 2)
            {
                var previewPoints = _drawPoints.Select(p => ToCanvas(p.Easting, p.Northing)).ToList();

                // For AB preview, extend the line
                if (_drawMode == DrawMode.ABLinePreview)
                {
                    var p1 = _drawPoints[0];
                    var p2 = _drawPoints[1];
                    double dx = p2.Easting - p1.Easting;
                    double dy = p2.Northing - p1.Northing;
                    double len = Math.Sqrt(dx * dx + dy * dy);
                    if (len > 0.01)
                    {
                        double ext = Math.Max(_rangeE, _rangeN) * 2;
                        double nx = dx / len, ny = dy / len;
                        previewPoints = new()
                        {
                            ToCanvas(p1.Easting - nx * ext, p1.Northing - ny * ext),
                            ToCanvas(p2.Easting + nx * ext, p2.Northing + ny * ext)
                        };
                    }
                }

                var drawLine = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 100, 50)),
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                    Points = previewPoints
                };
                canvas.Children.Add(drawLine);
            }

            // Draw point markers
            for (int i = 0; i < _drawPoints.Count; i++)
            {
                var pt = ToCanvas(_drawPoints[i].Easting, _drawPoints[i].Northing);
                string? label = null;

                if (_drawMode == DrawMode.ABLine || _drawMode == DrawMode.ABLinePreview)
                    label = i == 0 ? "A" : "B";

                var fill = i == 0 ? Brushes.Red : (i == _drawPoints.Count - 1 ? Brushes.Orange : Brushes.Yellow);
                AddMarker(canvas, pt, fill, label);
            }
        }
    }

    private static void AddMarker(Canvas canvas, Point pt, IBrush fill, string? label)
    {
        var marker = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 2
        };
        Canvas.SetLeft(marker, pt.X - 6);
        Canvas.SetTop(marker, pt.Y - 6);
        canvas.Children.Add(marker);

        if (label != null)
        {
            var text = new TextBlock
            {
                Text = label,
                FontSize = 14,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };
            Canvas.SetLeft(text, pt.X + 8);
            Canvas.SetTop(text, pt.Y - 8);
            canvas.Children.Add(text);
        }
    }
}
