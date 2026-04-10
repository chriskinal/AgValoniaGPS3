// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldBuilderDialogPanel : UserControl
{
    public FieldBuilderDialogPanel()
    {
        InitializeComponent();
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible) && IsVisible)
        {
            // Reset to main tabs view and redraw
            ShowMainTabs();
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
        // Switch to Add Track sub-panel
        var tabs = this.FindControl<TabControl>("MainTabs");
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (tabs != null) tabs.IsVisible = false;
        if (addPanel != null) addPanel.IsVisible = true;
    }

    private void BackBtn_Click(object? sender, RoutedEventArgs e)
    {
        ShowMainTabs();
        // Refresh preview after returning (track may have been added)
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdatePreview, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void ShowMainTabs()
    {
        var tabs = this.FindControl<TabControl>("MainTabs");
        var addPanel = this.FindControl<Border>("AddTrackPanel");
        if (tabs != null) tabs.IsVisible = true;
        if (addPanel != null) addPanel.IsVisible = false;
    }

    private void UpdatePreview()
    {
        var canvas = this.FindControl<Canvas>("BoundaryPreview");
        if (canvas == null || DataContext is not MainViewModel vm) return;

        canvas.Children.Clear();

        var boundary = vm.CurrentBoundary?.OuterBoundary;
        if (boundary?.Points == null || boundary.Points.Count < 3) return;

        double cw = canvas.Bounds.Width > 0 ? canvas.Bounds.Width : 300;
        double ch = canvas.Bounds.Height > 0 ? canvas.Bounds.Height : 300;
        if (cw < 10 || ch < 10) return;

        var pts = boundary.Points;

        // Calculate bounds
        double minE = pts.Min(p => p.Easting);
        double maxE = pts.Max(p => p.Easting);
        double minN = pts.Min(p => p.Northing);
        double maxN = pts.Max(p => p.Northing);
        double rangeE = maxE - minE;
        double rangeN = maxN - minN;
        if (rangeE < 1) rangeE = 1;
        if (rangeN < 1) rangeN = 1;

        // Scale to fit canvas with margin
        double margin = 20;
        double scaleX = (cw - margin * 2) / rangeE;
        double scaleY = (ch - margin * 2) / rangeN;
        double scale = Math.Min(scaleX, scaleY);
        double offsetX = (cw - rangeE * scale) / 2;
        double offsetY = (ch - rangeN * scale) / 2;

        Point ToCanvas(double e, double n) => new Point(
            (e - minE) * scale + offsetX,
            ch - ((n - minN) * scale + offsetY)
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
            var trackLine = new Polyline
            {
                Stroke = new SolidColorBrush(isSelected
                    ? Color.FromRgb(50, 200, 255)
                    : Color.FromRgb(100, 130, 160)),
                StrokeThickness = isSelected ? 3 : 1.5,
                Points = track.Points.Select(p => ToCanvas(p.Easting, p.Northing)).ToList()
            };
            canvas.Children.Add(trackLine);

            // A/B markers for selected track
            if (isSelected && track.Points.Count >= 2)
            {
                var first = ToCanvas(track.Points[0].Easting, track.Points[0].Northing);
                var last = ToCanvas(track.Points[track.Points.Count - 1].Easting,
                                    track.Points[track.Points.Count - 1].Northing);

                var markerA = new Ellipse { Width = 10, Height = 10, Fill = Brushes.LimeGreen };
                Canvas.SetLeft(markerA, first.X - 5);
                Canvas.SetTop(markerA, first.Y - 5);
                canvas.Children.Add(markerA);

                var markerB = new Ellipse { Width = 10, Height = 10, Fill = Brushes.Red };
                Canvas.SetLeft(markerB, last.X - 5);
                Canvas.SetTop(markerB, last.Y - 5);
                canvas.Children.Add(markerB);
            }
        }
    }
}
