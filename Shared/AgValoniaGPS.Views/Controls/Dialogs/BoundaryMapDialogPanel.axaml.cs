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
using System.ComponentModel;
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
using BruTile;
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
using NetTopologySuite.Geometries;
using NtsPoint = NetTopologySuite.Geometries.Point;
using SkiaSharp;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class BoundaryMapDialogPanel : UserControl
{
    private WritableLayer? _pointsLayer;
    private WritableLayer? _polygonLayer;
    private WritableLayer? _existingBoundaryLayer;
    private WritableLayer? _tractorLayer;
    private GeometryFeature? _tractorFeature;
    private AgValoniaGPS.ViewModels.MainViewModel? _trackedVm;
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
        if (e.Property.Name == nameof(IsVisible))
        {
            if (IsVisible)
            {
                if (!_mapInitialized)
                {
                    SetupMap();
                    _mapInitialized = true;
                }
                UpdateExistingBoundaryLayer();
                AttachTractorTracking();
            }
            else
            {
                DetachTractorTracking();
            }
        }
    }

    private void SetupMap()
    {
        var map = new Mapsui.Map();

        // Explicitly set the map CRS to SphericalMercator (EPSG:3857)
        // This ensures all layers use consistent coordinate system
        map.CRS = "EPSG:3857";

        // Bing Maps aerial imagery via Virtual Earth tile servers.
        // Schema bumped to L20 (default tops out at L19); Bing serves L20 across
        // most populated regions and the imagery is generally sharper than Esri
        // in agricultural areas.
        var bingSatelliteUrl = "https://ecn.t0.tiles.virtualearth.net/tiles/a{quadkey}.jpeg?g=587";
        var bingTileSource = new HttpTileSource(
            new GlobalSphericalMercator(YAxis.OSM, 0, 20),
            bingSatelliteUrl,
            name: "Bing Satellite");
        map.Layers.Add(new TileLayer(bingTileSource) { Name = "Satellite" });
        Debug.WriteLine("[BoundaryMap] Using Bing Satellite tiles");

        // Create layer for existing boundary reference (below active drawing layers)
        // Style = null so per-feature styles are used instead of a layer default
        _existingBoundaryLayer = new WritableLayer { Name = "ExistingBoundaries", Style = null };
        map.Layers.Add(_existingBoundaryLayer);

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

        // Tractor position marker — green dot, on top so it stays visible
        // over the boundary the user is drawing.
        _tractorLayer = new WritableLayer
        {
            Name = "Tractor",
            Style = new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(46, 204, 113, 255)),  // green
                Outline = new Mapsui.Styles.Pen(new Mapsui.Styles.Color(255, 255, 255, 255), 2),
                SymbolScale = 0.6
            }
        };
        map.Layers.Add(_tractorLayer);

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

    /// <summary>
    /// Subscribe to the VM's GPS PropertyChanged so the tractor marker
    /// follows live position while the dialog is open. Idempotent.
    /// </summary>
    private void AttachTractorTracking()
    {
        if (DataContext is not AgValoniaGPS.ViewModels.MainViewModel vm) return;
        if (ReferenceEquals(_trackedVm, vm)) return;

        DetachTractorTracking();
        _trackedVm = vm;
        _trackedVm.PropertyChanged += OnVmPropertyChanged;
        UpdateTractorMarker(); // initial render
    }

    private void DetachTractorTracking()
    {
        if (_trackedVm != null)
        {
            _trackedVm.PropertyChanged -= OnVmPropertyChanged;
            _trackedVm = null;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AgValoniaGPS.ViewModels.MainViewModel.Latitude)
                           or nameof(AgValoniaGPS.ViewModels.MainViewModel.Longitude))
        {
            // Hop to UI thread; PropertyChanged from the GPS pipeline can
            // arrive on a worker thread depending on dispatcher state.
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                UpdateTractorMarker();
            else
                Avalonia.Threading.Dispatcher.UIThread.Post(UpdateTractorMarker);
        }
    }

    private void UpdateTractorMarker()
    {
        if (_tractorLayer == null || _trackedVm == null || MapControl?.Map == null)
            return;

        double lat = _trackedVm.Latitude;
        double lon = _trackedVm.Longitude;
        if (lat == 0 && lon == 0)
        {
            // No fix — hide the marker rather than render at (0,0).
            _tractorLayer.Clear();
            _tractorFeature = null;
            _tractorLayer.DataHasChanged();
            return;
        }

        var merc = SphericalMercator.FromLonLat(lon, lat);
        if (_tractorFeature == null)
        {
            _tractorFeature = new GeometryFeature(new NtsPoint(merc.x, merc.y));
            _tractorLayer.Add(_tractorFeature);
        }
        else
        {
            _tractorFeature.Geometry = new NtsPoint(merc.x, merc.y);
        }
        _tractorLayer.DataHasChanged();
    }

    /// <summary>
    /// Renders existing boundary polygons (outer + inner) as reference on the satellite map.
    /// </summary>
    private void UpdateExistingBoundaryLayer()
    {
        if (_existingBoundaryLayer == null) return;
        _existingBoundaryLayer.Clear();

        if (DataContext is not AgValoniaGPS.ViewModels.MainViewModel vm) return;
        if (vm.BoundaryMapExistingPolygons.Count == 0) return;

        var factory = new GeometryFactory();

        for (int polyIdx = 0; polyIdx < vm.BoundaryMapExistingPolygons.Count; polyIdx++)
        {
            var wgs84Points = vm.BoundaryMapExistingPolygons[polyIdx];
            if (wgs84Points.Count < 3) continue;

            // Convert to Mercator coordinates and close the ring
            var coords = new Coordinate[wgs84Points.Count + 1];
            for (int i = 0; i < wgs84Points.Count; i++)
            {
                var merc = SphericalMercator.FromLonLat(wgs84Points[i].Longitude, wgs84Points[i].Latitude);
                coords[i] = new Coordinate(merc.x, merc.y);
            }
            coords[^1] = coords[0]; // Close ring

            var ring = factory.CreateLinearRing(coords);
            var polygon = factory.CreatePolygon(ring);
            var feature = new GeometryFeature(polygon);

            // Outer boundary = orange, inner boundaries = yellow
            bool isOuter = polyIdx == 0;
            feature.Styles.Add(new VectorStyle
            {
                Fill = new Mapsui.Styles.Brush(isOuter
                    ? new Mapsui.Styles.Color(242, 112, 89, 40)   // Semi-transparent orange
                    : new Mapsui.Styles.Color(245, 245, 77, 40)), // Semi-transparent yellow
                Line = new Mapsui.Styles.Pen(isOuter
                    ? new Mapsui.Styles.Color(242, 112, 89, 200)  // Orange outline
                    : new Mapsui.Styles.Color(245, 245, 77, 200), // Yellow outline
                    2)
            });

            _existingBoundaryLayer.Add(feature);
        }

        _existingBoundaryLayer.DataHasChanged();
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
            if (_boundaryPoints.Count < 3) return null;

            var tileLayer = MapControl.Map.Layers.FirstOrDefault(l => l.Name == "Satellite") as TileLayer;
            if (tileLayer?.TileSource is not HttpTileSource tileSource)
            {
                Debug.WriteLine("[Capture] Satellite tile source not available");
                return null;
            }
            var schema = tileSource.Schema;

            // Boundary bbox in WGS84, padded 10% (min 20 m on each side).
            double minLat = _boundaryPoints.Min(p => p.Lat);
            double maxLat = _boundaryPoints.Max(p => p.Lat);
            double minLon = _boundaryPoints.Min(p => p.Lon);
            double maxLon = _boundaryPoints.Max(p => p.Lon);
            double midLat = (minLat + maxLat) / 2.0;
            double latPad = Math.Max((maxLat - minLat) * 0.1, 20.0 / 111000.0);
            double lonPad = Math.Max((maxLon - minLon) * 0.1, 20.0 / (111000.0 * Math.Cos(midLat * Math.PI / 180.0)));
            minLat -= latPad; maxLat += latPad;
            minLon -= lonPad; maxLon += lonPad;

            var minMerc = SphericalMercator.FromLonLat(minLon, minLat);
            var maxMerc = SphericalMercator.FromLonLat(maxLon, maxLat);
            double mercMinX = minMerc.x;
            double mercMinY = minMerc.y;
            double mercMaxX = maxMerc.x;
            double mercMaxY = maxMerc.y;
            double mercWidth = mercMaxX - mercMinX;
            double mercHeight = mercMaxY - mercMinY;

            // Pixel cap is a memory ceiling, not a quality target — 10000 px on the
            // long edge keeps the worst-case raw RGBA buffer around 400 MB.
            const int maxPixels = 10000;

            var extent = new Extent(mercMinX, mercMinY, mercMaxX, mercMaxY);

            // Tile providers (Esri included) return small "data not available" placeholders
            // outside their actual imagery footprint. Detect placeholders by size and fall
            // back to the next-lower zoom level — real aerial tiles are typically >5 KB,
            // placeholders are ~2-3 KB.
            const int placeholderByteThreshold = 4000;

            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; AgValoniaGPS/1.0)");
            httpClient.Timeout = TimeSpan.FromSeconds(20);

            // Candidate levels, finest first, that fit within the pixel cap.
            var candidateLevels = schema.Resolutions
                .OrderBy(kvp => kvp.Value.UnitsPerPixel)
                .Where(kvp =>
                {
                    double w = mercWidth / kvp.Value.UnitsPerPixel;
                    double h = mercHeight / kvp.Value.UnitsPerPixel;
                    return Math.Max(w, h) <= maxPixels;
                })
                .ToList();
            if (candidateLevels.Count == 0)
            {
                // bbox is enormous — fall back to the coarsest level so we still produce something.
                var coarsest = schema.Resolutions.OrderByDescending(kvp => kvp.Value.UnitsPerPixel).First();
                candidateLevels.Add(coarsest);
            }

            // Walk finest → coarsest, fetching tiles at each level. Stop when a level
            // gives 100% real coverage — that level acts as the gap-filler beneath
            // any partially-covered finer levels already fetched.
            var fetchedLevels = new List<(int Level, double Resolution, (TileInfo Info, byte[]? Bytes)[] Tiles)>();

            foreach (var kvp in candidateLevels)
            {
                var tileInfos = schema.GetTileInfos(extent, kvp.Key).ToList();
                if (tileInfos.Count == 0) continue;

                Console.WriteLine($"[Capture] Fetching L{kvp.Key} ({kvp.Value.UnitsPerPixel:F3} m/px, {tileInfos.Count} tiles)");

                var fetchTasks = tileInfos.Select<TileInfo, Task<(TileInfo Info, byte[]? Bytes)>>(async ti =>
                {
                    try
                    {
                        var bytes = await tileSource.GetTileAsync(httpClient, ti);
                        return (ti, bytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Capture] Tile fetch failed ({ti.Index.Col},{ti.Index.Row},{ti.Index.Level}): {ex.Message}");
                        return (ti, null);
                    }
                });
                var attempt = await Task.WhenAll(fetchTasks);

                int realCount = attempt.Count(r => r.Bytes is { Length: > placeholderByteThreshold });
                Console.WriteLine($"[Capture] L{kvp.Key}: {realCount}/{attempt.Length} real tiles");

                fetchedLevels.Add((kvp.Key, kvp.Value.UnitsPerPixel, attempt));

                if (realCount == attempt.Length)
                    break; // full coverage — done fetching deeper fallbacks
            }

            // Use the finest level that returned any real tiles as the output resolution.
            // Coarser levels are upscaled into the same buffer to fill gaps.
            var finestWithCoverage = fetchedLevels
                .FirstOrDefault(f => f.Tiles.Any(r => r.Bytes is { Length: > placeholderByteThreshold }));
            if (finestWithCoverage.Tiles is null)
            {
                Debug.WriteLine("[Capture] No real tiles at any zoom level");
                return null;
            }

            int level = finestWithCoverage.Level;
            double resolution = finestWithCoverage.Resolution;
            int outWidth = Math.Max((int)Math.Ceiling(mercWidth / resolution), 1);
            int outHeight = Math.Max((int)Math.Ceiling(mercHeight / resolution), 1);
            Console.WriteLine($"[Capture] Output L{level}, resolution {resolution:F4} m/px, {outWidth}x{outHeight}");
            Console.WriteLine($"[Capture] bbox merc: X[{mercMinX:F2}..{mercMaxX:F2}] Y[{mercMinY:F2}..{mercMaxY:F2}]");

            // Composite all fetched levels into one bitmap. Draw coarsest first so finer
            // levels overdraw it where they have imagery — "best available per pixel."
            // Use high-quality sampling so upscaled coarse tiles don't look blocky next to
            // sharp finer tiles.
            int decoded = 0;
            using var fullBitmap = new SKBitmap(outWidth, outHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(fullBitmap))
            using (var samplingPaint = new SKPaint { FilterQuality = SKFilterQuality.High })
            {
                canvas.Clear(SKColors.Black);
                foreach (var lvl in fetchedLevels.OrderBy(f => f.Level))
                {
                    foreach (var result in lvl.Tiles)
                    {
                        if (result.Bytes is null || result.Bytes.Length == 0) continue;
                        if (result.Bytes.Length <= placeholderByteThreshold) continue;
                        using var tileBitmap = SKBitmap.Decode(result.Bytes);
                        if (tileBitmap is null)
                        {
                            Console.WriteLine($"[Capture] Decode failed for tile L{lvl.Level} ({result.Info.Index.Col},{result.Info.Index.Row}), {result.Bytes.Length} bytes");
                            continue;
                        }
                        decoded++;

                        var tx = result.Info.Extent;
                        float dstLeft = (float)((tx.MinX - mercMinX) / resolution);
                        float dstRight = (float)((tx.MaxX - mercMinX) / resolution);
                        // Pixel Y is inverted relative to Mercator Y (north is up in world, top is 0 in pixels).
                        float dstTop = (float)((mercMaxY - tx.MaxY) / resolution);
                        float dstBottom = (float)((mercMaxY - tx.MinY) / resolution);
                        canvas.DrawBitmap(tileBitmap, new SKRect(dstLeft, dstTop, dstRight, dstBottom), samplingPaint);
                    }
                }
            }
            int totalFetched = fetchedLevels.Sum(f => f.Tiles.Length);
            Console.WriteLine($"[Capture] Decoded + drawn {decoded} tiles across {fetchedLevels.Count} level(s) (of {totalFetched} total)");

            var tempDir = Path.Combine(Path.GetTempPath(), "AgValoniaGPS_Mapsui");
            Directory.CreateDirectory(tempDir);
            var savedBackgroundPath = Path.Combine(tempDir, "BackPic.png");
            using (var data = fullBitmap.Encode(SKEncodedImageFormat.Png, 100))
            using (var fileStream = File.Create(savedBackgroundPath))
            {
                data.SaveTo(fileStream);
            }

            var nw = SphericalMercator.ToLonLat(mercMinX, mercMaxY);
            var se = SphericalMercator.ToLonLat(mercMaxX, mercMinY);

            var geoPath = Path.Combine(tempDir, "BackPic.txt");
            var geoContent = $"$BackPic\ntrue\n{nw.lat.ToString(CultureInfo.InvariantCulture)}\n{nw.lon.ToString(CultureInfo.InvariantCulture)}\n{se.lat.ToString(CultureInfo.InvariantCulture)}\n{se.lon.ToString(CultureInfo.InvariantCulture)}\n{mercMinX.ToString(CultureInfo.InvariantCulture)}\n{mercMaxX.ToString(CultureInfo.InvariantCulture)}\n{mercMinY.ToString(CultureInfo.InvariantCulture)}\n{mercMaxY.ToString(CultureInfo.InvariantCulture)}";
            File.WriteAllText(geoPath, geoContent);

            return (savedBackgroundPath, nw.lat, nw.lon, se.lat, se.lon, mercMinX, mercMaxX, mercMinY, mercMaxY);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Capture] Tile assembly failed: {ex.Message}");
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
        // Backdrop = abandon to the map; the Back button returns to Field Tools.
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.State.UI.CloseDialog();
            vm.BoundaryMapResultPoints.Clear();
            ResetState();
        }
        e.Handled = true;
    }
}
