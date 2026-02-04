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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Mapsui.Rendering.Skia;
using NetTopologySuite.Geometries;
using NtsPoint = NetTopologySuite.Geometries.Point;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class BoundaryMapDialogPanel : UserControl
{
    private WritableLayer? _pointsLayer;
    private WritableLayer? _polygonLayer;
    private bool _isDrawingMode;
    private bool _mapInitialized;
    private readonly List<(double Lat, double Lon)> _boundaryPoints = new();

    public BoundaryMapDialogPanel()
    {
        InitializeComponent();

        // Initialize map when the control becomes visible
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible) && IsVisible && !_mapInitialized)
        {
            SetupMap();
            _mapInitialized = true;
        }
    }

    private void SetupMap()
    {
        var map = new Mapsui.Map();

        // Explicitly set the map CRS to SphericalMercator (EPSG:3857)
        // This ensures all layers use consistent coordinate system
        map.CRS = "EPSG:3857";

        // Test with different tile providers to diagnose offset issue
        // Set to false to use ESRI instead of Google
        var useGoogle = false; // Testing ESRI to compare with Google

        if (useGoogle)
        {
            // Google Satellite tiles (lyrs=s for satellite, y for hybrid with labels)
            var googleSatelliteUrl = "https://mt1.google.com/vt/lyrs=s&x={x}&y={y}&z={z}";
            var googleTileSource = new HttpTileSource(
                new GlobalSphericalMercator(),
                googleSatelliteUrl,
                name: "Google Satellite");
            map.Layers.Add(new TileLayer(googleTileSource) { Name = "Satellite" });
            Debug.WriteLine("[BoundaryMap] Using Google Satellite tiles");
        }
        else
        {
            // ESRI World Imagery (may have georeferencing offsets)
            var esriSatelliteUrl = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}";
            var esriTileSource = new HttpTileSource(
                new GlobalSphericalMercator(),
                esriSatelliteUrl,
                name: "Esri World Imagery");
            map.Layers.Add(new TileLayer(esriTileSource) { Name = "Satellite" });
            Debug.WriteLine("[BoundaryMap] Using ESRI World Imagery tiles");
        }

        // Create layer for polygon (drawn below points)
        _polygonLayer = new WritableLayer
        {
            Name = "Polygon",
            Style = new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(52, 152, 219, 50)), // Semi-transparent blue
                Line = new Mapsui.Styles.Pen(new Mapsui.Styles.Color(255, 255, 255, 255), 3) // White outline
            }
        };
        map.Layers.Add(_polygonLayer);

        // Create layer for boundary points
        _pointsLayer = new WritableLayer
        {
            Name = "Points",
            Style = new SymbolStyle
            {
                Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(231, 76, 60, 255)), // Red
                Outline = new Mapsui.Styles.Pen(new Mapsui.Styles.Color(192, 57, 43, 255), 2),
                SymbolScale = 0.5
            }
        };
        map.Layers.Add(_pointsLayer);

        // Get initial position from ViewModel
        double lat = 39.8283; // Default to US center
        double lon = -98.5795;

        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            if (Math.Abs(vm.BoundaryMapCenterLatitude) > 0.01 || Math.Abs(vm.BoundaryMapCenterLongitude) > 0.01)
            {
                lat = vm.BoundaryMapCenterLatitude;
                lon = vm.BoundaryMapCenterLongitude;
            }
        }

        // Convert to SphericalMercator
        var center = SphericalMercator.FromLonLat(lon, lat);
        Console.WriteLine($"[BoundaryMap] Input WGS84: lat={lat:F8}, lon={lon:F8}");
        Console.WriteLine($"[BoundaryMap] Mercator center: x={center.x:F2}, y={center.y:F2}");

        // Verify round-trip conversion
        var verify = SphericalMercator.ToLonLat(center.x, center.y);
        Console.WriteLine($"[BoundaryMap] Round-trip WGS84: lat={verify.lat:F8}, lon={verify.lon:F8}");
        Console.WriteLine($"[BoundaryMap] Round-trip error: lat={Math.Abs(lat - verify.lat) * 111132:F2}m, lon={Math.Abs(lon - verify.lon) * 111132 * Math.Cos(lat * Math.PI / 180):F2}m");

        map.Navigator.CenterOnAndZoomTo(new MPoint(center.x, center.y), map.Navigator.Resolutions[16]);

        MapControl.Map = map;

        // Disable all debug/performance overlays and widgets
        map.Widgets.Clear();

        // Handle map clicks via pointer events
        MapControl.PointerPressed += OnMapPointerPressed;

        // Handle pointer movement for coordinate display
        MapControl.PointerMoved += OnPointerMoved;
    }

    private void OnMapPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_isDrawingMode)
            return;

        var point = e.GetCurrentPoint(MapControl);
        if (point.Properties.IsLeftButtonPressed)
        {
            var viewport = MapControl.Map.Navigator.Viewport;
            var worldPos = viewport.ScreenToWorldXY(point.Position.X, point.Position.Y);

            // DEBUG: Log click position vs viewport bounds
            Debug.WriteLine($"[Click] Screen: ({point.Position.X:F1}, {point.Position.Y:F1}), Viewport: {viewport.Width:F1}x{viewport.Height:F1}");
            Debug.WriteLine($"[Click] Screen Y as fraction of height: {point.Position.Y / viewport.Height:F3}");
            Debug.WriteLine($"[Click] World pos: ({worldPos.worldX:F2}, {worldPos.worldY:F2})");

            // Convert from SphericalMercator to WGS84
            var lonLat = SphericalMercator.ToLonLat(worldPos.worldX, worldPos.worldY);

            AddBoundaryPoint(lonLat.lat, lonLat.lon);

            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var position = e.GetPosition(MapControl);
        var viewport = MapControl.Map.Navigator.Viewport;
        var worldPos = viewport.ScreenToWorldXY(position.X, position.Y);

        // Convert from SphericalMercator to WGS84
        var lonLat = SphericalMercator.ToLonLat(worldPos.worldX, worldPos.worldY);

        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.BoundaryMapCoordinateText = $"Lat: {lonLat.lat:F6}, Lon: {lonLat.lon:F6}";
        }
    }

    private void AddBoundaryPoint(double lat, double lon)
    {
        _boundaryPoints.Add((lat, lon));
        Debug.WriteLine($"[BoundaryPoint] Added point #{_boundaryPoints.Count}: ({lat:F8}, {lon:F8})");
        Console.WriteLine($"[BoundaryPoint] #{_boundaryPoints.Count}: lat={lat:F6}, lon={lon:F6}");

        // Add point marker
        var mercator = SphericalMercator.FromLonLat(lon, lat);
        var point = new GeometryFeature(new NtsPoint(mercator.x, mercator.y));
        _pointsLayer?.Add(point);

        UpdatePolygon();
        UpdateUI();

        MapControl.Refresh();
    }

    private void UpdatePolygon()
    {
        _polygonLayer?.Clear();

        if (_boundaryPoints.Count >= 3)
        {
            // Create polygon from points
            var coordinates = new List<Coordinate>();
            foreach (var (lat, lon) in _boundaryPoints)
            {
                var mercator = SphericalMercator.FromLonLat(lon, lat);
                coordinates.Add(new Coordinate(mercator.x, mercator.y));
            }
            // Close the polygon
            var first = _boundaryPoints[0];
            var firstMercator = SphericalMercator.FromLonLat(first.Lon, first.Lat);
            coordinates.Add(new Coordinate(firstMercator.x, firstMercator.y));

            var ring = new LinearRing(coordinates.ToArray());
            var polygon = new Polygon(ring);
            var feature = new GeometryFeature(polygon);
            _polygonLayer?.Add(feature);
        }
        else if (_boundaryPoints.Count >= 2)
        {
            // Draw line between points
            var coordinates = new List<Coordinate>();
            foreach (var (lat, lon) in _boundaryPoints)
            {
                var mercator = SphericalMercator.FromLonLat(lon, lat);
                coordinates.Add(new Coordinate(mercator.x, mercator.y));
            }
            var line = new LineString(coordinates.ToArray());
            var feature = new GeometryFeature(line);
            _polygonLayer?.Add(feature);
        }
    }

    private void UpdateUI()
    {
        var count = _boundaryPoints.Count;

        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.BoundaryMapPointCount = count;
            vm.BoundaryMapCanSave = count >= 3;
        }

        BtnUndo.IsEnabled = count > 0;
        BtnClear.IsEnabled = count > 0;
    }

    private void BtnDraw_Click(object? sender, RoutedEventArgs e)
    {
        _isDrawingMode = !_isDrawingMode;

        if (_isDrawingMode)
        {
            BtnDraw.Classes.Add("active");
            BtnDrawText.Text = "Stop";
            MapControl.Cursor = new Cursor(StandardCursorType.Cross);
        }
        else
        {
            BtnDraw.Classes.Remove("active");
            BtnDrawText.Text = "Draw";
            MapControl.Cursor = Cursor.Default;
        }
    }

    private void BtnUndo_Click(object? sender, RoutedEventArgs e)
    {
        if (_boundaryPoints.Count == 0)
            return;

        _boundaryPoints.RemoveAt(_boundaryPoints.Count - 1);

        // Rebuild points layer
        _pointsLayer?.Clear();
        foreach (var (lat, lon) in _boundaryPoints)
        {
            var mercator = SphericalMercator.FromLonLat(lon, lat);
            var point = new GeometryFeature(new NtsPoint(mercator.x, mercator.y));
            _pointsLayer?.Add(point);
        }

        UpdatePolygon();
        UpdateUI();
        MapControl.Refresh();
    }

    private void BtnClear_Click(object? sender, RoutedEventArgs e)
    {
        _boundaryPoints.Clear();
        _pointsLayer?.Clear();
        _polygonLayer?.Clear();
        UpdateUI();
        MapControl.Refresh();
    }

    private async void BtnSave_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_boundaryPoints.Count < 3)
                return;

            if (DataContext is not AgValoniaGPS.ViewModels.MainViewModel vm)
                return;

            var includeBackground = vm.BoundaryMapIncludeBackground;
            string? backgroundPath = null;
            double nwLat = 0, nwLon = 0, seLat = 0, seLon = 0;
            double mercMinX = 0, mercMaxX = 0, mercMinY = 0, mercMaxY = 0;

            // Capture background if requested
            if (includeBackground)
            {
                BtnSave.IsEnabled = false;

                try
                {
                    var result = await CaptureBackgroundImageAsync();
                    if (result != null)
                    {
                        backgroundPath = result.Value.Path;
                        nwLat = result.Value.NwLat;
                        nwLon = result.Value.NwLon;
                        seLat = result.Value.SeLat;
                        seLon = result.Value.SeLon;
                        mercMinX = result.Value.MercMinX;
                        mercMaxX = result.Value.MercMaxX;
                        mercMinY = result.Value.MercMinY;
                        mercMaxY = result.Value.MercMaxY;
                    }
                }
                finally
                {
                    BtnSave.IsEnabled = true;
                }
            }

            // Copy boundary points to ViewModel
            vm.BoundaryMapResultPoints.Clear();
            foreach (var (lat, lon) in _boundaryPoints)
            {
                vm.BoundaryMapResultPoints.Add((lat, lon));
            }

            vm.BoundaryMapResultBackgroundPath = backgroundPath;
            vm.BoundaryMapResultNwLat = nwLat;
            vm.BoundaryMapResultNwLon = nwLon;
            vm.BoundaryMapResultSeLat = seLat;
            vm.BoundaryMapResultSeLon = seLon;
            vm.BoundaryMapResultMercMinX = mercMinX;
            vm.BoundaryMapResultMercMaxX = mercMaxX;
            vm.BoundaryMapResultMercMinY = mercMinY;
            vm.BoundaryMapResultMercMaxY = mercMaxY;

            // Execute confirm command
            vm.ConfirmBoundaryMapDialogCommand?.Execute(null);

            // Reset state for next use
            ResetState();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BoundaryMap] Save failed: {ex.Message}");
        }
    }

    private async Task<(string Path, double NwLat, double NwLon, double SeLat, double SeLon,
        double MercMinX, double MercMaxX, double MercMinY, double MercMaxY)?> CaptureBackgroundImageAsync()
    {
        try
        {
            // Get current viewport - this defines what area to capture
            var viewport = MapControl.Map.Navigator.Viewport;

            // Compute Mercator bounds from viewport center and resolution
            double halfWidthMeters = (viewport.Width / 2.0) * viewport.Resolution;
            double halfHeightMeters = (viewport.Height / 2.0) * viewport.Resolution;
            double mercMinX = viewport.CenterX - halfWidthMeters;
            double mercMaxX = viewport.CenterX + halfWidthMeters;
            double mercMinY = viewport.CenterY - halfHeightMeters;
            double mercMaxY = viewport.CenterY + halfHeightMeters;

            // Convert extent corners to WGS84
            var nw = SphericalMercator.ToLonLat(mercMinX, mercMaxY);  // NW = west X, north Y
            var se = SphericalMercator.ToLonLat(mercMaxX, mercMinY);  // SE = east X, south Y

            // Export the map to a bitmap
            var tempDir = Path.Combine(Path.GetTempPath(), "AgValoniaGPS_Mapsui");
            Directory.CreateDirectory(tempDir);
            var savedBackgroundPath = Path.Combine(tempDir, "BackPic.png");

            // Hide drawing layers before capture
            if (_pointsLayer != null) _pointsLayer.Enabled = false;
            if (_polygonLayer != null) _polygonLayer.Enabled = false;
            MapControl.Refresh();

            // Wait for tile layer to finish loading
            var tileLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "Satellite") as TileLayer;
            if (tileLayer != null)
            {
                // Wait for tiles to load (max 10 seconds)
                int waitCount = 0;
                while (tileLayer.Busy && waitCount < 100)
                {
                    await Task.Delay(100);
                    waitCount++;
                }
            }

            // Small additional delay for rendering to complete
            await Task.Delay(200);

            // Get the actual pixel size accounting for DPI scaling
            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;

            // Use Mapsui's MapRenderer to render directly - this ensures correct coordinate alignment
            // because it uses Mapsui's internal rendering pipeline, not Avalonia's
            var map = MapControl.Map;
            var renderer = new MapRenderer();

            using var bitmapStream = renderer.RenderToBitmapStream(map, pixelDensity: (float)scaling);

            if (bitmapStream != null && bitmapStream.Length > 0)
            {
                bitmapStream.Position = 0;
                using var fileStream = File.Create(savedBackgroundPath);
                bitmapStream.CopyTo(fileStream);
            }
            else
            {
                Debug.WriteLine("[Capture] MapRenderer returned null/empty - falling back to RenderTargetBitmap");
                // Fallback to Avalonia rendering if MapRenderer fails
                var pixelWidth = (int)(viewport.Width * scaling);
                var pixelHeight = (int)(viewport.Height * scaling);
                var pixelSize = new PixelSize(pixelWidth, pixelHeight);
                var dpi = new Vector(96 * scaling, 96 * scaling);
                var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
                renderTarget.Render(MapControl);
                renderTarget.Save(savedBackgroundPath);
            }

            // Re-enable drawing layers
            if (_pointsLayer != null) _pointsLayer.Enabled = true;
            if (_polygonLayer != null) _polygonLayer.Enabled = true;
            MapControl.Refresh();

            // Create geo-reference file content (includes Mercator bounds)
            var geoPath = Path.Combine(tempDir, "BackPic.txt");
            var geoContent = $"$BackPic\ntrue\n{nw.lat.ToString(CultureInfo.InvariantCulture)}\n{nw.lon.ToString(CultureInfo.InvariantCulture)}\n{se.lat.ToString(CultureInfo.InvariantCulture)}\n{se.lon.ToString(CultureInfo.InvariantCulture)}\n{mercMinX.ToString(CultureInfo.InvariantCulture)}\n{mercMaxX.ToString(CultureInfo.InvariantCulture)}\n{mercMinY.ToString(CultureInfo.InvariantCulture)}\n{mercMaxY.ToString(CultureInfo.InvariantCulture)}";
            File.WriteAllText(geoPath, geoContent);

            return (savedBackgroundPath, nw.lat, nw.lon, se.lat, se.lon, mercMinX, mercMaxX, mercMinY, mercMaxY);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error capturing background: {ex.Message}");

            // Re-enable drawing layers on error
            if (_pointsLayer != null) _pointsLayer.Enabled = true;
            if (_polygonLayer != null) _polygonLayer.Enabled = true;
            MapControl.Refresh();

            return null;
        }
    }

    private void ResetState()
    {
        _boundaryPoints.Clear();
        _pointsLayer?.Clear();
        _polygonLayer?.Clear();
        _isDrawingMode = false;
        BtnDraw.Classes.Remove("active");
        BtnDrawText.Text = "Draw";
        MapControl.Cursor = Cursor.Default;
        BtnUndo.IsEnabled = false;
        BtnClear.IsEnabled = false;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelBoundaryMapDialogCommand?.Execute(null);
            ResetState();
        }
        e.Handled = true;
    }
}
