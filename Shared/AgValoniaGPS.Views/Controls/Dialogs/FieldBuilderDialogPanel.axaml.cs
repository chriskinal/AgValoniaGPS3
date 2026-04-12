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
    private enum DrawMode { None, ABLine, ABLinePreview, Curve, BoundaryLine, BoundaryLinePreview, BoundaryCurve, BoundaryCurvePreview, APlus, APlusPreview, HeadlandLine, HeadlandCurve, HeadlandPreview }
    private DrawMode _drawMode = DrawMode.None;
    private readonly List<Vec3> _drawPoints = new();
    private int _boundaryPointIndex1 = -1;
    private int _boundaryPointIndex2 = -1;
    private BoundaryPolygon? _selectedBoundaryPoly;

    // Drag state
    private int _dragPointIndex = -1;
    private bool _isDragging;

    // Arrow drag state for headland extend/shrink
    private enum ArrowDragType { None, Start, End }
    private ArrowDragType _arrowDrag = ArrowDragType.None;
    private Point _arrowStartCanvasPos;
    private Point _arrowEndCanvasPos;
    private double _headlandStartExt = 50;
    private double _headlandEndExt = 50;

    // Edit backup (restored on cancel)
    private Models.Headland.HeadlandSegment? _editBackupSegment;
    private Models.Track.Track? _editBackupTrack;

    // Inline confirmation/input
    private Action? _inlineConfirmAction;
    private MainViewModel? _viewModel;

    // Theme detection
    private bool IsLightTheme => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;

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

        var headingInput = this.FindControl<TextBox>("HeadingInput");
        if (headingInput != null)
            headingInput.TextChanged += HeadingInput_TextChanged;

        // Deselect tracks/headland segments when switching tabs
        var mainTabs = this.FindControl<TabControl>("MainTabs");
        if (mainTabs != null)
            mainTabs.SelectionChanged += MainTabs_SelectionChanged;
    }

    private void MainTabs_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var mainTabs = this.FindControl<TabControl>("MainTabs");
        if (mainTabs == null) return;

        if (mainTabs.SelectedIndex == 1) // Headland tab
        {
            // Deselect headland segment if there's only one (allow re-select)
            // Clear track visual highlight handled by isDrawing/onHeadlandTab
        }
        else // Tracks or Tram tab
        {
            // Deselect headland segments
            vm.SelectedHeadlandSegment = null;
        }

        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
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
        if (IsVisible && (e.PropertyName == nameof(MainViewModel.SelectedTrack)
            || e.PropertyName == nameof(MainViewModel.HasHeadland)
            || e.PropertyName == nameof(MainViewModel.CurrentHeadlandLineForPreview)
            || e.PropertyName == nameof(MainViewModel.HeadlandStatusText)
            || e.PropertyName == nameof(MainViewModel.SelectedHeadlandSegment)))
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
            // Rebuild headland from segments (clears legacy headland if no segments)
            if (DataContext is MainViewModel openVm)
                openVm.BuildHeadlandFromSegments();
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

    private void AddHeadlandLine_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.HeadlandLine;
        _drawPoints.Clear();
        _boundaryPointIndex1 = _boundaryPointIndex2 = -1;
        _headlandStartExt = 50;
        _headlandEndExt = 50;
        _selectedBoundaryPoly = (DataContext as MainViewModel)?.CurrentBoundary?.OuterBoundary;
        ShowDrawModeUI("Click first point on boundary");
    }

    private void AddHeadlandCurve_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.HeadlandCurve;
        _drawPoints.Clear();
        _boundaryPointIndex1 = _boundaryPointIndex2 = -1;
        _headlandStartExt = 50;
        _headlandEndExt = 50;
        _selectedBoundaryPoly = (DataContext as MainViewModel)?.CurrentBoundary?.OuterBoundary;
        ShowDrawModeUI("Click first point on boundary");
    }

    private void AddHeadlandBoundary_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var boundary = vm.CurrentBoundary?.OuterBoundary;
        if (boundary?.Points == null || boundary.Points.Count < 3)
        {
            vm.StatusMessage = "No boundary available";
            return;
        }

        // Load boundary points and enter preview mode
        _drawMode = DrawMode.HeadlandPreview;
        _drawPoints.Clear();
        _selectedBoundaryPoly = boundary;

        foreach (var pt in boundary.Points)
            _drawPoints.Add(new Vec3(pt.Easting, pt.Northing, pt.Heading));
        _drawPoints.Add(new Vec3(boundary.Points[0].Easting, boundary.Points[0].Northing, boundary.Points[0].Heading));

        _boundaryPointIndex1 = 0;
        _boundaryPointIndex2 = boundary.Points.Count - 1;

        // Show draw panel with offset input
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
        var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
        var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
        var headingInput = this.FindControl<TextBox>("HeadingInput");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = "Adjust offset distance, then Create";
        if (pointCountText != null) pointCountText.Text = "";
        if (createPanel != null) createPanel.IsVisible = true;
        if (finishPanel != null) finishPanel.IsVisible = false;

        if (headingPanel != null) headingPanel.IsVisible = true;
        if (headingInput != null)
        {
            headingInput.Text = vm.HeadlandDistance.ToString("F1");
            headingInput.Focus();
        }
        var headingLabel = headingPanel?.Children.OfType<TextBlock>().FirstOrDefault();
        if (headingLabel != null) headingLabel.Text = "Offset:";
        var degLabel = headingPanel?.Children.OfType<TextBlock>().LastOrDefault();
        if (degLabel != null) degLabel.Text = "m";

        SetCanvasStatus("Set offset distance, then Create");
        UpdatePreview();
    }

    private void EditHeadlandSegment_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedHeadlandSegment == null) return;

        var seg = vm.SelectedHeadlandSegment;
        _editBackupSegment = seg; // Save for cancel restore

        // Load segment into draw mode for editing
        _drawMode = DrawMode.HeadlandPreview;
        _drawPoints.Clear();
        _drawPoints.AddRange(seg.BoundaryPoints);
        _boundaryPointIndex1 = seg.BoundaryStartIndex;
        _boundaryPointIndex2 = seg.BoundaryEndIndex;
        _headlandStartExt = seg.StartExtension;
        _headlandEndExt = seg.EndExtension;
        _selectedBoundaryPoly = vm.CurrentBoundary?.OuterBoundary;

        // Remove the segment (will be re-added when Create is clicked)
        vm.HeadlandSegments.Remove(seg);
        vm.SelectedHeadlandSegment = null;

        // Show draw panel with offset input
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
        var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
        var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
        var headingInput = this.FindControl<TextBox>("HeadingInput");
        var extPanel = this.FindControl<StackPanel>("ExtendShrinkPanel");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = "Edit headland line - adjust offset and endpoints";
        if (pointCountText != null) pointCountText.Text = "";
        if (createPanel != null) createPanel.IsVisible = true;
        if (finishPanel != null) finishPanel.IsVisible = false;
        if (extPanel != null) extPanel.IsVisible = true;

        if (headingPanel != null) headingPanel.IsVisible = true;
        if (headingInput != null)
        {
            headingInput.Text = seg.Offset.ToString("F1");
            headingInput.Focus();
        }
        var headingLabel = headingPanel?.Children.OfType<TextBlock>().FirstOrDefault();
        if (headingLabel != null) headingLabel.Text = "Offset:";
        var degLabel = headingPanel?.Children.OfType<TextBlock>().LastOrDefault();
        if (degLabel != null) degLabel.Text = "m";

        SetCanvasStatus("Edit headland line");
        UpdatePreview();
    }

    private void EditTrack_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTrack == null) return;

        var track = vm.SelectedTrack;
        _editBackupTrack = track; // Save for cancel restore

        // Load track into AB line preview for editing
        _drawMode = track.Points.Count == 2 ? DrawMode.ABLinePreview : DrawMode.Curve;
        _drawPoints.Clear();
        _drawPoints.AddRange(track.Points);
        _selectedBoundaryPoly = vm.CurrentBoundary?.OuterBoundary;

        // Remove the track (will be re-added when Create is clicked)
        vm.SavedTracks.Remove(track);
        vm.SelectedTrack = null;

        // Show draw panel
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
        var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (createPanel != null) createPanel.IsVisible = true;
        if (finishPanel != null) finishPanel.IsVisible = false;

        var tabs = this.FindControl<TabControl>("MainTabs");
        if (tabs != null) tabs.IsVisible = false;

        if (track.Points.Count == 2)
        {
            if (instrText != null) instrText.Text = "Edit AB line - drag points or Create";
            if (pointCountText != null) pointCountText.Text = "A and B set";
            UpdateDrawModeInfo();
        }
        else
        {
            if (instrText != null) instrText.Text = "Edit curve - drag points, then Create";
            if (pointCountText != null) pointCountText.Text = $"{_drawPoints.Count} points";
        }

        SetCanvasStatus("Edit track");
        UpdatePreview();
    }

    private void DeleteHeadlandSegment_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.SelectedHeadlandSegment == null) return;
        vm.HeadlandSegments.Remove(vm.SelectedHeadlandSegment);
        vm.SelectedHeadlandSegment = null;
        vm.BuildHeadlandFromSegments();
        UpdatePreview();
    }

    private void ExtendStart_Click(object? sender, RoutedEventArgs e)
    {
        if (_drawMode != DrawMode.HeadlandPreview || _selectedBoundaryPoly == null) return;
        if (_boundaryPointIndex1 < 0) return;

        var pts = _selectedBoundaryPoly.Points;
        int count = pts.Count;
        // Move start index one step backward along boundary
        _boundaryPointIndex1 = (_boundaryPointIndex1 - 1 + count) % count;

        var newPt = pts[_boundaryPointIndex1];
        _drawPoints.Insert(0, new Vec3(newPt.Easting, newPt.Northing, newPt.Heading));
        UpdatePreview();
    }

    private void ShrinkStart_Click(object? sender, RoutedEventArgs e)
    {
        if (_drawMode != DrawMode.HeadlandPreview || _drawPoints.Count <= 2) return;
        _drawPoints.RemoveAt(0);
        if (_selectedBoundaryPoly != null)
        {
            int count = _selectedBoundaryPoly.Points.Count;
            _boundaryPointIndex1 = (_boundaryPointIndex1 + 1) % count;
        }
        UpdatePreview();
    }

    private void ExtendEnd_Click(object? sender, RoutedEventArgs e)
    {
        if (_drawMode != DrawMode.HeadlandPreview || _selectedBoundaryPoly == null) return;
        if (_boundaryPointIndex2 < 0) return;

        var pts = _selectedBoundaryPoly.Points;
        int count = pts.Count;
        // Move end index one step forward along boundary
        _boundaryPointIndex2 = (_boundaryPointIndex2 + 1) % count;

        var newPt = pts[_boundaryPointIndex2];
        _drawPoints.Add(new Vec3(newPt.Easting, newPt.Northing, newPt.Heading));
        UpdatePreview();
    }

    private void ShrinkEnd_Click(object? sender, RoutedEventArgs e)
    {
        if (_drawMode != DrawMode.HeadlandPreview || _drawPoints.Count <= 2) return;
        _drawPoints.RemoveAt(_drawPoints.Count - 1);
        if (_selectedBoundaryPoly != null)
        {
            int count = _selectedBoundaryPoly.Points.Count;
            _boundaryPointIndex2 = (_boundaryPointIndex2 - 1 + count) % count;
        }
        UpdatePreview();
    }

    private void SetCanvasStatus(string? text)
    {
        var banner = this.FindControl<Border>("CanvasStatusBanner");
        var statusText = this.FindControl<TextBlock>("CanvasStatusText");
        if (banner != null) banner.IsVisible = text != null;
        if (statusText != null) statusText.Text = text ?? "";
    }

    private void CreateALine_Click(object? sender, RoutedEventArgs e)
    {
        _drawMode = DrawMode.APlus;
        _drawPoints.Clear();
        ShowDrawModeUI("Click a point on the map");

        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (addPanel != null) addPanel.IsVisible = false;

        // Show heading input
        var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
        if (headingPanel != null) headingPanel.IsVisible = false; // Hidden until point placed
    }

    private void ShowDrawModeUI(string instruction)
    {
        SetCanvasStatus(instruction);
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        var instrText = this.FindControl<TextBlock>("DrawInstructionText");
        var pointCountText = this.FindControl<TextBlock>("DrawPointCountText");
        var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
        var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");

        var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");

        if (drawPanel != null) drawPanel.IsVisible = true;
        if (instrText != null) instrText.Text = instruction;
        if (pointCountText != null) pointCountText.Text = "Points: 0";
        if (finishPanel != null) finishPanel.IsVisible = _drawMode == DrawMode.Curve;
        if (createPanel != null) createPanel.IsVisible = false;
        if (headingPanel != null) headingPanel.IsVisible = false;

        UpdatePreview();
    }

    private void Canvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_transformValid) return;
        if (DataContext is not MainViewModel) return;

        var pos = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));

        // Check arrow drag for headland extend/shrink (higher priority than point drag)
        if (_drawMode == DrawMode.HeadlandPreview && _arrowStartCanvasPos != default && _arrowEndCanvasPos != default)
        {
            double distStart = Math.Sqrt(Math.Pow(pos.X - _arrowStartCanvasPos.X, 2) + Math.Pow(pos.Y - _arrowStartCanvasPos.Y, 2));
            double distEnd = Math.Sqrt(Math.Pow(pos.X - _arrowEndCanvasPos.X, 2) + Math.Pow(pos.Y - _arrowEndCanvasPos.Y, 2));

            if (distStart < 25)
            {
                _arrowDrag = ArrowDragType.Start;
                _isDragging = true;
                e.Handled = true;
                return;
            }
            if (distEnd < 25)
            {
                _arrowDrag = ArrowDragType.End;
                _isDragging = true;
                e.Handled = true;
                return;
            }
        }

        // In preview mode, check if clicking near an existing point to drag it
        bool isPreview = _drawMode == DrawMode.ABLinePreview || _drawMode == DrawMode.BoundaryLinePreview
                         || _drawMode == DrawMode.BoundaryCurvePreview || _drawMode == DrawMode.APlusPreview
                         || _drawMode == DrawMode.HeadlandPreview;
        if (isPreview || (_drawMode == DrawMode.Curve && _drawPoints.Count >= 2))
        {
            // For boundary curve preview, only allow dragging first and last points
            var draggableIndices = (_drawMode == DrawMode.BoundaryCurvePreview && _drawPoints.Count > 2)
                ? new[] { 0, _drawPoints.Count - 1 }
                : Enumerable.Range(0, _drawPoints.Count).ToArray();

            foreach (int i in draggableIndices)
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
                SetCanvasStatus("Click point B");
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
            SetCanvasStatus($"Click next point ({_drawPoints.Count} placed)");
        }
        else if (_drawMode == DrawMode.BoundaryLine || _drawMode == DrawMode.BoundaryCurve
                 || _drawMode == DrawMode.HeadlandLine || _drawMode == DrawMode.HeadlandCurve)
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
                SetCanvasStatus("Click second point on boundary");
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

                // For curve modes, extract the boundary segment
                if (_drawMode == DrawMode.BoundaryCurve || _drawMode == DrawMode.HeadlandCurve)
                {
                    var segment = ExtractBoundarySegment(_boundaryPointIndex1, _boundaryPointIndex2);
                    _drawPoints.Clear();
                    _drawPoints.AddRange(segment);
                    _drawMode = _drawMode == DrawMode.HeadlandCurve ? DrawMode.HeadlandPreview : DrawMode.BoundaryCurvePreview;
                }
                else if (_drawMode == DrawMode.HeadlandLine)
                {
                    _drawMode = DrawMode.HeadlandPreview;
                }
                else
                {
                    _drawMode = DrawMode.BoundaryLinePreview;
                }

                // Show offset input for headland modes
                if (_drawMode == DrawMode.HeadlandPreview)
                {
                    var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
                    var headingInput = this.FindControl<TextBox>("HeadingInput");
                    if (headingPanel != null) headingPanel.IsVisible = true;
                    if (headingInput != null)
                    {
                        headingInput.Text = ((DataContext as MainViewModel)?.HeadlandDistance ?? 12).ToString("F1");
                        headingInput.Focus();
                    }
                    // Relabel heading input as offset
                    var headingLabel = headingPanel?.Children.OfType<TextBlock>().FirstOrDefault();
                    if (headingLabel != null) headingLabel.Text = "Offset:";
                    var degLabel = headingPanel?.Children.OfType<TextBlock>().LastOrDefault();
                    if (degLabel != null) degLabel.Text = "m";
                }

                // Show extend/shrink for headland preview
                var extPanel = this.FindControl<StackPanel>("ExtendShrinkPanel");
                if (extPanel != null) extPanel.IsVisible = _drawMode == DrawMode.HeadlandPreview;

                UpdateDrawModeInfo();
                var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
                var finishPanel = this.FindControl<StackPanel>("FinishDrawBtnPanel");
                if (createPanel != null) createPanel.IsVisible = true;
                if (finishPanel != null) finishPanel.IsVisible = false;
            }
        }
        else if (_drawMode == DrawMode.APlus)
        {
            if (_drawPoints.Count == 1)
            {
                // Point placed, show heading input
                _drawMode = DrawMode.APlusPreview;
                SetCanvasStatus("Enter heading and click Create");
                if (instrText != null) instrText.Text = "Enter heading angle";
                if (pointCountText != null) pointCountText.Text = "Point set";

                var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
                var headingInput = this.FindControl<TextBox>("HeadingInput");
                var createPanel = this.FindControl<StackPanel>("CreateABBtnPanel");
                if (headingPanel != null) headingPanel.IsVisible = true;
                if (headingInput != null) { headingInput.Text = "0"; headingInput.Focus(); headingInput.SelectAll(); }
                if (createPanel != null) createPanel.IsVisible = true;

                // Generate preview line at 0 degrees
                UpdateAPlusPreview();
            }
        }

        UpdatePreview();
        e.Handled = true;
    }

    private void UpdateAPlusPreview()
    {
        var headingInput = this.FindControl<TextBox>("HeadingInput");
        if (headingInput == null || _drawPoints.Count < 1) return;

        if (!double.TryParse(headingInput.Text, out double headingDeg)) return;
        double headingRad = headingDeg * Math.PI / 180.0;

        // Keep only the clicked point, regenerate A/B from heading
        var origin = _drawPoints[0];
        double ext = 200;
        var a = new Vec3(origin.Easting - Math.Sin(headingRad) * ext, origin.Northing - Math.Cos(headingRad) * ext, headingRad);
        var b = new Vec3(origin.Easting + Math.Sin(headingRad) * ext, origin.Northing + Math.Cos(headingRad) * ext, headingRad);

        while (_drawPoints.Count > 1) _drawPoints.RemoveAt(_drawPoints.Count - 1);
        _drawPoints[0] = new Vec3(origin.Easting, origin.Northing, headingRad);
        _drawPoints.Add(b);
        // Insert A before origin for the extended line
        _drawPoints.Insert(0, a);

        SetCanvasStatus($"Heading: {headingDeg:F1} - click Create");
    }

    private void HeadingInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_drawMode == DrawMode.APlusPreview)
        {
            if (_drawPoints.Count == 0) return;
            var origin = _drawPoints.Count == 3 ? _drawPoints[1] : _drawPoints[0];
            _drawPoints.Clear();
            _drawPoints.Add(origin);
            UpdateAPlusPreview();
            UpdatePreview();
        }
        else if (_drawMode == DrawMode.HeadlandPreview)
        {
            // Offset changed - redraw preview
            UpdatePreview();
        }
    }

    private void CreateAB_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm || _drawPoints.Count < 2) return;

        if (_drawMode == DrawMode.APlusPreview)
        {
            // Create from the 3 points (extended A, origin, extended B)
            var headingInput = this.FindControl<TextBox>("HeadingInput");
            double headingDeg = 0;
            if (headingInput != null) double.TryParse(headingInput.Text, out headingDeg);

            var posA = _drawPoints[0];
            var posB = _drawPoints[^1];
            var track = new Models.Track.Track
            {
                Name = $"A+ {headingDeg:F1}",
                Points = new List<Vec3> { posA, posB },
                Type = TrackType.ABLine,
                IsVisible = true
            };
            vm.SavedTracks.Add(track);
            vm.SelectedTrack = track;
            vm.StatusMessage = $"Created A+ line at {headingDeg:F1}";

            ExitDrawMode();
            ShowMainTabs();
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
            return;
        }

        if (_drawMode == DrawMode.HeadlandPreview)
        {
            // Create headland segment from the drawn points + offset
            var headingInput = this.FindControl<TextBox>("HeadingInput");
            double offset = vm.HeadlandDistance;
            if (headingInput != null) double.TryParse(headingInput.Text, out offset);

            bool isCurve = _drawPoints.Count > 2;
            var segment = new Models.Headland.HeadlandSegment
            {
                Name = $"{(isCurve ? "Curve" : "Line")} {vm.HeadlandSegments.Count + 1}",
                Type = isCurve ? Models.Headland.HeadlandSegmentType.Curve : Models.Headland.HeadlandSegmentType.Line,
                Offset = offset,
                BoundaryStartIndex = _boundaryPointIndex1,
                BoundaryEndIndex = _boundaryPointIndex2,
                BoundaryIndex = 0,
                BoundaryPoints = new List<Vec3>(_drawPoints),
                StartExtension = _headlandStartExt,
                EndExtension = _headlandEndExt
            };

            vm.ComputeSegmentOffset(segment);
            vm.HeadlandSegments.Add(segment);
            vm.SelectedHeadlandSegment = segment;
            vm.BuildHeadlandFromSegments();

            ExitDrawMode();
            ShowMainTabs();
            // Switch to headland tab
            var mainTabs = this.FindControl<TabControl>("MainTabs");
            if (mainTabs != null) mainTabs.SelectedIndex = 1;
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
            return;
        }

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

            // Reset preview states if we went back below 2 points
            if (_drawMode == DrawMode.ABLinePreview)
            {
                _drawMode = DrawMode.ABLine;
                if (createPanel != null) createPanel.IsVisible = false;
            }
            else if (_drawMode == DrawMode.HeadlandPreview)
            {
                // Reset back to picking mode
                _drawMode = _drawPoints.Count <= 2 ? DrawMode.HeadlandLine : DrawMode.HeadlandCurve;
                if (createPanel != null) createPanel.IsVisible = false;
                var extPanel = this.FindControl<StackPanel>("ExtendShrinkPanel");
                if (extPanel != null) extPanel.IsVisible = false;
                var headingPanel = this.FindControl<StackPanel>("HeadingInputPanel");
                if (headingPanel != null) headingPanel.IsVisible = false;
                SetCanvasStatus("Click point on boundary");
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
        // Arrow drag for headland extend/shrink - modifies extension lengths only
        if (_isDragging && _arrowDrag != ArrowDragType.None && _transformValid)
        {
            var pos2 = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));
            double fe = (pos2.X - _offsetX) / _scale + _minE;
            double fn = (_canvasHeight - pos2.Y - _offsetY) / _scale + _minN;

            if (_arrowDrag == ArrowDragType.Start && _drawPoints.Count >= 2)
            {
                // Distance from drag position to offset line start point
                var start = _drawPoints[0];
                var next = _drawPoints[1];
                double dx = start.Easting - next.Easting;
                double dy = start.Northing - next.Northing;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.01)
                {
                    // Project drag position onto the extension direction
                    double projDist = ((fe - start.Easting) * dx / len + (fn - start.Northing) * dy / len);
                    _headlandStartExt = Math.Max(0, projDist);
                }
            }
            else if (_arrowDrag == ArrowDragType.End && _drawPoints.Count >= 2)
            {
                var end = _drawPoints[^1];
                var prev = _drawPoints[^2];
                double dx = end.Easting - prev.Easting;
                double dy = end.Northing - prev.Northing;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > 0.01)
                {
                    double projDist = ((fe - end.Easting) * dx / len + (fn - end.Northing) * dy / len);
                    _headlandEndExt = Math.Max(0, projDist);
                }
            }

            UpdatePreview();
            e.Handled = true;
            return;
        }

        if (!_isDragging || _dragPointIndex < 0 || !_transformValid) return;

        var pos = e.GetPosition(this.FindControl<Canvas>("BoundaryPreview"));
        double fieldE = (pos.X - _offsetX) / _scale + _minE;
        double fieldN = (_canvasHeight - pos.Y - _offsetY) / _scale + _minN;

        // For headland and boundary modes, snap to nearest boundary vertex during drag
        bool snapToBoundary = _selectedBoundaryPoly != null &&
            (_drawMode == DrawMode.HeadlandPreview || _drawMode == DrawMode.BoundaryLinePreview
             || _drawMode == DrawMode.BoundaryCurvePreview);

        if (snapToBoundary)
        {
            int nearIdx = FindNearestBoundaryPoint(fieldE, fieldN);
            if (nearIdx >= 0)
            {
                var bPt = _selectedBoundaryPoly!.Points[nearIdx];
                fieldE = bPt.Easting;
                fieldN = bPt.Northing;
            }
        }

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
        // Arrow drag release
        if (_arrowDrag != ArrowDragType.None)
        {
            _arrowDrag = ArrowDragType.None;
            _isDragging = false;
            e.Handled = true;
            return;
        }

        // For boundary curve, snap endpoint to boundary and re-extract segment
        if (_isDragging && (_drawMode == DrawMode.BoundaryCurvePreview || _drawMode == DrawMode.HeadlandPreview) && _selectedBoundaryPoly != null && _drawPoints.Count > 2)
        {
            var draggedPt = _drawPoints[_dragPointIndex];
            int nearIdx = FindNearestBoundaryPoint(draggedPt.Easting, draggedPt.Northing);
            if (nearIdx >= 0)
            {
                // Determine which boundary indices to use
                if (_dragPointIndex == 0)
                    _boundaryPointIndex1 = nearIdx;
                else
                    _boundaryPointIndex2 = nearIdx;

                // Re-extract the segment
                var segment = ExtractBoundarySegment(_boundaryPointIndex1, _boundaryPointIndex2);
                _drawPoints.Clear();
                _drawPoints.AddRange(segment);
                UpdateDrawModeInfo();
                UpdatePreview();
            }
        }

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

            string msg = $"Heading: {headingDeg:F1} - drag points or Create";
            if (instrText != null) instrText.Text = msg;
            if (pointCountText != null) pointCountText.Text = "A and B set";
            SetCanvasStatus(msg);
        }
        else if (_drawMode == DrawMode.BoundaryCurvePreview)
        {
            string msg = "Drag endpoints or click Create";
            if (instrText != null) instrText.Text = msg;
            if (pointCountText != null) pointCountText.Text = "";
            SetCanvasStatus(msg);
        }
        else if (_drawMode == DrawMode.HeadlandPreview)
        {
            string msg = "Set offset distance, drag points, then Create";
            if (instrText != null) instrText.Text = msg;
            if (pointCountText != null) pointCountText.Text = "";
            SetCanvasStatus(msg);
        }
    }

    private void CancelDraw_Click(object? sender, RoutedEventArgs e)
    {
        bool wasHeadland = _drawMode == DrawMode.HeadlandLine || _drawMode == DrawMode.HeadlandCurve || _drawMode == DrawMode.HeadlandPreview;
        bool wasTrackEdit = _editBackupTrack != null;
        bool wasHeadlandEdit = _editBackupSegment != null;

        // Restore backup if editing was cancelled
        if (DataContext is MainViewModel vm)
        {
            if (_editBackupSegment != null)
            {
                vm.HeadlandSegments.Add(_editBackupSegment);
                vm.SelectedHeadlandSegment = _editBackupSegment;
                vm.BuildHeadlandFromSegments();
                _editBackupSegment = null;
            }
            if (_editBackupTrack != null)
            {
                vm.SavedTracks.Add(_editBackupTrack);
                vm.SelectedTrack = _editBackupTrack;
                _editBackupTrack = null;
            }
        }

        ExitDrawMode();

        if (wasHeadland || wasHeadlandEdit)
        {
            // Return to headland tab
            ShowMainTabs();
            var mainTabs = this.FindControl<TabControl>("MainTabs");
            if (mainTabs != null) mainTabs.SelectedIndex = 1;
        }
        else if (wasTrackEdit)
        {
            // Return to tracks tab
            ShowMainTabs();
        }
        else
        {
            // Return to add track panel
            var addPanel = this.FindControl<Border>("AddTrackPanel");
            var drawPanel = this.FindControl<Border>("DrawModePanel");
            if (addPanel != null) addPanel.IsVisible = true;
            if (drawPanel != null) drawPanel.IsVisible = false;
        }
        UpdatePreview();
    }

    private void ExitDrawMode()
    {
        _drawMode = DrawMode.None;
        _drawPoints.Clear();
        _isDragging = false;
        _dragPointIndex = -1;
        // Clear edit backups (successful create clears them; cancel restores before calling this)
        _editBackupSegment = null;
        _editBackupTrack = null;
        var drawPanel = this.FindControl<Border>("DrawModePanel");
        if (drawPanel != null) drawPanel.IsVisible = false;
        var extPanel = this.FindControl<StackPanel>("ExtendShrinkPanel");
        if (extPanel != null) extPanel.IsVisible = false;
        SetCanvasStatus(null);
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

        // Theme-aware colors
        bool light = IsLightTheme;
        var canvasBorder = this.FindControl<Border>("CanvasBorder");
        if (canvasBorder != null)
            canvasBorder.Background = new SolidColorBrush(light ? Color.FromRgb(245, 247, 250) : Color.FromRgb(26, 26, 46));

        var noFieldText = this.FindControl<TextBlock>("NoFieldText");
        if (noFieldText != null)
            noFieldText.Foreground = new SolidColorBrush(light ? Color.FromArgb(100, 0, 0, 0) : Color.FromArgb(96, 255, 255, 255));

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

        // Draw boundary polygon (red/orange solid)
        var boundaryColor = light ? Color.FromRgb(200, 100, 30) : Color.FromRgb(240, 160, 40);
        var boundaryPoly = new Polygon
        {
            Stroke = new SolidColorBrush(boundaryColor),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(light ? (byte)10 : (byte)15, boundaryColor.R, boundaryColor.G, boundaryColor.B)),
            Points = pts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
        };
        canvas.Children.Add(boundaryPoly);

        // Determine draw/headland tab state for highlighting
        var mainTabs = this.FindControl<TabControl>("MainTabs");
        bool onHeadlandTab = mainTabs is { IsVisible: true, SelectedIndex: 1 };
        bool isDrawing = _drawMode != DrawMode.None || onHeadlandTab;

        // Draw output headland path (yellow dashed, distinct from boundary)
        if (vm.HasHeadland && vm.CurrentHeadlandLineForPreview != null)
        {
            var headPts = vm.CurrentHeadlandLineForPreview;
            if (headPts.Count >= 3)
            {
                var headlandColor = light ? Color.FromRgb(200, 180, 0) : Color.FromRgb(255, 230, 50);
                var headlandPoly = new Polygon
                {
                    Stroke = new SolidColorBrush(headlandColor),
                    StrokeThickness = 4,
                    Points = headPts.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
                };
                canvas.Children.Add(headlandPoly);
            }
        }

        // Draw headland segments (offset lines with extensions, ON TOP of headland path)
        foreach (var seg in vm.HeadlandSegments)
        {
            if (seg.OffsetPoints.Count < 2) continue;
            bool segSelected = seg == vm.SelectedHeadlandSegment && _drawMode == DrawMode.None;
            var segColor = new SolidColorBrush(segSelected
                ? (light ? Color.FromRgb(0, 140, 0) : Color.FromRgb(80, 255, 80))
                : (light ? Color.FromRgb(30, 160, 30) : Color.FromRgb(50, 200, 50)));

            // Build full line with extensions
            var segPts = new List<Point>();
            if (seg.StartExtension > 0 && seg.OffsetPoints.Count >= 2)
            {
                var s0 = seg.OffsetPoints[0]; var s1 = seg.OffsetPoints[1];
                double sdx = s0.Easting - s1.Easting, sdy = s0.Northing - s1.Northing;
                double slen = Math.Sqrt(sdx * sdx + sdy * sdy);
                if (slen > 0.01)
                    segPts.Add(ToCanvas(s0.Easting + sdx / slen * seg.StartExtension, s0.Northing + sdy / slen * seg.StartExtension));
            }
            segPts.AddRange(seg.OffsetPoints.Select(p => ToCanvas(p.Easting, p.Northing)));
            if (seg.EndExtension > 0 && seg.OffsetPoints.Count >= 2)
            {
                var e0 = seg.OffsetPoints[^2]; var e1 = seg.OffsetPoints[^1];
                double edx = e1.Easting - e0.Easting, edy = e1.Northing - e0.Northing;
                double elen = Math.Sqrt(edx * edx + edy * edy);
                if (elen > 0.01)
                    segPts.Add(ToCanvas(e1.Easting + edx / elen * seg.EndExtension, e1.Northing + edy / elen * seg.EndExtension));
            }

            var segLine = new Polyline
            {
                Stroke = segColor,
                StrokeThickness = segSelected ? 3 : 1.5,
                StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                Points = segPts
            };
            canvas.Children.Add(segLine);

            // Endpoint markers for selected segment
            if (segSelected)
            {
                AddMarker(canvas, segPts[0], new SolidColorBrush(Color.FromRgb(218, 165, 32)), null, light);
                AddMarker(canvas, segPts[^1], new SolidColorBrush(Color.FromRgb(65, 105, 225)), null, light);
            }
        }

        // Draw tracks
        foreach (var track in vm.SavedTracks)
        {
            if (track.Points.Count < 2) continue;

            bool isSelected = !isDrawing && track == vm.SelectedTrack;
            var color = new SolidColorBrush(isSelected
                ? (light ? Color.FromRgb(30, 60, 200) : Color.FromRgb(220, 220, 255))
                : (light ? Color.FromRgb(140, 140, 160) : Color.FromRgb(120, 120, 140)));

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

                AddMarker(canvas, first, new SolidColorBrush(Color.FromRgb(218, 165, 32)), "A", light);
                AddMarker(canvas, last, new SolidColorBrush(Color.FromRgb(65, 105, 225)), "B", light);
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
                if (_drawMode == DrawMode.ABLinePreview || _drawMode == DrawMode.BoundaryLinePreview || _drawMode == DrawMode.APlusPreview)
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
                    Stroke = new SolidColorBrush(light ? Color.FromRgb(220, 80, 0) : Color.FromRgb(255, 130, 0)),
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 6, 3 },
                    Points = previewPoints
                };
                canvas.Children.Add(drawLine);
            }

            // Draw point markers (only endpoints for boundary curves with many points)
            bool isLinearMode = _drawMode == DrawMode.ABLine || _drawMode == DrawMode.ABLinePreview
                                || _drawMode == DrawMode.BoundaryLine || _drawMode == DrawMode.BoundaryLinePreview
                                || _drawMode == DrawMode.APlus || _drawMode == DrawMode.APlusPreview;
            bool isCurvePreview = _drawMode == DrawMode.BoundaryCurvePreview
                || (_drawMode == DrawMode.HeadlandPreview && _drawPoints.Count > 2);

            for (int i = 0; i < _drawPoints.Count; i++)
            {
                // For curve previews, only show first and last markers
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
                AddMarker(canvas, pt, fill, label, light);
            }
        }

        // Draw headland offset preview during HeadlandPreview mode
        if (_drawMode == DrawMode.HeadlandPreview && _drawPoints.Count >= 2)
        {
            // Compute temporary offset from the draw points
            var tempSeg = new Models.Headland.HeadlandSegment
            {
                Type = _drawPoints.Count > 2 ? Models.Headland.HeadlandSegmentType.Curve : Models.Headland.HeadlandSegmentType.Line,
                BoundaryPoints = new List<Vec3>(_drawPoints)
            };

            // Read offset from input
            var headingInput = this.FindControl<TextBox>("HeadingInput");
            if (headingInput != null && double.TryParse(headingInput.Text, out double previewOffset))
                tempSeg.Offset = previewOffset;
            else
                tempSeg.Offset = 12;

            if (DataContext is MainViewModel previewVm)
                previewVm.ComputeSegmentOffset(tempSeg);

            if (tempSeg.OffsetPoints.Count >= 2)
            {
                var offsetColor = new SolidColorBrush(light ? Color.FromRgb(0, 160, 0) : Color.FromRgb(100, 255, 100));

                // Build offset line with straight extensions at both ends
                var offsetPts = new List<Point>();

                // Start extension
                if (_headlandStartExt > 0)
                {
                    var s0 = tempSeg.OffsetPoints[0];
                    var s1 = tempSeg.OffsetPoints[1];
                    double sdx = s0.Easting - s1.Easting;
                    double sdy = s0.Northing - s1.Northing;
                    double slen = Math.Sqrt(sdx * sdx + sdy * sdy);
                    if (slen > 0.01)
                        offsetPts.Add(ToCanvas(s0.Easting + sdx / slen * _headlandStartExt, s0.Northing + sdy / slen * _headlandStartExt));
                }

                offsetPts.AddRange(tempSeg.OffsetPoints.Select(p => ToCanvas(p.Easting, p.Northing)));

                // End extension
                if (_headlandEndExt > 0)
                {
                    var e0 = tempSeg.OffsetPoints[^2];
                    var e1 = tempSeg.OffsetPoints[^1];
                    double edx = e1.Easting - e0.Easting;
                    double edy = e1.Northing - e0.Northing;
                    double elen = Math.Sqrt(edx * edx + edy * edy);
                    if (elen > 0.01)
                        offsetPts.Add(ToCanvas(e1.Easting + edx / elen * _headlandEndExt, e1.Northing + edy / elen * _headlandEndExt));
                }

                var offsetLine = new Polyline
                {
                    Stroke = offsetColor,
                    StrokeThickness = 3,
                    Points = offsetPts
                };
                canvas.Children.Add(offsetLine);

                // Draw arrow markers at extension endpoints
                // Arrows sit at the very ends of the extended offset line
                Point arrowStart = offsetPts[0];
                Point arrowEnd = offsetPts[^1];

                _arrowStartCanvasPos = arrowStart;
                _arrowEndCanvasPos = arrowEnd;

                // Draw arrow triangles
                var arrowBrush = new SolidColorBrush(light ? Color.FromRgb(0, 120, 0) : Color.FromRgb(150, 255, 150));
                AddArrowMarker(canvas, arrowStart, arrowBrush);
                AddArrowMarker(canvas, arrowEnd, arrowBrush);
            }
            else
            {
                _arrowStartCanvasPos = default;
                _arrowEndCanvasPos = default;
            }
        }

        // Draw headland clip selection markers
        var clipMarkers = vm.HeadlandSelectedMarkers;
        if (clipMarkers != null && clipMarkers.Count > 0)
        {
            for (int i = 0; i < clipMarkers.Count; i++)
            {
                var pt = ToCanvas(clipMarkers[i].Easting, clipMarkers[i].Northing);
                AddMarker(canvas, pt, Brushes.Magenta, i == 0 ? "1" : "2", light);
            }

            // Draw clip line between the two markers
            if (clipMarkers.Count == 2)
            {
                var p1 = ToCanvas(clipMarkers[0].Easting, clipMarkers[0].Northing);
                var p2 = ToCanvas(clipMarkers[1].Easting, clipMarkers[1].Northing);
                var clipLine = new Avalonia.Controls.Shapes.Line
                {
                    StartPoint = p1,
                    EndPoint = p2,
                    Stroke = Brushes.Magenta,
                    StrokeThickness = 2,
                    StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 4 }
                };
                canvas.Children.Add(clipLine);
            }
        }
    }

    private static void AddMarker(Canvas canvas, Point pt, IBrush fill, string? label, bool light = false)
    {
        var marker = new Ellipse
        {
            Width = 12, Height = 12,
            Fill = fill,
            Stroke = light ? Brushes.Black : Brushes.White,
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
                Foreground = light ? Brushes.Black : Brushes.White
            };
            Canvas.SetLeft(text, pt.X + 8);
            Canvas.SetTop(text, pt.Y - 8);
            canvas.Children.Add(text);
        }
    }

    private static void AddArrowMarker(Canvas canvas, Point pt, IBrush fill)
    {
        double size = 16;
        var diamond = new Polygon
        {
            Fill = fill,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            Points = new List<Point>
            {
                new(pt.X, pt.Y - size / 2),
                new(pt.X + size / 2, pt.Y),
                new(pt.X, pt.Y + size / 2),
                new(pt.X - size / 2, pt.Y)
            }
        };
        canvas.Children.Add(diamond);
    }
}
