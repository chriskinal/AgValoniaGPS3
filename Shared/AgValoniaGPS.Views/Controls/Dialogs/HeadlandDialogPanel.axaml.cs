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
using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AgValoniaGPS.ViewModels;
using AgValoniaGPS.Models;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class HeadlandDialogPanel : UserControl
{
    private DrawingContextMapControl? _mapControl;
    private MainViewModel? _viewModel;

    public HeadlandDialogPanel()
    {
        InitializeComponent();

        // Subscribe to DataContext changes to hook up ViewModel
        DataContextChanged += OnDataContextChanged;

        // Get map control reference after visual tree is built
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _mapControl = this.FindControl<DrawingContextMapControl>("HeadlandMapControl");
        Debug.WriteLine($"[HeadlandDialogPanel] AttachedToVisualTree - MapControl found: {_mapControl != null}");

        // Subscribe to map click events for point selection
        if (_mapControl != null)
        {
            _mapControl.MapClicked += OnMapClicked;
        }

        // If we already have a ViewModel, sync now
        if (_viewModel != null)
        {
            SyncMapWithViewModel(_viewModel);
        }
    }

    private void OnMapClicked(object? sender, MapClickEventArgs e)
    {
        Debug.WriteLine($"[HeadlandDialogPanel] Map clicked at ({e.Easting:F1}, {e.Northing:F1})");
        _viewModel?.HandleHeadlandMapClick(e.Easting, e.Northing);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel != null)
        {
            Debug.WriteLine($"[HeadlandDialogPanel] DataContext set to MainViewModel");

            // Subscribe to property changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Initial sync if map control is ready
            if (_mapControl != null)
            {
                SyncMapWithViewModel(_viewModel);
            }
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.CurrentBoundary):
            case nameof(MainViewModel.CurrentHeadlandLine):
            case nameof(MainViewModel.HeadlandPreviewLine):
            case nameof(MainViewModel.HeadlandSelectedMarkers):
            case nameof(MainViewModel.IsHeadlandCurveMode):
                if (_viewModel.State.UI.IsHeadlandDialogVisible)
                {
                    SyncMapWithViewModel(_viewModel);
                }
                break;
        }
    }

    private void SyncMapWithViewModel(MainViewModel vm)
    {
        if (_mapControl == null)
        {
            Debug.WriteLine($"[HeadlandDialogPanel] SyncMapWithViewModel - MapControl is null!");
            return;
        }

        // Get boundary from the active field via the ViewModel's helper
        var boundary = vm.CurrentBoundary;
        var headlandLine = vm.CurrentHeadlandLine;
        var headlandPreview = vm.HeadlandPreviewLine;
        var selectionMarkers = vm.HeadlandSelectedMarkers;

        Debug.WriteLine($"[HeadlandDialogPanel] SyncMapWithViewModel - Boundary: {boundary != null}, OuterPoints: {boundary?.OuterBoundary?.Points.Count ?? 0}, HeadlandLine: {headlandLine?.Count ?? 0}, Preview: {headlandPreview?.Count ?? 0}, Markers: {selectionMarkers?.Count ?? 0}");

        _mapControl.SetBoundary(boundary);
        _mapControl.SetHeadlandLine(headlandLine);
        _mapControl.SetHeadlandPreview(headlandPreview);
        _mapControl.SetSelectionMarkers(selectionMarkers);
        _mapControl.SetHeadlandVisible(true);

        // Set clip visualization - either curved path or straight line
        var clipPath = vm.HeadlandClipPath;
        var clipLine = vm.HeadlandClipLine;

        if (clipPath != null && clipPath.Count >= 2)
        {
            // Curve mode: show path along headland
            _mapControl.SetClipPath(clipPath);
            _mapControl.SetClipLine(null, null);
        }
        else if (clipLine.HasValue)
        {
            // Line mode: show straight clip line
            _mapControl.SetClipPath(null);
            _mapControl.SetClipLine(clipLine.Value.Start, clipLine.Value.End);
        }
        else
        {
            // No selection
            _mapControl.SetClipPath(null);
            _mapControl.SetClipLine(null, null);
        }
    }

    private void CenterViewOnBoundary(MainViewModel vm)
    {
        if (_mapControl == null)
        {
            Debug.WriteLine($"[HeadlandDialogPanel] CenterViewOnBoundary - MapControl is null!");
            return;
        }

        var boundary = vm.CurrentBoundary;
        if (boundary?.OuterBoundary == null || !boundary.OuterBoundary.IsValid)
        {
            Debug.WriteLine($"[HeadlandDialogPanel] CenterViewOnBoundary - No valid boundary");
            return;
        }

        // Calculate bounding box
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var point in boundary.OuterBoundary.Points)
        {
            minX = Math.Min(minX, point.Easting);
            maxX = Math.Max(maxX, point.Easting);
            minY = Math.Min(minY, point.Northing);
            maxY = Math.Max(maxY, point.Northing);
        }

        // Center and calculate zoom to fit
        double centerX = (minX + maxX) / 2.0;
        double centerY = (minY + maxY) / 2.0;
        double width = maxX - minX;
        double height = maxY - minY;

        // Add padding (20%)
        double padding = 1.2;
        double viewSize = Math.Max(width, height) * padding;

        // Calculate zoom (base view is 200m, so zoom = 200 / viewSize)
        double zoom = viewSize > 0 ? 200.0 / viewSize : 1.0;
        zoom = Math.Clamp(zoom, 0.1, 10.0);

        Debug.WriteLine($"[HeadlandDialogPanel] CenterViewOnBoundary - Center: ({centerX:F1}, {centerY:F1}), Zoom: {zoom:F2}");
        _mapControl.SetCamera(centerX, centerY, zoom, 0);
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clicking the backdrop closes the dialog
        if (_viewModel != null)
        {
            _viewModel.CloseHeadlandDialogCommand?.Execute(null);
        }
    }
}
