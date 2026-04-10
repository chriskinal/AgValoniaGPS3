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
using AgValoniaGPS.Models.Track;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldBuilderDialogPanel : UserControl
{
    // Drawing state
    private enum DrawMode { None, ABLine, ABLinePreview, Curve, BoundaryLine, BoundaryLinePreview, BoundaryCurve, BoundaryCurvePreview }
    private DrawMode _drawMode = DrawMode.None;
    private readonly List<Vec3> _drawPoints = new();
    private int _boundaryPointIndex1 = -1;
    private int _boundaryPointIndex2 = -1;
    private BoundaryPolygon? _selectedBoundaryPoly;

    // Drag state
    private int _dragPointIndex = -1;
    private bool _isDragging;

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

    private void StartBoundaryLine_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.BoundaryLine;
        _drawPoints.Clear();
        _boundaryPointIndex1 = _boundaryPointIndex2 = -1;
        _selectedBoundaryPoly = (DataContext as MainViewModel)?.CurrentBoundary?.OuterBoundary;
        ShowDrawModeUI("Click first point on the boundary");

        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;
    }

    private void StartBoundaryCurve_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.BoundaryCurve;
        _drawPoints.Clear();
        _boundaryPointIndex1 = _boundaryPointIndex2 = -1;
        _selectedBoundaryPoly = (DataContext as MainViewModel)?.CurrentBoundary?.OuterBoundary;
        ShowDrawModeUI("Click first point on the boundary");

        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;
    }

    private void WholeBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var boundary = vm.CurrentBoundary;
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            vm.StatusMessage = "No boundary available";
            return;
        }

        // If there are inner boundaries, show a selection panel; otherwise use outer directly
        var boundaries = new List<(string name, BoundaryPolygon poly)>();
        boundaries.Add(("Outer Boundary", boundary.OuterBoundary));
        for (int i = 0; i < boundary.InnerBoundaries.Count; i++)
            boundaries.Add(($"Inner Boundary {i + 1}", boundary.InnerBoundaries[i]));

        if (boundaries.Count == 1)
        {
            CreateCurveFromBoundaryPoly(vm, boundary.OuterBoundary, "Outer Boundary");
            return;
        }

        // Show inline selection for multiple boundaries
        _drawMode = DrawMode.None;
        _drawPoints.Clear();
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;

        // Use inline confirmation as a simple selector - show each option
        // For simplicity, just create from outer and notify about inners
        ShowInlineConfirmation(
            "Select Boundary",
            $"Create curve from Outer Boundary? ({boundary.InnerBoundaries.Count} inner boundaries also available - select from track list to create from inner)",
            () => CreateCurveFromBoundaryPoly(vm, boundary.OuterBoundary, "Outer Boundary"));
    }

    private void CreateCurveFromBoundaryPoly(MainViewModel vm, BoundaryPolygon poly, string name)
    {
        var pts = poly.Points;
        var curvePoints = new List<Vec3>();
        for (int i = 0; i < pts.Count; i++)
            curvePoints.Add(new Vec3(pts[i].Easting, pts[i].Northing, pts[i].Heading));
        curvePoints.Add(new Vec3(pts[0].Easting, pts[0].Northing, pts[0].Heading));

        var track = new Models.Track.Track
        {
            Name = $"{name} Curve",
            Points = curvePoints,
            Type = TrackType.Curve,
            IsVisible = true
        };

        vm.SavedTracks.Add(track);
        vm.SelectedTrack = track;
        vm.StatusMessage = $"Created curve from {name} ({curvePoints.Count} points)";

        ExitDrawMode();
        ShowMainTabs();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private int FindNearestBoundaryPoint(double fieldE, double fieldN)
    {
        if (_selectedBoundaryPoly?.Points == null) return -1;

        var pts = _selectedBoundaryPoly.Points;
        double minDist = double.MaxValue;
        int bestIdx = -1;

        for (int i = 0; i < pts.Count; i++)
        {
            double dx = pts[i].Easting - fieldE;
            double dy = pts[i].Northing - fieldN;
            double dist = dx * dx + dy * dy;
            if (dist < minDist)
            {
                minDist = dist;
                bestIdx = i;
            }
        }
        return bestIdx;
    }

    private List<Vec3> ExtractBoundarySegment(int idx1, int idx2)
    {
        if (_selectedBoundaryPoly?.Points == null) return new();
        var pts = _selectedBoundaryPoly.Points;
        int count = pts.Count;

        // Walk from idx1 to idx2 in forward direction
        var forward = new List<Vec3>();
        int i = idx1;
        while (true)
        {
            var p = pts[i];
            forward.Add(new Vec3(p.Easting, p.Northing, p.Heading));
            if (i == idx2) break;
            i = (i + 1) % count;
            if (forward.Count > count + 1) break; // Safety
        }

        // Walk from idx1 to idx2 in reverse direction
        var reverse = new List<Vec3>();
        i = idx1;
        while (true)
        {
            var p = pts[i];
            reverse.Add(new Vec3(p.Easting, p.Northing, p.Heading));
            if (i == idx2) break;
            i = (i - 1 + count) % count;
            if (reverse.Count > count + 1) break;
        }

        // Return the shorter path
        return forward.Count <= reverse.Count ? forward : reverse;
    }

    private void ShowDrawModeUI(string instruction)
    {
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
        var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = instruction;
        if (pointCountText != null) pointCountText.Text = "Points: 0";
        if (finishPanel != null) finishPanel.IsVisible = _drawMode == DrawMode.Curve;
        if (createPanel != null) createPanel.IsVisible = false;

        UpdatePreview();
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_transformValid) return;
        if (DataContext is not MainViewModel) return;

        var pos = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));

        // In preview mode, check if clicking near an existing point to drag it
        bool isPreview = _drawMode == DrawMode.ABLinePreview || _drawMode == DrawMode.BoundaryLinePreview
                         || _drawMode == DrawMode.BoundaryCurvePreview;
        if (isPreview || (_drawMode == DrawMode.Curve && _drawPoints.Count >= 2))
        {
            for (int i = 0; i < _drawPoints.Count; i++)
            {
                var ptCanvas = ToCanvasPoint(_drawPoints[i].Easting, _drawPoints[i].Northing);
                double dist = Math.Sqrt(Math.Pow(pos.X - ptCanvas.X, 2) + Math.Pow(pos.Y - ptCanvas.Y, 2));
                if (dist < 20) // 20px hit radius
                {
                    _dragPointIndex = i;
                    _isDragging = true;
                    e.Handled = true;
                    return;
                }
            }
        }

        if (_drawMode == DrawMode.None || isPreview) return;

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
                UpdateDrawModeInfo();

                var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
                var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
                if (createPanel != null) createPanel.IsVisible = true;
                if (finishPanel != null) finishPanel.IsVisible = false;
            }
        }
        else if (_drawMode == DrawMode.Curve)
        {
            if (pointCountText != null) pointCountText.Text = $"Points: {_drawPoints.Count}";
            if (instrText != null) instrText.Text = $"Click more points or Finish ({_drawPoints.Count} placed)";
        }
        else if (_drawMode == DrawMode.BoundaryLine || _drawMode == DrawMode.BoundaryCurve)
        {
            // Snap to nearest boundary vertex
            int nearIdx = FindNearestBoundaryPoint(fieldE, fieldN);
            if (nearIdx < 0) { UpdatePreview(); e.Handled = true; return; }

            var bPt = _selectedBoundaryPoly!.Points[nearIdx];
            // Replace the free-form point with the snapped boundary point
            _drawPoints[^1] = new Vec3(bPt.Easting, bPt.Northing, _drawPoints[^1].Heading);

            if (_drawPoints.Count == 1)
            {
                _boundaryPointIndex1 = nearIdx;
                if (instrText != null) instrText.Text = "Click second point on the boundary";
                if (pointCountText != null) pointCountText.Text = "Point 1 set";
            }
            else if (_drawPoints.Count >= 2)
            {
                _boundaryPointIndex2 = nearIdx;

                // Recalculate headings
                double h = Math.Atan2(
                    _drawPoints[1].Easting - _drawPoints[0].Easting,
                    _drawPoints[1].Northing - _drawPoints[0].Northing);
                _drawPoints[0] = new Vec3(_drawPoints[0].Easting, _drawPoints[0].Northing, h);
                _drawPoints[1] = new Vec3(_drawPoints[1].Easting, _drawPoints[1].Northing, h);

                // For boundary curve, extract the segment between the two points
                if (_drawMode == DrawMode.BoundaryCurve)
                {
                    var segment = ExtractBoundarySegment(_boundaryPointIndex1, _boundaryPointIndex2);
                    _drawPoints.Clear();
                    _drawPoints.AddRange(segment);
                    _drawMode = DrawMode.BoundaryCurvePreview;
                }
                else
                {
                    _drawMode = DrawMode.BoundaryLinePreview;
                }

                UpdateDrawModeInfo();
                var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
                var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
                if (createPanel != null) createPanel.IsVisible = true;
                if (finishPanel != null) finishPanel.IsVisible = false;
            }
        }

        UpdatePreview();
        e.Handled = true;
    }

    private void CreateAB_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || _drawPoints.Count < 2) return;

        if (_drawMode == DrawMode.BoundaryCurvePreview)
        {
            // Extend curve straight past endpoints
            var points = new List<Vec3>(_drawPoints);
            double ext = 200; // 200m extension

            if (points.Count >= 2)
            {
                var s0 = points[0];
                var s1 = points[1];
                double sdx = s0.Easting - s1.Easting;
                double sdy = s0.Northing - s1.Northing;
                double slen = Math.Sqrt(sdx * sdx + sdy * sdy);
                if (slen > 0.01)
                    points.Insert(0, new Vec3(s0.Easting + sdx / slen * ext, s0.Northing + sdy / slen * ext, s0.Heading));

                var e0 = points[^2];
                var e1 = points[^1];
                double edx = e1.Easting - e0.Easting;
                double edy = e1.Northing - e0.Northing;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen > 0.01)
                    points.Add(new Vec3(e1.Easting + edx / elen * ext, e1.Northing + edy / elen * ext, e1.Heading));
            }

            var track = new Models.Track.Track
            {
                Name = $"BndCurve {DateTime.Now:HH:mm:ss}",
                Points = points,
                Type = TrackType.Curve,
                IsVisible = true
            };
            vm.SavedTracks.Add(track);
            vm.SelectedTrack = track;
            vm.StatusMessage = "Created boundary curve";
        }
        else
        {
            // AB line (free draw or boundary line)
            CreateABLineFromPoints(vm);
            return; // CreateABLineFromPoints handles cleanup
        }

        ExitDrawMode();
        ShowMainTabs();
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
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
            var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");

            // Reset AB preview state if we went back below 2 points
            if (_drawMode == DrawMode.ABLinePreview)
            {
                _drawMode = DrawMode.ABLine;
                if (createPanel != null) createPanel.IsVisible = false;
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

    private void Canvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging || _dragPointIndex < 0 || !_transformValid) return;

        var pos = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));
        double fieldE = (pos.X - _offsetX) / _scale + _minE;
        double fieldN = (_canvasHeight - pos.Y - _offsetY) / _scale + _minN;

        // Recalculate heading
        double heading = 0;
        if (_drawPoints.Count >= 2)
        {
            int otherIdx = _dragPointIndex == 0 ? 1 : 0;
            var other = _drawPoints[otherIdx];
            if (_dragPointIndex == 0)
                heading = Math.Atan2(other.Easting - fieldE, other.Northing - fieldN);
            else
                heading = Math.Atan2(fieldE - _drawPoints[0].Easting, fieldN - _drawPoints[0].Northing);
        }

        _drawPoints[_dragPointIndex] = new Vec3(fieldE, fieldN, heading);

        // Update both points' headings for AB lines
        if (_drawPoints.Count == 2)
        {
            double h = Math.Atan2(
                _drawPoints[1].Easting - _drawPoints[0].Easting,
                _drawPoints[1].Northing - _drawPoints[0].Northing);
            _drawPoints[0] = new Vec3(_drawPoints[0].Easting, _drawPoints[0].Northing, h);
            _drawPoints[1] = new Vec3(_drawPoints[1].Easting, _drawPoints[1].Northing, h);
        }

        UpdateDrawModeInfo();
        UpdatePreview();
        e.Handled = true;
    }

    private void Canvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _dragPointIndex = -1;
            e.Handled = true;
        }
    }

    private Point ToCanvasPoint(double e, double n)
    {
        return new Point(
            (e - _minE) * _scale + _offsetX,
            _canvasHeight - ((n - _minN) * _scale + _offsetY));
    }

    private void UpdateDrawModeInfo()
    {
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");

        if ((_drawMode == DrawMode.ABLinePreview || _drawMode == DrawMode.BoundaryLinePreview) && _drawPoints.Count >= 2)
        {
            double headingDeg = Math.Atan2(
                _drawPoints[1].Easting - _drawPoints[0].Easting,
                _drawPoints[1].Northing - _drawPoints[0].Northing) * 180.0 / Math.PI;
            if (headingDeg < 0) headingDeg += 360;

            if (instrText != null) instrText.Text = $"Heading: {headingDeg:F1} - drag points or Create";
            if (pointCountText != null) pointCountText.Text = "A and B set";
        }
        else if (_drawMode == DrawMode.BoundaryCurvePreview)
        {
            if (instrText != null) instrText.Text = "Boundary curve - drag ends or Create";
            if (pointCountText != null) pointCountText.Text = "";
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

        // Draw boundary polygon (yellow - matches legacy)
        var boundaryPoly = new Polygon
        {
            Stroke = new SolidColorBrush(Color.FromRgb(240, 200, 40)),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(20, 240, 200, 40)),
            Points = pts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
        };
        canvas.Children.Add(boundaryPoly);

        // Draw headland (bright green dashed - matches legacy)
        if (vm.HasHeadland && vm.CurrentHeadlandLineForPreview != null)
        {
            var headPts = vm.CurrentHeadlandLineForPreview;
            if (headPts.Count >= 3)
            {
                var headlandPoly = new Polygon
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(50, 220, 50)),
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 },
                    Points = headPts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
                };
                canvas.Children.Add(headlandPoly);
            }
        }

        // Draw tracks (all gray/inactive during draw mode)
        bool isDrawing = _drawMode != DrawMode.None;
        foreach (var track in vm.SavedTracks)
        {
            if (track.Points.Count < 2) continue;

            bool isSelected = !isDrawing && track == vm.SelectedTrack;
            // Selected: white (legacy active boundary), inactive: gray
            var color = new SolidColorBrush(isSelected
                ? Color.FromRgb(220, 220, 255)
                : Color.FromRgb(120, 120, 140));

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

                AddMarker(canvas, first, new SolidColorBrush(Color.FromRgb(218, 165, 32)), "A"); // Gold
                AddMarker(canvas, last, new SolidColorBrush(Color.FromRgb(65, 105, 225)), "B");  // RoyalBlue
            }
        }

        // Draw points being placed in draw mode
        if (_drawMode != DrawMode.None && _drawPoints.Count > 0)
        {
            // Draw preview line between points
            if (_drawPoints.Count >= 2)
            {
                var previewPoints = _drawPoints.Select(p => ToCanvas(p.Easting, p.Northing)).ToList();

                // For AB/boundary line preview, extend the line
                if (_drawMode == DrawMode.ABLinePreview || _drawMode == DrawMode.BoundaryLinePreview)
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
                // For boundary curve preview, extend straight past endpoints
                else if (_drawMode == DrawMode.BoundaryCurvePreview && _drawPoints.Count >= 2)
                {
                    double ext = Math.Max(_rangeE, _rangeN) * 2;

                    // Extend from start: direction from point[1] to point[0]
                    var s0 = _drawPoints[0];
                    var s1 = _drawPoints[1];
                    double sdx = s0.Easting - s1.Easting;
                    double sdy = s0.Northing - s1.Northing;
                    double slen = Math.Sqrt(sdx * sdx + sdy * sdy);
                    if (slen > 0.01)
                    {
                        previewPoints.Insert(0, ToCanvas(
                            s0.Easting + sdx / slen * ext,
                            s0.Northing + sdy / slen * ext));
                    }

                    // Extend from end: direction from point[-2] to point[-1]
                    var e0 = _drawPoints[^2];
                    var e1 = _drawPoints[^1];
                    double edx = e1.Easting - e0.Easting;
                    double edy = e1.Northing - e0.Northing;
                    double elen = Math.Sqrt(edx * edx + edy * edy);
                    if (elen > 0.01)
                    {
                        previewPoints.Add(ToCanvas(
                            e1.Easting + edx / elen * ext,
                            e1.Northing + edy / elen * ext));
                    }
                }

                var drawLine = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 130, 0)),  // Orange preview
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                    Points = previewPoints
                };
                canvas.Children.Add(drawLine);
            }

            // Draw point markers (only endpoints for boundary curves with many points)
            bool isLinearMode = _drawMode == DrawMode.ABLine || _drawMode == DrawMode.ABLinePreview
                                || _drawMode == DrawMode.BoundaryLine || _drawMode == DrawMode.BoundaryLinePreview;
            bool isCurvePreview = _drawMode == DrawMode.BoundaryCurvePreview;

            for (int i = 0; i < _drawPoints.Count; i++)
            {
                // For boundary curve preview, only show first and last markers
                if (isCurvePreview && i > 0 && i < _drawPoints.Count - 1) continue;

                var pt = ToCanvas(_drawPoints[i].Easting, _drawPoints[i].Northing);
                string? label = null;

                if (isLinearMode)
                    label = i == 0 ? "A" : "B";
                else if (isCurvePreview)
                    label = i == 0 ? "Start" : "End";

                IBrush fill = i == 0
                    ? new SolidColorBrush(Color.FromRgb(218, 165, 32))   // Gold (A/Start)
                    : (i == _drawPoints.Count - 1
                        ? new SolidColorBrush(Color.FromRgb(65, 105, 225))  // RoyalBlue (B/End)
                        : Brushes.Yellow);
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
