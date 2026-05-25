// AgValoniaGPS
// Copyright (C) 2024-2026 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using AgValoniaGPS.Models;
using AgValoniaGPS.Models.Coverage;
using AgValoniaGPS.Models.Configuration;
using AgValoniaGPS.Models.Diagnostics;
using AgValoniaGPS.Models.Track;
using AssetLoader = Avalonia.Platform.AssetLoader;
using Vec2 = AgValoniaGPS.Models.Base.Vec2;
using Vec3 = AgValoniaGPS.Models.Base.Vec3;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Cross-platform map control. Renders on the compositor render thread via
/// <see cref="MapRenderState"/> snapshots posted from the UI thread. Uses
/// CompositionCustomVisualHandler + RegisterForNextAnimationFrameUpdate to
/// stay outside the Av12 commit throttle that caps OpenGlControlBase on
/// iPad (issue #21409). Renders boundary, headland, tracks, vehicle, tool,
/// coverage, ground texture, and supports real perspective via SKMatrix44.
/// Track/section/coverage/tram/youturn rendering is deferred to Phase 2.
/// </summary>
public partial class SkiaMapControl : Control, ISharedMapControl
{
    // ------------------------------------------------------------------
    // Avalonia styled properties (mirror DCMC for binding parity)
    // ------------------------------------------------------------------

    public static readonly StyledProperty<bool> IsGridVisibleProperty =
        AvaloniaProperty.Register<SkiaMapControl, bool>(nameof(IsGridVisible), defaultValue: true);

    public bool IsGridVisible
    {
        get => GetValue(IsGridVisibleProperty);
        set => SetValue(IsGridVisibleProperty, value);
    }

    public static readonly StyledProperty<bool> ShowVehicleProperty =
        AvaloniaProperty.Register<SkiaMapControl, bool>(nameof(ShowVehicle), defaultValue: true);

    public bool ShowVehicle
    {
        get => GetValue(ShowVehicleProperty);
        set => SetValue(ShowVehicleProperty, value);
    }

    // ------------------------------------------------------------------
    // Camera/viewport state (matches DCMC layout)
    // ------------------------------------------------------------------

    private double _cameraX, _cameraY;
    private double _zoom = 1.0;
    private double _rotation;
    private bool _isNorthUp;
    private bool _isDayMode = ConfigurationStore.Instance.Display.IsDayMode;

    // Phase 3 perspective state. Pitch is AOG convention: 0 = top-down,
    // π/2 = horizon. Receives radians from the platform code's
    // (90 + MainVM.CameraPitch) * π/180 conversion at MainWindow.axaml.cs:622
    // and MainView.axaml.cs:352. Below TopDownEpsilon the renderer falls back
    // to the 2D-affine fast path; at or above it the handler builds an
    // SKMatrix44 MVP per [[skiasharp-skmatrix44]].
    private double _cameraPitch;
    private bool _is3DMode;
    private const double TopDownEpsilon = 1e-3;
    private const double Default3DPitchRadians = Math.PI / 3.0; // 60°
    private const double MaxPitchRadians = Math.PI * 80.0 / 180.0; // 80°, matches GL clamp
    private int _cameraFollowMode = 3;
    public int CameraFollowMode
    {
        get => _cameraFollowMode;
        set { _cameraFollowMode = value; SendStateToHandler(); }
    }
    public event Action? UserPanned;
    public bool AutoPanEnabled { get; set; } = true;
    // Auto-pan tuning — matches DCMC so camera behavior is identical.
    private const double AutoPanSafeZone = 0.65;
    private const double AutoPanSmoothing = 0.15;

    // ------------------------------------------------------------------
    // Vehicle / tool / section state
    // ------------------------------------------------------------------

    private double _vehicleX, _vehicleY, _vehicleHeading, _vehicleSteerAngle;
    private bool _hasValidHeading;
    private bool _isReversing;
    // First valid vehicle position snaps the camera to it so the user doesn't
    // need to cycle through NorthUp at startup — DCMC's Map mode smooths in
    // over many ticks from (0,0), which is jarring on a freshly opened field.
    private bool _cameraInitialized;
    public bool IsReversing { get => _isReversing; set { _isReversing = value; SendStateToHandler(); } }

    private double _toolX, _toolY, _toolHeading, _toolWidth, _hitchX, _hitchY;
    private bool _toolPositionReady;

    private const int MaxSections = AgValoniaGPS.Models.Configuration.ToolConfig.MaxSections;
    private bool[] _sectionOn = new bool[MaxSections];
    private int[] _sectionButtonState = new int[MaxSections];
    private double[] _sectionWidths = new double[MaxSections];
    private double[] _sectionLeft = new double[MaxSections];
    private double[] _sectionRight = new double[MaxSections];
    private int _numSections;

    // ------------------------------------------------------------------
    // Pointer interaction (pan / rotate / click) — mirrors DCMC
    // ------------------------------------------------------------------

    private bool _isPanning;
    private bool _isRotating;
    private Point _lastMousePosition;
    private Point _panStartPosition;
    private bool _hasDraggedPastThreshold;
    private double _rotationOnPanStart;
    private const double DragThreshold = 5.0;

    public static readonly StyledProperty<bool> EnableClickSelectionProperty =
        AvaloniaProperty.Register<SkiaMapControl, bool>(nameof(EnableClickSelection), defaultValue: false);

    public bool EnableClickSelection
    {
        get => GetValue(EnableClickSelectionProperty);
        set => SetValue(EnableClickSelectionProperty, value);
    }

    public event EventHandler<MapClickEventArgs>? MapClicked;

    // ------------------------------------------------------------------
    // Data we render in Phase 1
    // ------------------------------------------------------------------

    private Boundary? _boundary;
    private IReadOnlyList<Vec3>? _headlandLine;
    private IReadOnlyList<Vec2>? _headlandPreview;
    private bool _isHeadlandVisible = true;

    private Bitmap? _groundTexture;
    private Bitmap? _groundTextureDay;
    private Bitmap? _groundTextureNight;
    private IImage? _vehicleImage;
    private IImage? _frontWheelImage;

    // ------------------------------------------------------------------
    // Data we accept but don't render yet (Phase 2+)
    // ------------------------------------------------------------------

    private Track? _activeTrack;
    private Track? _baseTrack;
    private Track? _nextTrack;
    private bool _isInYouTurn;
    private AgValoniaGPS.Models.Position? _pendingPointA;
    private IReadOnlyList<Track> _recordedPaths = Array.Empty<Track>();
    private IReadOnlyList<Track> _contourStrips = Array.Empty<Track>();
    private List<(double Easting, double Northing)>? _recordingPoints;
    private IReadOnlyList<(double Easting, double Northing, string Color, string Name)> _flags
        = Array.Empty<(double, double, string, string)>();
    private double _goalEasting, _goalNorthing;
    private bool _guidanceActive;

    // YouTurn path
    private IReadOnlyList<(double Easting, double Northing)>? _youTurnPath;

    // Tram lines
    private IReadOnlyList<Vec2>? _tramOuterTrack;
    private IReadOnlyList<Vec2>? _tramInnerTrack;
    private IReadOnlyList<IReadOnlyList<Vec2>>? _tramParallelLines;
    private IReadOnlyList<IReadOnlyList<Vec2>>? _tramBoundaryExtraLines;
    private byte _tramControlByte;

    // ------------------------------------------------------------------
    // Composition visual
    // ------------------------------------------------------------------

    private CompositionCustomVisual? _customVisual;
    private SkiaMapVisualHandler? _handler;

    // ------------------------------------------------------------------
    // FPS
    // ------------------------------------------------------------------

    private double _currentFps;
    public double CurrentFps => _currentFps;
    public event Action<double>? FpsUpdated;
    internal void ReportFps(double fps)
    {
        _currentFps = fps;
        FpsUpdated?.Invoke(fps);
    }

    // ------------------------------------------------------------------
    // Map snapshot (thumbnail)
    // ------------------------------------------------------------------

    /// <summary>Message asking the render thread to capture a thumbnail.</summary>
    private sealed record SnapshotRequest(int MaxDimension);

    public event EventHandler<byte[]>? SnapshotCaptured;

    public void RequestSnapshot(int maxDimension)
    {
        if (_customVisual == null) return;
        _customVisual.SendHandlerMessage(new SnapshotRequest(Math.Max(16, maxDimension)));
    }

    // Called by the handler (already marshalled onto the UI thread).
    internal void DeliverSnapshot(byte[] png) => SnapshotCaptured?.Invoke(this, png);

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public SkiaMapControl()
    {
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        try
        {
            var dayUri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/GroundTexture.png");
            using var dayStream = AssetLoader.Open(dayUri);
            _groundTextureDay = new Bitmap(dayStream);
            var nightUri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/GroundTextureDark.png");
            using var nightStream = AssetLoader.Open(nightUri);
            _groundTextureNight = new Bitmap(nightStream);
            _groundTexture = _isDayMode ? _groundTextureDay : _groundTextureNight;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaMapControl] Ground texture not found: {ex.Message}");
        }

        try
        {
            var uri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/TractorAoG.png");
            using var stream = AssetLoader.Open(uri);
            _vehicleImage = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaMapControl] Tractor image not found: {ex.Message}");
        }

        try
        {
            var uri = new Uri("avares://AgValoniaGPS.Views/Assets/Images/FrontWheels.png");
            using var stream = AssetLoader.Open(uri);
            _frontWheelImage = new Bitmap(stream);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SkiaMapControl] Front wheels image not found: {ex.Message}");
        }

        PropertyChanged += OnControlPropertyChanged;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    public override void Render(DrawingContext context)
    {
        // Composition handler does all real drawing — we just provide a hit-test surface.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SetupCompositionVisual();
    }

    private void SetupCompositionVisual()
    {
        var compositionVisual = ElementComposition.GetElementVisual(this);
        if (compositionVisual == null) return;

        var compositor = compositionVisual.Compositor;
        _handler = new SkiaMapVisualHandler(this);
        _customVisual = compositor.CreateCustomVisual(_handler);
        _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
        ElementComposition.SetElementChildVisual(this, _customVisual);
        SendStateToHandler();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && _customVisual != null)
        {
            _customVisual.Size = new Vector(Bounds.Width, Bounds.Height);
            SendStateToHandler();
        }
    }

    private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(IsVisible) && e.NewValue is true)
            SendStateToHandler();
    }

    // ------------------------------------------------------------------
    // State snapshot
    // ------------------------------------------------------------------

    // Coalesce multiple setter calls in the same UI tick into a single
    // state build + send. ApplyGpsCycleResult fires N setters per GPS cycle
    // (sections, you-turn, tracks, etc.); each used to build a fresh
    // MapRenderState (large class) and clone 5 arrays, dominating
    // ApplyGpsCycle's 11 KB/cycle allocation. Coalescing collapses that to
    // one build per cycle. Render-thread timing is unchanged — the
    // CustomVisual.SendHandlerMessage queue was already async.
    private bool _sendStatePending;

    internal void SendStateToHandler()
    {
        if (_customVisual == null || _handler == null) return;
        if (_sendStatePending) return;
        _sendStatePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _sendStatePending = false;
            SendStateToHandlerNow();
        }, DispatcherPriority.Background);
    }

    private void SendStateToHandlerNow()
    {
        if (_customVisual == null || _handler == null) return;

        EnsureCoverageBitmapReady();

        var displayCfg = ConfigurationStore.Instance.Display;
        var vehicleCfg = ConfigurationStore.Instance.Vehicle;
        var toolCfg = ConfigurationStore.Instance.Tool;

        var state = new MapRenderState
        {
            CameraX = _cameraX,
            CameraY = _cameraY,
            Zoom = _zoom,
            Rotation = _rotation,
            CameraPitch = _cameraPitch,
            Is3DMode = _is3DMode,
            IsNorthUp = _isNorthUp,
            IsDayMode = _isDayMode,

            BoundsWidth = Bounds.Width,
            BoundsHeight = Bounds.Height,

            VehicleX = _vehicleX,
            VehicleY = _vehicleY,
            VehicleHeading = _vehicleHeading,
            VehicleSteerAngle = _vehicleSteerAngle,
            VehicleWheelbase = vehicleCfg.Wheelbase,
            VehicleTrackWidth = vehicleCfg.TrackWidth,
            FrontWheelImage = _frontWheelImage,
            HasValidHeading = _hasValidHeading,
            IsReversing = _isReversing,
            ShowVehicle = ShowVehicle,
            VehicleImage = _vehicleImage,
            AntennaPivot = vehicleCfg.AntennaPivot,
            AntennaOffset = vehicleCfg.AntennaOffset,

            ToolX = _toolX,
            ToolY = _toolY,
            ToolHeading = _toolHeading,
            ToolWidth = _toolWidth,
            HitchX = _hitchX,
            HitchY = _hitchY,
            ToolReady = _toolPositionReady,
            HitchLength = toolCfg.HitchLength,
            IsToolTrailing = toolCfg.IsToolTrailing || toolCfg.IsToolTBT,
            ToolArmHalfSpread = vehicleCfg.TrackWidth * 0.5 * 0.6,
            ToolArmBaseX = _vehicleX + (toolCfg.IsToolFrontFixed
                ? Math.Sin(_vehicleHeading) * vehicleCfg.Wheelbase : 0),
            ToolArmBaseY = _vehicleY + (toolCfg.IsToolFrontFixed
                ? Math.Cos(_vehicleHeading) * vehicleCfg.Wheelbase : 0),
            ToolDrawbarBaseX = _vehicleX,
            ToolDrawbarBaseY = _vehicleY,

            SectionOn = (bool[])_sectionOn.Clone(),
            SectionWidths = (double[])_sectionWidths.Clone(),
            SectionLeft = (double[])_sectionLeft.Clone(),
            SectionRight = (double[])_sectionRight.Clone(),
            SectionButtonState = (int[])_sectionButtonState.Clone(),
            NumSections = _numSections,

            CoverageSkBitmap = _coverageSkBitmap,
            BitmapMinE = _bitmapMinE,
            BitmapMinN = _bitmapMinN,
            BitmapMaxE = _bitmapMaxE,
            BitmapMaxN = _bitmapMaxN,
            BitmapWidth = _bitmapWidth,
            BitmapHeight = _bitmapHeight,
            BitmapHasContent = _bitmapHasContent,
            BitmapExplicitlyInitialized = _bitmapExplicitlyInitialized,

            BackgroundImagePath = _backgroundImagePath,
            BgMinX = _bgMinX,
            BgMaxX = _bgMaxX,
            BgMinY = _bgMinY,
            BgMaxY = _bgMaxY,

            Boundary = _boundary,
            HeadlandLine = _headlandLine,
            HeadlandPreview = _headlandPreview,
            IsHeadlandVisible = _isHeadlandVisible,

            YouTurnPath = _youTurnPath,
            IsInYouTurn = _isInYouTurn,

            ActiveTrack = _activeTrack,
            BaseTrack = _baseTrack,
            NextTrack = _nextTrack,
            PendingPointA = _pendingPointA,
            RecordedPaths = _recordedPaths,
            ContourStrips = _contourStrips,

            RecordingPoints = _recordingPoints != null
                ? new List<(double, double)>(_recordingPoints) : null,

            Flags = _flags,

            GoalEasting = _goalEasting,
            GoalNorthing = _goalNorthing,
            GuidanceActive = _guidanceActive,

            GroundTexture = _groundTexture,
            FieldTextureVisible = displayCfg.FieldTextureVisible,
            GroundTextureMoveable = displayCfg.FieldTextureMoveable,
            LineSmoothEnabled = displayCfg.LineSmoothEnabled,
            ExtraGuidelines = displayCfg.ExtraGuidelines,
            ExtraGuidelinesCount = displayCfg.ExtraGuidelinesCount,

            TramOuterTrack = _tramOuterTrack,
            TramInnerTrack = _tramInnerTrack,
            TramParallelLines = _tramParallelLines,
            TramBoundaryExtraLines = _tramBoundaryExtraLines,
            TramDisplayMode = ConfigurationStore.Instance.Tram.DisplayMode,
            TramAlpha = (float)ConfigurationStore.Instance.Tram.Alpha,
            TramControlByte = _tramControlByte,
            HalfWheelTrack = vehicleCfg.TrackWidth / 2.0,
            IsDisplayTramControl = ConfigurationStore.Instance.Tram.IsDisplayTramControl,

            IsGridVisible = IsGridVisible,
            IsMetric = ConfigurationStore.Instance.IsMetric,
        };

        _customVisual.SendHandlerMessage(state);
    }

    // ------------------------------------------------------------------
    // ISharedMapControl — camera / view
    // ------------------------------------------------------------------

    public bool Is3DMode => _is3DMode;

    public void Toggle3DMode()
    {
        _is3DMode = !_is3DMode;
        if (_is3DMode && _cameraPitch < TopDownEpsilon)
            _cameraPitch = Default3DPitchRadians;
        SendStateToHandler();
    }

    public void Set3DMode(bool is3D)
    {
        _is3DMode = is3D;
        if (is3D && _cameraPitch < TopDownEpsilon)
            _cameraPitch = Default3DPitchRadians;
        SendStateToHandler();
    }

    public void SetPitch(double deltaRadians)
    {
        _cameraPitch = Math.Clamp(_cameraPitch + deltaRadians, 0.0, MaxPitchRadians);
        SendStateToHandler();
    }

    public void SetPitchAbsolute(double pitchRadians)
    {
        _cameraPitch = Math.Clamp(pitchRadians, 0.0, MaxPitchRadians);
        SendStateToHandler();
    }

    public void PanTo(double x, double y)
    {
        _cameraX = x; _cameraY = y;
        SendStateToHandler();
    }

    public void Pan(double deltaX, double deltaY)
    {
        _cameraX += deltaX; _cameraY += deltaY;
        SendStateToHandler();
    }

    // Zoom range matches PointerWheelChanged so on-screen zoom buttons stop
    // mutating _zoom past visual limits (otherwise grid stroke widths balloon
    // because they scale with worldPerPixel = 1/zoom).
    private const double MinZoom = 0.02;
    private const double MaxZoom = 100.0;

    // AOG zoom→distance curve plateaus at zoomScalar=4 (zoom-in) and 60
    // (zoom-out), so tapping past those points doesn't change the 3D visual
    // but would silently drift _zoom toward the hard clamp. Bail when the
    // underlying scalar is already pinned.
    private const double Min3DZoomScalar = 4.0;
    private const double Max3DZoomScalar = 60.0;

    public void Zoom(double factor)
    {
        // 2D hard clamp — refuse the tap if we're already at the limit
        // so _zoom doesn't multiply past it (prevents stroke ballooning).
        if (factor > 1.0 && _zoom >= MaxZoom) return;
        if (factor < 1.0 && _zoom <= MinZoom) return;

        // 3D plateau — same bail condition keyed off the zoomScalar curve.
        if (_is3DMode && _cameraPitch > TopDownEpsilon)
        {
            double currentScalar = 9.0 / _zoom;
            if (factor > 1.0 && currentScalar <= Min3DZoomScalar) return;
            if (factor < 1.0 && currentScalar >= Max3DZoomScalar) return;
        }

        _zoom = Math.Clamp(_zoom * factor, MinZoom, MaxZoom);
        SendStateToHandler();
    }

    public double GetZoom() => _zoom;

    public (double X, double Y) GetCameraCenter() => (_cameraX, _cameraY);

    public void SetCamera(double x, double y, double zoom, double rotation)
    {
        _cameraX = x; _cameraY = y;
        _zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        _rotation = rotation;
        SendStateToHandler();
    }

    public void Rotate(double deltaRadians)
    {
        _rotation += deltaRadians;
        SendStateToHandler();
    }

    // Mouse interaction — Phase 1 doesn't enable pan-by-drag; revisit when
    // the new control becomes the default (Phase 4). UserPanned event stays
    // declared so the platform code-behind can subscribe blindly.
    public void StartPan(Point position) { _ = position; }
    public void StartRotate(Point position) { _ = position; }
    public void UpdateMouse(Point position) { _ = position; }
    public void EndPanRotate() { }

    // ------------------------------------------------------------------
    // ISharedMapControl — content setters
    // ------------------------------------------------------------------

    public void SetBoundary(Boundary? boundary)
    {
        _boundary = boundary;
        SendStateToHandler();
    }

    public void SetVehiclePosition(double x, double y, double heading)
    {
        if (!_hasValidHeading && (Math.Abs(x) > 0.1 || Math.Abs(y) > 0.1))
            _hasValidHeading = true;

        _vehicleX = x; _vehicleY = y; _vehicleHeading = heading;

        ApplyCameraFollow();
        SendStateToHandler();
    }

    /// <summary>
    /// Camera follow switch — mirrors DCMC so map tracks the vehicle in
    /// NorthUp / HeadingUp / Map (auto-pan) modes. Free (mode 2) leaves the
    /// camera where the user dragged it.
    /// </summary>
    private void ApplyCameraFollow()
    {
        // First vehicle position after a field load (or app start): snap the
        // camera to the vehicle so the tractor is centered. Without this,
        // the VM's PanTo(boundaryCenter) leaves the camera where it parked
        // and Map-mode auto-pan smooths in only when the vehicle eventually
        // moves — which doesn't happen if the sim/GPS isn't running yet.
        if (!_cameraInitialized)
        {
            _cameraX = _vehicleX;
            _cameraY = _vehicleY;
            _cameraInitialized = true;
        }
        switch (_cameraFollowMode)
        {
            case 0:
                _cameraX = _vehicleX;
                _cameraY = _vehicleY;
                _rotation = 0;
                break;
            case 1:
                _cameraX = _vehicleX;
                _cameraY = _vehicleY;
                _rotation = -_vehicleHeading;
                break;
            case 2:
                break;
            case 3:
                _rotation = 0;
                if (Bounds.Width > 0 && Bounds.Height > 0)
                {
                    // If the vehicle is fully outside the current viewport
                    // (e.g. field-open PanTo parked the camera at a distant
                    // boundary center), snap to vehicle. AutoPan's 0.15
                    // smoothing factor takes many GPS ticks to converge,
                    // which looks broken when the sim isn't running yet.
                    double aspect = Bounds.Width / Bounds.Height;
                    double viewWidth = 200.0 * aspect / _zoom;
                    double viewHeight = 200.0 / _zoom;
                    double dx = _vehicleX - _cameraX;
                    double dy = _vehicleY - _cameraY;
                    if (Math.Abs(dx) > viewWidth / 2 || Math.Abs(dy) > viewHeight / 2)
                    {
                        _cameraX = _vehicleX;
                        _cameraY = _vehicleY;
                    }
                    else
                    {
                        ApplyAutoPan();
                    }
                }
                break;
        }
    }

    private void ApplyAutoPan()
    {
        double aspect = Bounds.Width / Bounds.Height;
        double viewWidth = 200.0 * aspect / _zoom;
        double viewHeight = 200.0 / _zoom;

        double safeHalfWidth = (viewWidth / 2) * AutoPanSafeZone;
        double safeHalfHeight = (viewHeight / 2) * AutoPanSafeZone;

        double relX = _vehicleX - _cameraX;
        double relY = _vehicleY - _cameraY;

        double cos = Math.Cos(-_rotation);
        double sin = Math.Sin(-_rotation);
        double screenRelX = relX * cos - relY * sin;
        double screenRelY = relX * sin + relY * cos;

        double panX = 0, panY = 0;
        if (screenRelX > safeHalfWidth) panX = screenRelX - safeHalfWidth;
        else if (screenRelX < -safeHalfWidth) panX = screenRelX + safeHalfWidth;
        if (screenRelY > safeHalfHeight) panY = screenRelY - safeHalfHeight;
        else if (screenRelY < -safeHalfHeight) panY = screenRelY + safeHalfHeight;

        if (Math.Abs(panX) > 0.01 || Math.Abs(panY) > 0.01)
        {
            double worldPanX = panX * Math.Cos(_rotation) - panY * Math.Sin(_rotation);
            double worldPanY = panX * Math.Sin(_rotation) + panY * Math.Cos(_rotation);
            _cameraX += worldPanX * AutoPanSmoothing;
            _cameraY += worldPanY * AutoPanSmoothing;
        }
    }

    public void SetVehicleSteerAngle(double radians)
    {
        _vehicleSteerAngle = radians;
        SendStateToHandler();
    }

    public void SetToolPosition(double x, double y, double heading, double width,
        double hitchX, double hitchY, bool isReady = true)
    {
        _toolX = x; _toolY = y; _toolHeading = heading; _toolWidth = width;
        _hitchX = hitchX; _hitchY = hitchY; _toolPositionReady = isReady;
        SendStateToHandler();
    }

    public void SetAllPositions(double vehicleX, double vehicleY, double vehicleHeading,
        double toolX, double toolY, double toolHeading, double toolWidth,
        double hitchX, double hitchY, bool toolReady)
    {
        if (!_hasValidHeading && (Math.Abs(vehicleX) > 0.1 || Math.Abs(vehicleY) > 0.1))
            _hasValidHeading = true;

        _vehicleX = vehicleX; _vehicleY = vehicleY; _vehicleHeading = vehicleHeading;
        _toolX = toolX; _toolY = toolY; _toolHeading = toolHeading; _toolWidth = toolWidth;
        _hitchX = hitchX; _hitchY = hitchY; _toolPositionReady = toolReady;

        ApplyCameraFollow();
        _isReversing = vehicleHeading < 0;

        SendStateToHandler();
    }

    public void SetSectionStates(bool[] sectionOn, double[] sectionWidths, int numSections,
        int[]? buttonStates = null)
    {
        _numSections = Math.Min(numSections, MaxSections);
        for (int i = 0; i < _numSections; i++)
        {
            _sectionOn[i] = i < sectionOn.Length && sectionOn[i];
            _sectionButtonState[i] = buttonStates != null && i < buttonStates.Length ? buttonStates[i] : 1;
            _sectionWidths[i] = i < sectionWidths.Length ? sectionWidths[i] : 1.0;
        }

        // Cumulative left/right positions centered on the tool — matches DCMC layout
        // so the same DrawToolSk geometry renders identically here.
        double totalWidth = 0;
        for (int i = 0; i < _numSections; i++) totalWidth += _sectionWidths[i];
        double pos = -totalWidth / 2.0;
        for (int i = 0; i < _numSections; i++)
        {
            _sectionLeft[i] = pos;
            _sectionRight[i] = pos + _sectionWidths[i];
            pos += _sectionWidths[i];
        }

        SendStateToHandler();
    }

    public void SetGridVisible(bool visible)
    {
        IsGridVisible = visible;
        SendStateToHandler();
    }

    public void SetNorthUp(bool isNorthUp)
    {
        _isNorthUp = isNorthUp;
        SendStateToHandler();
    }

    public void SetDayMode(bool isDayMode)
    {
        _isDayMode = isDayMode;
        _groundTexture = isDayMode ? _groundTextureDay : _groundTextureNight;
        SendStateToHandler();
    }

    public void SetRecordingPoints(IReadOnlyList<(double Easting, double Northing)> points)
    {
        _recordingPoints = new List<(double, double)>(points);
        SendStateToHandler();
    }

    public void ClearRecordingPoints()
    {
        _recordingPoints = null;
        SendStateToHandler();
    }

    public void SetBoundaryOffsetIndicator(bool show, double offsetMeters = 0.0)
    {
        // Phase 1: boundary recording UX deferred until Phase 2's input wiring.
    }
    // SetBackgroundImage / SetBackgroundImageWithMercator / ClearBackground
    // are in SkiaMapControl.Coverage.cs (Phase 2b — imagery composites into
    // the coverage bitmap).

    public void SetHeadlandLine(IReadOnlyList<Vec3>? headlandPoints)
    {
        _headlandLine = headlandPoints;
        SendStateToHandler();
    }

    public void SetHeadlandPreview(IReadOnlyList<Vec2>? previewPoints)
    {
        _headlandPreview = previewPoints;
        SendStateToHandler();
    }

    public void SetHeadlandVisible(bool visible)
    {
        _isHeadlandVisible = visible;
        SendStateToHandler();
    }

    public void SetYouTurnPath(IReadOnlyList<(double Easting, double Northing)>? turnPath)
    {
        _youTurnPath = turnPath;
        SendStateToHandler();
    }

    public void SetNextTrack(Track? track) { _nextTrack = track; SendStateToHandler(); }
    public void SetIsInYouTurn(bool isInTurn) { _isInYouTurn = isInTurn; SendStateToHandler(); }
    public void SetActiveTrack(Track? track) { _activeTrack = track; SendStateToHandler(); }
    public void SetBaseTrack(Track? track) { _baseTrack = track; SendStateToHandler(); }
    public void SetPendingPointA(AgValoniaGPS.Models.Position? pointA)
    {
        _pendingPointA = pointA;
        SendStateToHandler();
    }

    public void SetTramLines(
        IReadOnlyList<Vec2>? outerTrack,
        IReadOnlyList<Vec2>? innerTrack,
        IReadOnlyList<IReadOnlyList<Vec2>>? parallelLines,
        IReadOnlyList<IReadOnlyList<Vec2>>? boundaryExtraLines = null)
    {
        _tramOuterTrack = outerTrack;
        _tramInnerTrack = innerTrack;
        _tramParallelLines = parallelLines;
        _tramBoundaryExtraLines = boundaryExtraLines;
        SendStateToHandler();
    }

    public void SetTramControlByte(byte controlByte)
    {
        _tramControlByte = controlByte;
        SendStateToHandler();
    }

    public void SetRecordedPaths(IReadOnlyList<Track> paths) { _recordedPaths = paths; SendStateToHandler(); }
    public void SetContourStrips(IReadOnlyList<Track> strips) { _contourStrips = strips; SendStateToHandler(); }

    // Coverage subsystem (Phase 2b) is in SkiaMapControl.Coverage.cs.
    // CoveragePatches is dead code per [[coverage-architecture]] — kept as no-op.
    public void SetCoveragePatches(IReadOnlyList<CoveragePatch> patches) { }

    public void SetFlags(IReadOnlyList<(double Easting, double Northing, string Color, string Name)> flags)
    {
        _flags = flags;
        SendStateToHandler();
    }

    public void SetGuidancePoints(double goalEasting, double goalNorthing, bool isActive)
    {
        _goalEasting = goalEasting; _goalNorthing = goalNorthing; _guidanceActive = isActive;
    }

    // ------------------------------------------------------------------
    // Pointer input — pan / rotate / wheel-zoom / click. Same shape as
    // DCMC so EnableClickSelection (boundary editor) and AB-creation
    // click handling work uniformly across both 2D control paths.
    // ------------------------------------------------------------------

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (EnableClickSelection)
            {
                var world = ScreenToWorld(point.Position.X, point.Position.Y);
                MapClicked?.Invoke(this, new MapClickEventArgs(world.Easting, world.Northing));
                e.Handled = true;
                return;
            }
            _isPanning = true;
            _panStartPosition = point.Position;
            _hasDraggedPastThreshold = false;
            _rotationOnPanStart = _rotation;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            _isRotating = true;
            _lastMousePosition = point.Position;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var currentPos = point.Position;
        if (_isPanning)
        {
            _rotation = _rotationOnPanStart;
            double deltaX = currentPos.X - _lastMousePosition.X;
            double deltaY = currentPos.Y - _lastMousePosition.Y;
            double aspect = Bounds.Width / Bounds.Height;
            double viewWidth = 200.0 * aspect / _zoom;
            double viewHeight = 200.0 / _zoom;
            double worldDeltaX = -deltaX * viewWidth / Bounds.Width;
            double worldDeltaY = deltaY * viewHeight / Bounds.Height;
            double cos = Math.Cos(_rotation), sin = Math.Sin(_rotation);
            double rotatedDeltaX = worldDeltaX * cos - worldDeltaY * sin;
            double rotatedDeltaY = worldDeltaX * sin + worldDeltaY * cos;
            _cameraX += rotatedDeltaX;
            _cameraY += rotatedDeltaY;
            if (!_hasDraggedPastThreshold)
            {
                double dist = Math.Sqrt(Math.Pow(currentPos.X - _panStartPosition.X, 2)
                    + Math.Pow(currentPos.Y - _panStartPosition.Y, 2));
                if (dist > DragThreshold) _hasDraggedPastThreshold = true;
            }
            if (_hasDraggedPastThreshold)
            {
                _cameraFollowMode = 2;
                UserPanned?.Invoke();
            }
            _lastMousePosition = currentPos;
            SendStateToHandler();
            e.Handled = true;
        }
        else if (_isRotating)
        {
            double deltaX = currentPos.X - _lastMousePosition.X;
            _rotation += deltaX * 0.01;
            _cameraFollowMode = 2;
            _lastMousePosition = currentPos;
            UserPanned?.Invoke();
            SendStateToHandler();
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Treat a tap-without-drag as a click. Phase 2 enables AB-line creation
        // by tapping the map: short presses fire MapClicked with the world coord.
        if (_isPanning && !_hasDraggedPastThreshold)
        {
            var pos = e.GetCurrentPoint(this).Position;
            var world = ScreenToWorld(pos.X, pos.Y);
            MapClicked?.Invoke(this, new MapClickEventArgs(world.Easting, world.Northing));
        }
        if (_isPanning || _isRotating)
        {
            _isPanning = false;
            _isRotating = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        // Route through Zoom() so the wheel honors the same 3D plateau bail as the
        // zoom buttons. A raw clamp here let _zoom keep dropping past the 3D visual
        // limit (camera pinned at the zoomScalar cap), which kept coarsening the
        // grid spacing (200/_zoom) several more steps after max zoom-out (#430).
        Zoom(e.Delta.Y > 0 ? 1.1 : 0.9);
        e.Handled = true;
    }

    public (double Easting, double Northing) ScreenToWorld(double screenX, double screenY)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return (_cameraX, _cameraY);

        if (_is3DMode && _cameraPitch > TopDownEpsilon)
            return ScreenToWorldPerspective(screenX, screenY);

        double aspect = Bounds.Width / Bounds.Height;
        double viewWidth = 200.0 * aspect / _zoom;
        double viewHeight = 200.0 / _zoom;

        double normalizedX = (screenX / Bounds.Width) - 0.5;
        double normalizedY = 0.5 - (screenY / Bounds.Height);

        double worldOffsetX = normalizedX * viewWidth;
        double worldOffsetY = normalizedY * viewHeight;

        double cos = Math.Cos(_rotation), sin = Math.Sin(_rotation);
        double rotatedX = worldOffsetX * cos - worldOffsetY * sin;
        double rotatedY = worldOffsetX * sin + worldOffsetY * cos;
        return (_cameraX + rotatedX, _cameraY + rotatedY);
    }

    // Inverse-project a screen point through the perspective MVP to the
    // ground plane (z=0). MVP construction here MUST match
    // BuildPerspectiveScreenMatrix exactly, or AB tap creation will land
    // off the click point at tilted view.
    private (double Easting, double Northing) ScreenToWorldPerspective(double screenX, double screenY)
    {
        float vx = (float)_cameraX;
        float vy = (float)_cameraY;
        float aogPitchRad = -(float)_cameraPitch;
        float zoomScalar = MathF.Max(4f, MathF.Min(60f, 9f / MathF.Max(0.05f, (float)_zoom)));
        float distance = 0.5f * zoomScalar * zoomScalar;
        float rotationRad = -(float)_rotation;

        var t1 = System.Numerics.Matrix4x4.CreateTranslation(-vx, -vy, 0);
        var rZ = System.Numerics.Matrix4x4.CreateRotationZ(rotationRad);
        var rX = System.Numerics.Matrix4x4.CreateRotationX(aogPitchRad);
        var tBack = System.Numerics.Matrix4x4.CreateTranslation(0, 0, -distance);
        var view = t1 * rZ * rX * tBack;
        float aspect = (float)(Bounds.Width / Bounds.Height);
        var proj = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(
            0.7f, aspect, 1f, MathF.Max(5000f, distance * 4f));
        var mvp = view * proj;
        if (!System.Numerics.Matrix4x4.Invert(mvp, out var inv))
            return (_cameraX, _cameraY);

        // Screen → NDC. Y flips (screen y-down, NDC y-up).
        float ndcX = (float)(2.0 * screenX / Bounds.Width - 1.0);
        float ndcY = (float)(1.0 - 2.0 * screenY / Bounds.Height);
        // DirectX-style NDC Z: near plane = 0, far plane = 1 (matches
        // CreatePerspectiveFieldOfView's output range).
        var nearH = System.Numerics.Vector4.Transform(
            new System.Numerics.Vector4(ndcX, ndcY, 0f, 1f), inv);
        var farH = System.Numerics.Vector4.Transform(
            new System.Numerics.Vector4(ndcX, ndcY, 1f, 1f), inv);
        if (Math.Abs(nearH.W) < 1e-6 || Math.Abs(farH.W) < 1e-6)
            return (_cameraX, _cameraY);
        var near = new System.Numerics.Vector3(nearH.X / nearH.W, nearH.Y / nearH.W, nearH.Z / nearH.W);
        var far  = new System.Numerics.Vector3(farH.X  / farH.W,  farH.Y  / farH.W,  farH.Z  / farH.W);
        var dir = far - near;
        if (Math.Abs(dir.Z) < 1e-6)
            return (_cameraX, _cameraY);
        float t = -near.Z / dir.Z;
        if (t < 0)
            return (_cameraX, _cameraY);
        var hit = near + t * dir;
        return (hit.X, hit.Y);
    }

    // ==================================================================
    // SkiaMapVisualHandler — renders on the compositor render thread
    // ==================================================================

    private class SkiaMapVisualHandler : CompositionCustomVisualHandler
    {
        private readonly SkiaMapControl _owner;
        private MapRenderState? _state;

        // FPS tracking
        private DateTime _lastFpsUpdate = DateTime.UtcNow;
        private int _frameCount;

        // Cached SK paints — per-frame allocation kills iPad FPS, mirroring DCMC's
        // PERF-05 caching. Same paint set, narrower scope (no track / section paints
        // until Phase 2).
        private SKPaint? _gridMinorPaintSk;
        private SKPaint? _gridMajorPaintSk;
        private SKPaint? _gridAxisXPaintSk;
        private SKPaint? _gridAxisYPaintSk;
        private bool _gridPaintIsDayMode;
        private double _gridPaintMinorThickness;
        private double _gridPaintMajorThickness;
        private double _gridPaintAxisThickness;

        private readonly SKPaint _boundaryOuterPaint;
        private readonly SKPaint _boundaryInnerPaint;
        private readonly SKPaint _headlandPaint;
        private readonly SKPaint _headlandPreviewPaint;

        private readonly SKPaint _vehicleFallbackPaint;
        private readonly SKPaint _antennaPaint;
        private readonly SKPaint _headingUnknownLinePaint;
        private readonly SKPaint _headingUnknownDotPaint;
        private readonly SKPaint _reverseIndicatorPaint;

        // Track paints (mirror DCMC PERF-05 cached set)
        private readonly SKPaint _trackActivePaint;
        private readonly SKPaint _trackBaseDashPaint;
        private readonly SKPaint _trackNextPaint;
        private readonly SKPaint _abMarkerAPaint;
        private readonly SKPaint _abMarkerBPaint;
        private readonly SKPaint _abMarkerOutlinePaint;
        private readonly SKPaint _pendingPointPaint;
        private readonly SKPathEffect _trackDashEffect;
        private readonly SKFont _abLabelFont;
        private readonly SKPaint _abLabelTextPaint;
        private readonly SKPaint _abLabelHaloPaint;
        private readonly SKPaint _youTurnPaint;

        // Tool / section paints
        private readonly SKPaint _toolHitchPaint;
        private readonly SKPaint _toolDrawbarPaint;
        private readonly SKPaint _toolCenterPaint;
        private readonly SKPaint _toolFullBarPaint;
        private readonly SKPaint _sectionOutlinePaint;
        private readonly SKPaint[] _sectionFillPaints;
        private readonly SKPaint _tramOnPaint;
        private readonly SKPaint _tramOffPaint;

        // Stroke widths in world meters get foreshortened under perspective —
        // a 1m-wide boundary at 500m depth becomes sub-pixel. The handler
        // applies a uniform 3× to all strokes in both 2D and 3D (user prefers
        // bolder lines even in top-down), with an extra 2× on grid so it
        // stays visible at high zoom-out.
        private float _strokeMult = 1f;
        private const float BaseStrokeMult = 3f;
        private const float GridExtraStrokeMult = 2f;

        // Set in DrawSkiaScene's perspective branch. Lets draw methods
        // (DrawExtendedABLineSk, AB markers) clip geometry to the view
        // near plane before Skia projects — endpoints behind the camera
        // produce W < 0 and Skia draws wild artifacts from the screen corner.
        private bool _perspective;
        private System.Numerics.Matrix4x4 _perspectiveView;
        // Match CreatePerspectiveFieldOfView's near=1f but clip a couple
        // meters past the projection near plane so float precision at UTM
        // coord magnitudes (millions of meters) can't put a clipped endpoint
        // back on the wrong side. View-space Z is negative for visible
        // geometry; vis means z <= NearPlaneClipZ.
        private const float NearPlaneClipZ = -3f;

        // Vehicle bitmap shadow caches (decode once on first frame)
        private SKBitmap? _vehicleSkBitmap;
        private SKBitmap? _frontWheelSkBitmap;

        // Ground texture shadow cache — perspective path draws ground inside
        // the Skia lease, so it needs an SKBitmap, not an Avalonia.Bitmap.
        // Re-decode when the IImage reference changes (day/night swap on
        // SetDayMode swaps _groundTexture to the other Avalonia.Bitmap).
        // Shader + paint are cached alongside; allocating per-frame collapsed
        // 3D field-open FPS from ~96 to 65 on iPad.
        private SKBitmap? _groundSkBitmap;
        private object? _groundSkBitmapSource;
        private SKShader? _groundShader;
        private SKPaint? _groundPaint;

        // Field imagery cache. Lazy-decoded on the render thread so the GPU
        // upload + mipmap chain build happen in the right graphics context
        // (UI-thread-decoded SKImages were falling back to CPU rasterization
        // on iPad, dragging us to 11 FPS). Re-decoded only when the path
        // changes.
        private SKImage? _backgroundSkImage;
        private string? _backgroundSkImagePath;
        private const int BackgroundMaxDim = 2048;

        // Coverage SKImage snapshot cache (same pattern as DCMC). Skia can't cache
        // a mutating SKBitmap as a GPU texture across frames, so we snapshot it to
        // an immutable SKImage on a throttled cadence and draw that. The copy runs
        // on a background task; the render thread always draws the latest finished
        // snapshot.
        private SKImage? _coverageSnapshot;
        private SKImage? _coverageSnapshotPending;
        private SKBitmap? _coverageSnapshotSource;
        private DateTime _coverageSnapshotTime = DateTime.MinValue;
        private int _coverageSnapshotInFlight;
        private const double CoverageSnapshotIntervalMs = 200.0;
        private static readonly SKSamplingOptions _coverageSamplingNearest =
            new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        // Apple path (composite imagery + coverage in one SKBitmap): coverage
        // snapshot carries imagery pixels, so it needs trilinear/mipmap to fix
        // far-field shimmer. Worth the per-snapshot mipmap rebuild cost on
        // iPad's Metal-backed Skia.
        private static readonly SKSamplingOptions _coverageSamplingMipped =
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        // Non-Apple path (split layers): coverage holds only painted cells
        // (transparent elsewhere), no mips needed; imagery is the layer below
        // and uses its own mipped sampling.
        private static readonly SKSamplingOptions _coverageSamplingLinear =
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        private static readonly SKSamplingOptions _imageryMipmappedSampling =
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        // Vehicle bitmap proportions — same constants DCMC uses so the sprite
        // sits on the configured wheelbase / track-width regardless of zoom.
        private const double BitmapRearAxleYNorm = 0.245;
        private const double BitmapFrontAxleYNorm = 0.75;
        private const double BitmapFrontWheelHalfXNorm = 0.245;
        private const double BitmapAxleSpanYNorm = BitmapFrontAxleYNorm - BitmapRearAxleYNorm;
        private const double WheelBitmapContentWFraction = 0.27;
        private const double WheelBitmapContentHFraction = 0.29;
        private const double FrontTireWidthM = 0.378;
        private const double FrontTireDiameterM = 0.85;
        private const double FrontWheelSpriteForwardOffsetM = -0.05;

        public SkiaMapVisualHandler(SkiaMapControl owner)
        {
            _owner = owner;

            _boundaryOuterPaint = new SKPaint { Color = new SKColor(242, 112, 89, 204), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            _boundaryInnerPaint = new SKPaint { Color = new SKColor(245, 245, 77), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            _headlandPaint = new SKPaint { Color = new SKColor(251, 235, 107), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
            _headlandPreviewPaint = new SKPaint { Color = new SKColor(77, 250, 0, 180), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };

            _vehicleFallbackPaint = new SKPaint { Color = new SKColor(0, 200, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
            _antennaPaint = new SKPaint { Color = new SKColor(0, 120, 255), Style = SKPaintStyle.Fill, IsAntialias = true };
            _headingUnknownLinePaint = new SKPaint { Color = new SKColor(255, 0, 0), Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f, IsAntialias = true };
            _headingUnknownDotPaint = new SKPaint { Color = new SKColor(255, 0, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
            _reverseIndicatorPaint = new SKPaint { Color = new SKColor(255, 60, 60), Style = SKPaintStyle.Fill, IsAntialias = true };

            _trackDashEffect = SKPathEffect.CreateDash(new float[] { 1.5f, 1.0f }, 0f);
            // Round joins on the polyline paints — curve segments otherwise show
            // hard miter corners between points and look like jointed line
            // segments instead of a smooth curve.
            _trackActivePaint = new SKPaint { Color = new SKColor(252, 86, 186), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f, IsAntialias = true, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
            _trackBaseDashPaint = new SKPaint { Color = new SKColor(180, 100, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f, IsAntialias = true, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round, PathEffect = _trackDashEffect };
            _trackNextPaint = new SKPaint { Color = new SKColor(0, 200, 200), Style = SKPaintStyle.Stroke, StrokeWidth = 0.4f, IsAntialias = true, StrokeJoin = SKStrokeJoin.Round, StrokeCap = SKStrokeCap.Round };
            _abMarkerAPaint = new SKPaint { Color = new SKColor(0, 255, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
            _abMarkerBPaint = new SKPaint { Color = new SKColor(255, 0, 0), Style = SKPaintStyle.Fill, IsAntialias = true };
            _abMarkerOutlinePaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.15f, IsAntialias = true };
            _pendingPointPaint = new SKPaint { Color = new SKColor(0, 255, 0), Style = SKPaintStyle.Fill };
            _abLabelFont = new SKFont(SKTypeface.Default, 1.6f);
            _abLabelTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            _abLabelHaloPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 0.25f };
            _youTurnPaint = new SKPaint { Color = new SKColor(77, 242, 77), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };

            _toolHitchPaint = new SKPaint { Color = new SKColor(255, 255, 0), Style = SKPaintStyle.Stroke, StrokeWidth = 0.15f };
            _toolDrawbarPaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.3f };
            _toolCenterPaint = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f };
            _toolFullBarPaint = new SKPaint { Color = new SKColor(0, 242, 0, 191), Style = SKPaintStyle.Fill };
            _sectionOutlinePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f };
            _sectionFillPaints = new SKPaint[]
            {
                new SKPaint { Color = new SKColor(242, 51, 51),   Style = SKPaintStyle.Fill }, // 0 Off
                new SKPaint { Color = new SKColor(247, 247, 0),   Style = SKPaintStyle.Fill }, // 1 Manual ON
                new SKPaint { Color = new SKColor(0, 242, 0),     Style = SKPaintStyle.Fill }, // 2 Auto ON
                new SKPaint { Color = new SKColor(0, 222, 222),   Style = SKPaintStyle.Fill }, // 3 Turning OFF
                new SKPaint { Color = new SKColor(255, 165, 0),   Style = SKPaintStyle.Fill }, // 4 Turning ON
                new SKPaint { Color = new SKColor(150, 150, 150), Style = SKPaintStyle.Fill }, // 5 Auto OFF
            };
            _tramOnPaint = new SKPaint { Color = new SKColor(0, 230, 0), Style = SKPaintStyle.Fill };
            _tramOffPaint = new SKPaint { Color = new SKColor(40, 40, 40), Style = SKPaintStyle.Fill };
        }

        public override Rect GetRenderBounds()
        {
            var s = _state;
            if (s != null && s.BoundsWidth > 0 && s.BoundsHeight > 0)
                return new Rect(0, 0, s.BoundsWidth, s.BoundsHeight);
            return new Rect(0, 0, 4000, 4000);
        }

        // >0 means a thumbnail capture is pending; value is the max dimension.
        private int _snapshotMaxDim;

        public override void OnMessage(object message)
        {
            if (message is MapRenderState state)
            {
                bool firstState = _state == null;
                _state = state;
                Invalidate();
                if (firstState)
                    RegisterForNextAnimationFrameUpdate();
            }
            else if (message is SnapshotRequest req)
            {
                _snapshotMaxDim = req.MaxDimension;
                Invalidate();
                RegisterForNextAnimationFrameUpdate();
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            Invalidate();
            if (!DiagFlags.DisableAnimationFrameUpdate)
                RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            var s = _state;
            if (s == null) return;
            if (s.BoundsWidth <= 0 || s.BoundsHeight <= 0) return;

            // FPS
            _frameCount++;
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFpsUpdate).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var fps = _frameCount / elapsed;
                _frameCount = 0;
                _lastFpsUpdate = now;
                Dispatcher.UIThread.Post(() => _owner.ReportFps(fps), DispatcherPriority.Background);
            }

            try
            {
                // Background fill (screen space — before camera transform)
                var bgColor = s.IsDayMode
                    ? Color.FromRgb(69, 102, 179)
                    : Color.FromRgb(10, 10, 10);
                drawingContext.FillRectangle(
                    new ImmutableSolidColorBrush(bgColor),
                    new Rect(0, 0, s.BoundsWidth, s.BoundsHeight));

                double aspect = s.BoundsWidth / s.BoundsHeight;
                double viewWidth = 200.0 * aspect / s.Zoom;
                double viewHeight = 200.0 / s.Zoom;

                bool perspective = s.Is3DMode && s.CameraPitch > TopDownEpsilon;
                if (!perspective)
                {
                    // === Top-down 2D-affine fast path (unchanged from Phase 2) ===
                    var cameraMatrix = GetCameraTransform(s, viewWidth, viewHeight);
                    using var cameraScope = drawingContext.PushPreTransform(cameraMatrix);

                    if (s.GroundTexture != null && s.FieldTextureVisible && !DiagFlags.SkipGroundTexture)
                        DrawGroundTexture(drawingContext, s, viewWidth, viewHeight);

                    var skiaFeature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                    if (skiaFeature != null)
                    {
                        using var skiaLease = skiaFeature.Lease();
                        DrawSkiaScene(skiaLease.SkCanvas, s, viewWidth, viewHeight, perspective: false);
                        TryCaptureSnapshot(skiaLease);
                    }
                }
                else
                {
                    // === Perspective path — everything through Skia under an SKMatrix44 MVP ===
                    // Avalonia's 2D Matrix can't represent perspective, so we skip
                    // PushPreTransform and set the canvas matrix directly inside the lease.
                    var skiaFeature = drawingContext.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                    if (skiaFeature != null)
                    {
                        using var skiaLease = skiaFeature.Lease();
                        var canvas = skiaLease.SkCanvas;
                        var screen33 = BuildPerspectiveScreenMatrix(s);
                        canvas.Save();
                        try
                        {
                            // Concat (not SetMatrix) so Avalonia's existing
                            // logical→physical DPI scale stays in the chain.
                            // Without this, iPad retina renders at half resolution.
                            canvas.Concat(screen33);
                            if (s.GroundTexture != null && s.FieldTextureVisible && !DiagFlags.SkipGroundTexture)
                                DrawGroundTextureSk(canvas, s);
                            DrawSkiaScene(canvas, s, viewWidth, viewHeight, perspective: true);
                        }
                        finally
                        {
                            canvas.Restore();
                        }
                        TryCaptureSnapshot(skiaLease);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaMapVisualHandler] OnRender outer error: {ex.Message}");
            }
        }

        // Captures the just-rendered surface as a downscaled PNG thumbnail when
        // a snapshot was requested. One-shot: the request flag is cleared on
        // entry regardless of outcome. The GPU surface image is read back to a
        // raster image, drawn scaled into a small CPU surface, and encoded.
        private void TryCaptureSnapshot(ISkiaSharpApiLease lease)
        {
            if (_snapshotMaxDim <= 0) return;
            int maxDim = _snapshotMaxDim;
            _snapshotMaxDim = 0;

            try
            {
                var surface = lease.SkSurface;
                if (surface == null) return;

                using var gpuImage = surface.Snapshot();
                if (gpuImage == null) return;
                using var raster = gpuImage.ToRasterImage(ensurePixelData: true);
                if (raster == null) return;

                int srcW = raster.Width, srcH = raster.Height;
                if (srcW <= 0 || srcH <= 0) return;

                double scale = Math.Min(1.0, (double)maxDim / Math.Max(srcW, srcH));
                int tw = Math.Max(1, (int)Math.Round(srcW * scale));
                int th = Math.Max(1, (int)Math.Round(srcH * scale));

                using var thumb = SKSurface.Create(new SKImageInfo(tw, th, SKColorType.Rgba8888, SKAlphaType.Premul));
                if (thumb == null) return;
                thumb.Canvas.Clear(SKColors.Black);
                thumb.Canvas.DrawImage(raster, new SKRect(0, 0, tw, th),
                    new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                thumb.Canvas.Flush();

                using var thumbImg = thumb.Snapshot();
                using var data = thumbImg.Encode(SKEncodedImageFormat.Png, 90);
                if (data == null) return;
                var bytes = data.ToArray();
                Dispatcher.UIThread.Post(() => _owner.DeliverSnapshot(bytes));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaMapVisualHandler] snapshot failed: {ex.Message}");
            }
        }

        // Top-down and perspective paths share this draw block. With perspective:true
        // the canvas already has the SKMatrix44 MVP set; with false the canvas
        // inherits Avalonia's PushPreTransform 2D-affine camera matrix.
        private void DrawSkiaScene(SKCanvas canvas, MapRenderState s, double viewWidth, double viewHeight, bool perspective)
        {
            _strokeMult = BaseStrokeMult;
            _perspective = perspective;
            try
            {
                // Coverage bitmap (includes field imagery PNG composite) draws
                // before grid so grid stays visible on top of imagery.
                if (s.CoverageSkBitmap != null
                    && (s.BitmapHasContent || s.BitmapExplicitlyInitialized)
                    && s.BitmapWidth > 0 && s.BitmapHeight > 0
                    && !DiagFlags.SkipCoverageDraw)
                {
                    DrawCoverageBitmap(canvas, s);
                }

                if (s.IsGridVisible && !DiagFlags.SkipGrid)
                    DrawGridSk(canvas, s, viewWidth, viewHeight);

                if (!DiagFlags.SkipBoundaryDraw)
                {
                    if (s.Boundary != null)
                        DrawBoundary(canvas, s);
                    if (s.IsHeadlandVisible && s.HeadlandLine != null && s.HeadlandLine.Count > 2)
                        DrawHeadlandLine(canvas, s);
                    if (s.HeadlandPreview != null && s.HeadlandPreview.Count > 2)
                        DrawHeadlandPreview(canvas, s);
                    if (s.YouTurnPath != null && s.YouTurnPath.Count > 1)
                        DrawYouTurnPathSk(canvas, s);
                }

                if (s.TramDisplayMode != TramDisplayMode.Off && !DiagFlags.SkipTracks)
                    DrawTramLinesSk(canvas, s);

                if (!DiagFlags.SkipTracks)
                {
                    DrawRecordedPathsSk(canvas, s);
                    DrawContourStripsSk(canvas, s);
                    DrawTrackSk(canvas, s);
                }

                if (s.RecordingPoints != null && s.RecordingPoints.Count > 0)
                    DrawRecordingPointsSk(canvas, s);

                if (s.Flags.Count > 0)
                    DrawFlagsSk(canvas, s);

                if (!DiagFlags.SkipVehicle)
                {
                    // ToolWidth > 0.1 means tool config is loaded; matches DCMC.
                    if (s.ShowVehicle && s.ToolWidth > 0.1)
                        DrawToolSk(canvas, s);
                    if (s.ShowVehicle)
                        DrawVehicleSk(canvas, s);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaMapVisualHandler] Render error: {ex.Message}");
            }
        }

        // ----------------- Camera transform -----------------

        private static Matrix GetCameraTransform(MapRenderState s, double viewWidth, double viewHeight)
        {
            // Top-down 2D-affine. Y is mirrored because world is Y-up but
            // screen is Y-down. Used when pitch < TopDownEpsilon — anything
            // higher routes through BuildPerspectiveScreenMatrix instead.
            double scaleX = s.BoundsWidth / viewWidth;
            double scaleY = -s.BoundsHeight / viewHeight;

            var matrix = Matrix.Identity;
            matrix = matrix * Matrix.CreateTranslation(s.BoundsWidth / 2, s.BoundsHeight / 2);
            matrix = Matrix.CreateScale(scaleX, scaleY) * matrix;
            if (Math.Abs(s.Rotation) > 0.001)
                matrix = Matrix.CreateRotation(-s.Rotation) * matrix;
            matrix = Matrix.CreateTranslation(-s.CameraX, -s.CameraY) * matrix;
            return matrix;
        }

        // AOG-turntable perspective MVP. System.Numerics is row-vector;
        // implicit cast to SKMatrix44 handles the transposition to Skia's
        // column-vector convention. The 4×4 → 3×3 collapse via .Matrix is
        // the official Skia path per [[skiasharp-skmatrix44]].
        private SKMatrix BuildPerspectiveScreenMatrix(MapRenderState s)
        {
            float vx = (float)s.CameraX;
            float vy = (float)s.CameraY;

            // aogPitchRad is negative for non-overhead views. Matches GL's
            // `aogPitchRad = π * (-pitchDegrees - 90) / 180`: at our
            // _cameraPitch=0 (top-down) → 0; at _cameraPitch=π·80/180 (horizon)
            // → -π·80/180. So aogPitchRad = -_cameraPitch.
            float aogPitchRad = -(float)s.CameraPitch;

            // Zoom curve: zoomScalar = clamp(9 / zoom, 4, 60); distance = ½·s².
            // Same formula GL uses so 3D distance feels the same with either renderer.
            float zoomScalar = MathF.Max(4f, MathF.Min(60f, 9f / MathF.Max(0.05f, (float)s.Zoom)));
            float distance = 0.5f * zoomScalar * zoomScalar;

            // Camera rotation about Z. The 2D path uses CreateRotation(-_rotation);
            // mirror that here so heading-up / north-up behaves identically.
            float rotationRad = -(float)s.Rotation;

            var t1 = System.Numerics.Matrix4x4.CreateTranslation(-vx, -vy, 0);
            var rZ = System.Numerics.Matrix4x4.CreateRotationZ(rotationRad);
            var rX = System.Numerics.Matrix4x4.CreateRotationX(aogPitchRad);
            var tBack = System.Numerics.Matrix4x4.CreateTranslation(0, 0, -distance);
            var view = t1 * rZ * rX * tBack;
            _perspectiveView = view;

            float aspect = (float)(s.BoundsWidth / s.BoundsHeight);
            // FOV 0.7 rad ≈ 40°, matching GL.
            var proj = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(
                0.7f, aspect, 1f, MathF.Max(5000f, distance * 4f));

            var mvp = view * proj;
            SKMatrix44 mvp44 = mvp;

            // Viewport: NDC (-1..+1, y-up) → pixel (0..W, 0..H, y-down).
            // Row-vector form (v · M); m30/m31 carry the translation.
            float halfW = (float)(s.BoundsWidth * 0.5);
            float halfH = (float)(s.BoundsHeight * 0.5);
            var viewport44 = new SKMatrix44(
                halfW, 0f,     0f, 0f,
                0f,    -halfH, 0f, 0f,
                0f,    0f,     1f, 0f,
                halfW, halfH,  0f, 1f);

            // Apply mvp first (world → NDC) then viewport (NDC → pixel).
            var screen44 = SKMatrix44.Concat(mvp44, viewport44);
            return screen44.Matrix;
        }

        // Ground texture under perspective. The non-moveable 2D mode draws a
        // single rect of the bitmap; in 3D we always tile-repeat because the
        // visible ground stretches to the horizon (~5 km) and a single
        // bitmap stretched that wide is unusable. Single Skia draw call via
        // a cached repeat-tile SKShader.
        private const float GroundTileSizeMeters = 50f;
        private const float GroundRectRadiusMeters = 5000f;

        private void DrawGroundTextureSk(SKCanvas canvas, MapRenderState s)
        {
            EnsureGroundSkBitmap(s);
            if (_groundSkBitmap == null || _groundPaint == null) return;

            float minX = (float)s.CameraX - GroundRectRadiusMeters;
            float minY = (float)s.CameraY - GroundRectRadiusMeters;
            float maxX = (float)s.CameraX + GroundRectRadiusMeters;
            float maxY = (float)s.CameraY + GroundRectRadiusMeters;
            canvas.DrawRect(new SKRect(minX, minY, maxX, maxY), _groundPaint);
        }

        private void EnsureBackgroundSkImage(MapRenderState s)
        {
            var path = s.BackgroundImagePath;
            if (string.IsNullOrEmpty(path))
            {
                if (_backgroundSkImage != null)
                {
                    _backgroundSkImage.Dispose();
                    _backgroundSkImage = null;
                    _backgroundSkImagePath = null;
                }
                return;
            }
            if (string.Equals(_backgroundSkImagePath, path, StringComparison.Ordinal)
                && _backgroundSkImage != null)
                return;
            try
            {
                using var codec = SKCodec.Create(path);
                if (codec == null) return;
                var info = codec.Info;
                int sampleSize = 1;
                while ((info.Width / sampleSize) > BackgroundMaxDim
                    || (info.Height / sampleSize) > BackgroundMaxDim)
                    sampleSize *= 2;
                var scaledSize = codec.GetScaledDimensions(1.0f / sampleSize);
                var targetInfo = new SKImageInfo(
                    scaledSize.Width, scaledSize.Height,
                    SKColorType.Rgba8888, SKAlphaType.Premul);
                using var bmp = new SKBitmap(targetInfo);
                var result = codec.GetPixels(targetInfo, bmp.GetPixels());
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
                    return;
                using var pixmap = bmp.PeekPixels();
                if (pixmap == null) return;
                var old = _backgroundSkImage;
                _backgroundSkImage = SKImage.FromPixelCopy(pixmap);
                _backgroundSkImagePath = path;
                old?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SkiaMapVisualHandler] Imagery decode failed: {ex.Message}");
            }
        }

        private void EnsureGroundSkBitmap(MapRenderState s)
        {
            if (s.GroundTexture == null) return;
            if (ReferenceEquals(_groundSkBitmapSource, s.GroundTexture) && _groundSkBitmap != null)
                return;

            // Same decode trick as the tractor sprite below — round-trip
            // through PNG bytes since SkiaSharp can't consume an Avalonia
            // Bitmap directly.
            if (s.GroundTexture is Bitmap avBitmap)
            {
                try
                {
                    using var ms = new System.IO.MemoryStream();
                    avBitmap.Save(ms);
                    ms.Position = 0;
                    var oldBitmap = _groundSkBitmap;
                    var oldShader = _groundShader;
                    var oldPaint = _groundPaint;
                    _groundSkBitmap = SKBitmap.Decode(ms);
                    _groundSkBitmapSource = s.GroundTexture;

                    // Shader localMatrix scales shader-space → bitmap-pixel space
                    // so one tile (50m) covers one bitmap width.
                    float sx = _groundSkBitmap.Width / GroundTileSizeMeters;
                    float sy = _groundSkBitmap.Height / GroundTileSizeMeters;
                    var localMatrix = SKMatrix.CreateScale(sx, sy);
                    _groundShader = SKShader.CreateBitmap(
                        _groundSkBitmap,
                        SKShaderTileMode.Repeat, SKShaderTileMode.Repeat,
                        localMatrix);
                    _groundPaint = new SKPaint { Shader = _groundShader, IsAntialias = false };

                    oldPaint?.Dispose();
                    oldShader?.Dispose();
                    oldBitmap?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SkiaMapVisualHandler] Ground texture decode failed: {ex.Message}");
                }
            }
        }

        // ----------------- Ground texture -----------------

        private void DrawGroundTexture(ImmediateDrawingContext dc, MapRenderState s, double viewWidth, double viewHeight)
        {
            if (!s.GroundTextureMoveable)
            {
                double centerX = s.CameraX;
                double centerY = s.CameraY;
                double diagonal = Math.Sqrt(viewWidth * viewWidth + viewHeight * viewHeight) / 2 + 100.0;
                var viewRect = new Rect(centerX - diagonal, centerY - diagonal, diagonal * 2, diagonal * 2);
                dc.DrawBitmap(s.GroundTexture!, viewRect);
                return;
            }

            const double BaseTileSize = 50.0;
            const int MaxTilesPerAxis = 6;
            double halfDiagonal = Math.Sqrt(viewWidth * viewWidth + viewHeight * viewHeight) / 2;
            double viewSpan = halfDiagonal * 2;

            double tileSize = BaseTileSize;
            while (tileSize * MaxTilesPerAxis < viewSpan)
                tileSize *= 2.0;

            double minX = s.CameraX - halfDiagonal;
            double minY = s.CameraY - halfDiagonal;
            double maxX = s.CameraX + halfDiagonal;
            double maxY = s.CameraY + halfDiagonal;
            int x0 = (int)Math.Floor(minX / tileSize);
            int y0 = (int)Math.Floor(minY / tileSize);
            int x1 = (int)Math.Ceiling(maxX / tileSize);
            int y1 = (int)Math.Ceiling(maxY / tileSize);
            int xCount = Math.Min(x1 - x0 + 1, MaxTilesPerAxis);
            int yCount = Math.Min(y1 - y0 + 1, MaxTilesPerAxis);

            for (int iy = 0; iy < yCount; iy++)
            for (int ix = 0; ix < xCount; ix++)
            {
                double tx = (x0 + ix) * tileSize;
                double ty = (y0 + iy) * tileSize;
                dc.DrawBitmap(s.GroundTexture!, new Rect(tx, ty, tileSize, tileSize));
            }
        }

        // ----------------- Grid -----------------

        /// <summary>
        /// Rounds up to the next value in the 1-2-5 × 10ⁿ series
        /// (1, 2, 5, 10, 20, 50, …), with a floor of 1. Gives a grid level-of-
        /// detail whose steps are at most ~2.5× apart, so spacing changes as
        /// the view zooms feel gradual rather than snapping.
        /// </summary>
        private static double NiceStep125(double x)
        {
            if (x <= 1.0) return 1.0;
            double pow = Math.Pow(10, Math.Floor(Math.Log10(x)));
            double f = x / pow; // [1, 10)
            double niceF = f <= 2.0 ? 2.0 : f <= 5.0 ? 5.0 : 10.0;
            return niceF * pow;
        }

        private void DrawGridSk(SKCanvas canvas, MapRenderState s, double viewWidth, double viewHeight)
        {
            const double gridSize = 2000.0;
            double toolW = s.ToolWidth > 0.5 ? s.ToolWidth : 6.0;
            double viewSpan = Math.Max(viewWidth, viewHeight);

            // Grid spacing is a multiple of the tool width (each square ≈ one
            // swath). Pick the multiplier on a smooth 1-2-5 series keyed off
            // how many tool widths span the view, so zooming out steps the
            // spacing gently (≤2.5×) instead of the old hard 1×→5× jump that
            // made the grid snap dramatically. Clamped to ≥1 so the grid never
            // subdivides below a single tool width — the zoomed-in look where
            // each square is one swath stays correct (#417).
            const double targetDivisions = 30.0;
            double mult = NiceStep125(viewSpan / (toolW * targetDivisions));
            double spacing = toolW * mult;
            double majorEvery = spacing * 10;

            double screenHeight = s.BoundsHeight > 0 ? s.BoundsHeight : 600;
            double worldPerPixel = viewHeight / screenHeight;
            double gridMult = _strokeMult * GridExtraStrokeMult;
            double minorThickness = Math.Max(0.3 * worldPerPixel, 0.05) * gridMult;
            double majorThickness = Math.Max(0.6 * worldPerPixel, 0.1) * gridMult;
            double axisThickness = Math.Max(0.9 * worldPerPixel, 0.15) * gridMult;

            EnsureGridPaintsSk(s.IsDayMode, minorThickness, majorThickness, axisThickness);

            // Anti-alias grid in 3D — sub-pixel camera shifts under perspective
            // make unaliased far lines flicker. Costs ~17 FPS on iPad; the
            // post-Phase-3 perf audit ([[task #16]]) will look at recovering this.
            bool perspective = s.Is3DMode && s.CameraPitch > TopDownEpsilon;
            _gridMinorPaintSk!.IsAntialias = perspective;
            _gridMajorPaintSk!.IsAntialias = perspective;
            _gridAxisXPaintSk!.IsAntialias = perspective;
            _gridAxisYPaintSk!.IsAntialias = perspective;

            double minX = Math.Max(s.CameraX - viewWidth, -gridSize);
            double maxX = Math.Min(s.CameraX + viewWidth, gridSize);
            double minY = Math.Max(s.CameraY - viewHeight, -gridSize);
            double maxY = Math.Min(s.CameraY + viewHeight, gridSize);

            double startX = Math.Floor(minX / spacing) * spacing;
            double startY = Math.Floor(minY / spacing) * spacing;
            float lineXStart = (float)Math.Max(minX, -gridSize);
            float lineXEnd = (float)Math.Min(maxX, gridSize);
            float lineYStart = (float)Math.Max(minY, -gridSize);
            float lineYEnd = (float)Math.Min(maxY, gridSize);

            if (!_perspective)
            {
                using var minorPath = new SKPath();
                using var majorPath = new SKPath();
                bool hasAxisY = false, hasAxisX = false;

                for (double x = startX; x <= maxX; x += spacing)
                {
                    if (x < -gridSize || x > gridSize) continue;
                    if (Math.Abs(x) < 0.1) { hasAxisY = true; continue; }
                    bool isMajor = Math.Abs(x % majorEvery) < 0.1;
                    var path = isMajor ? majorPath : minorPath;
                    path.MoveTo((float)x, lineYStart);
                    path.LineTo((float)x, lineYEnd);
                }
                for (double y = startY; y <= maxY; y += spacing)
                {
                    if (y < -gridSize || y > gridSize) continue;
                    if (Math.Abs(y) < 0.1) { hasAxisX = true; continue; }
                    bool isMajor = Math.Abs(y % majorEvery) < 0.1;
                    var path = isMajor ? majorPath : minorPath;
                    path.MoveTo(lineXStart, (float)y);
                    path.LineTo(lineXEnd, (float)y);
                }

                canvas.DrawPath(minorPath, _gridMinorPaintSk!);
                canvas.DrawPath(majorPath, _gridMajorPaintSk!);
                if (hasAxisY)
                    canvas.DrawLine(0, lineYStart, 0, lineYEnd, _gridAxisYPaintSk!);
                if (hasAxisX)
                    canvas.DrawLine(lineXStart, 0, lineXEnd, 0, _gridAxisXPaintSk!);
            }
            else
            {
                // 3D: each grid line is one segment; clip to near plane.
                // Drawing per-line is slower than one DrawPath but avoids
                // W<0 artifacts (white flashes from night-mode major grid
                // when world-aligned lines cross the camera plane).
                for (double x = startX; x <= maxX; x += spacing)
                {
                    if (x < -gridSize || x > gridSize) continue;
                    var paint = Math.Abs(x) < 0.1
                        ? _gridAxisYPaintSk!
                        : (Math.Abs(x % majorEvery) < 0.1 ? _gridMajorPaintSk! : _gridMinorPaintSk!);
                    float gx1 = (float)x, gy1 = lineYStart;
                    float gx2 = (float)x, gy2 = lineYEnd;
                    if (ClipSegmentToNearPlane(ref gx1, ref gy1, ref gx2, ref gy2))
                        canvas.DrawLine(gx1, gy1, gx2, gy2, paint);
                }
                for (double y = startY; y <= maxY; y += spacing)
                {
                    if (y < -gridSize || y > gridSize) continue;
                    var paint = Math.Abs(y) < 0.1
                        ? _gridAxisXPaintSk!
                        : (Math.Abs(y % majorEvery) < 0.1 ? _gridMajorPaintSk! : _gridMinorPaintSk!);
                    float gx1 = lineXStart, gy1 = (float)y;
                    float gx2 = lineXEnd, gy2 = (float)y;
                    if (ClipSegmentToNearPlane(ref gx1, ref gy1, ref gx2, ref gy2))
                        canvas.DrawLine(gx1, gy1, gx2, gy2, paint);
                }
            }
        }

        private void EnsureGridPaintsSk(bool isDayMode, double minorThickness, double majorThickness, double axisThickness)
        {
            if (_gridMinorPaintSk != null
                && _gridPaintIsDayMode == isDayMode
                && Math.Abs(_gridPaintMinorThickness - minorThickness) < 1e-4
                && Math.Abs(_gridPaintMajorThickness - majorThickness) < 1e-4
                && Math.Abs(_gridPaintAxisThickness - axisThickness) < 1e-4)
                return;

            _gridMinorPaintSk?.Dispose();
            _gridMajorPaintSk?.Dispose();
            _gridAxisXPaintSk?.Dispose();
            _gridAxisYPaintSk?.Dispose();

            SKColor minorColor, majorColor;
            if (isDayMode)
            {
                minorColor = new SKColor(40, 40, 40, 120);
                majorColor = new SKColor(30, 30, 30, 180);
            }
            else
            {
                minorColor = new SKColor(180, 180, 180, 80);
                majorColor = new SKColor(200, 200, 200, 120);
            }

            _gridMinorPaintSk = new SKPaint { Color = minorColor, Style = SKPaintStyle.Stroke, StrokeWidth = (float)minorThickness, IsAntialias = false };
            _gridMajorPaintSk = new SKPaint { Color = majorColor, Style = SKPaintStyle.Stroke, StrokeWidth = (float)majorThickness, IsAntialias = false };
            _gridAxisXPaintSk = new SKPaint { Color = new SKColor(204, 51, 51, 70), Style = SKPaintStyle.Stroke, StrokeWidth = (float)axisThickness, IsAntialias = false };
            _gridAxisYPaintSk = new SKPaint { Color = new SKColor(51, 204, 51, 70), Style = SKPaintStyle.Stroke, StrokeWidth = (float)axisThickness, IsAntialias = false };

            _gridPaintIsDayMode = isDayMode;
            _gridPaintMinorThickness = minorThickness;
            _gridPaintMajorThickness = majorThickness;
            _gridPaintAxisThickness = axisThickness;
        }

        // ----------------- Coverage -----------------

        private void DrawCoverageBitmap(SKCanvas canvas, MapRenderState s)
        {
            // Non-Apple platforms: imagery layer first, static + mipped.
            // EnsureBackgroundSkImage decodes lazily on the render thread so
            // the GPU upload + mip chain land in the right context.
            if (UseSeparateImageryLayer)
            {
                EnsureBackgroundSkImage(s);
                if (_backgroundSkImage != null && s.BgMaxX > s.BgMinX && s.BgMaxY > s.BgMinY)
                {
                    var bgDst = new SKRect(
                        (float)s.BgMinX, (float)s.BgMinY,
                        (float)s.BgMaxX, (float)s.BgMaxY);
                    canvas.DrawImage(_backgroundSkImage, bgDst, _imageryMipmappedSampling);
                }
            }

            var bitmap = s.CoverageSkBitmap;
            if (bitmap == null || s.BitmapWidth == 0 || s.BitmapHeight == 0) return;
            if (bitmap.Handle == IntPtr.Zero) return;

            // Pick up any pending snapshot the background task finished.
            var pending = System.Threading.Interlocked.Exchange(ref _coverageSnapshotPending, null);
            if (pending != null)
            {
                var old = _coverageSnapshot;
                _coverageSnapshot = pending;
                old?.Dispose();
            }

            var now = DateTime.UtcNow;
            bool bitmapChanged = !ReferenceEquals(_coverageSnapshotSource, bitmap);
            bool dueForRefresh = _coverageSnapshot == null
                || bitmapChanged
                || (now - _coverageSnapshotTime).TotalMilliseconds >= CoverageSnapshotIntervalMs;

            if (dueForRefresh
                && System.Threading.Interlocked.CompareExchange(ref _coverageSnapshotInFlight, 1, 0) == 0)
            {
                _coverageSnapshotSource = bitmap;
                _coverageSnapshotTime = now;
                var captured = bitmap;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (captured.Handle == IntPtr.Zero) return;
                        using var pixmap = captured.PeekPixels();
                        if (pixmap == null) return;
                        var img = SKImage.FromPixelCopy(pixmap);
                        var displaced = System.Threading.Interlocked.Exchange(ref _coverageSnapshotPending, img);
                        displaced?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[CoverageSnapshot] bg refresh failed: {ex.Message}");
                    }
                    finally
                    {
                        System.Threading.Volatile.Write(ref _coverageSnapshotInFlight, 0);
                    }
                });
            }

            if (_coverageSnapshot == null) return;

            double worldWidth = s.BitmapMaxE - s.BitmapMinE;
            double worldHeight = s.BitmapMaxN - s.BitmapMinN;
            var src = new SKRect(0, 0, bitmap.Width, bitmap.Height);
            var dst = new SKRect(
                (float)s.BitmapMinE, (float)s.BitmapMinN,
                (float)(s.BitmapMinE + worldWidth), (float)(s.BitmapMinN + worldHeight));
            // Linear sampling under perspective regardless of LineSmoothEnabled
            // — foreshortened nearest-neighbor pixels moire badly (visible as
            // tree-detail flicker in the field PNG). 2D path keeps its
            // nearest-neighbor fast path so coverage paint stays crisp.
            // On the Apple composite path the coverage snapshot carries
            // imagery pixels too, so we need mipmap sampling here to fix the
            // far-field shimmer. Non-Apple split-layer path keeps coverage
            // mip-free because imagery is its own layer drawn underneath.
            bool perspective = s.Is3DMode && s.CameraPitch > TopDownEpsilon;
            SKSamplingOptions sampling;
            if (perspective && !UseSeparateImageryLayer)
                sampling = _coverageSamplingMipped;
            else if (s.LineSmoothEnabled || perspective)
                sampling = _coverageSamplingLinear;
            else
                sampling = _coverageSamplingNearest;
            canvas.DrawImage(_coverageSnapshot, src, dst, sampling);
        }

        // ----------------- Boundary -----------------

        private void DrawBoundary(SKCanvas canvas, MapRenderState s)
        {
            if (s.Boundary == null) return;

            _boundaryOuterPaint.StrokeWidth = 1f * _strokeMult;
            _boundaryInnerPaint.StrokeWidth = 1f * _strokeMult;
            // AA on in all modes. With the -3 clip margin past the projection
            // near plane, AA-stroke artifacts near the plane shouldn't trigger
            // any more, and far edges need AA to avoid jaggy/dashed appearance
            // at high tilt.
            _boundaryOuterPaint.IsAntialias = true;
            _boundaryInnerPaint.IsAntialias = true;

            if (s.Boundary.OuterBoundary != null && s.Boundary.OuterBoundary.IsValid
                && s.Boundary.OuterBoundary.Points.Count > 1)
            {
                DrawBoundaryRingSk(canvas, s.Boundary.OuterBoundary.Points, _boundaryOuterPaint);
            }

            foreach (var inner in s.Boundary.InnerBoundaries)
            {
                if (inner.IsValid && inner.Points.Count > 1)
                    DrawBoundaryRingSk(canvas, inner.Points, _boundaryInnerPaint);
            }

            if (s.Boundary.HeadlandPolygon != null && s.Boundary.HeadlandPolygon.IsValid
                && s.Boundary.HeadlandPolygon.Points.Count > 1)
            {
                DrawBoundaryRingSk(canvas, s.Boundary.HeadlandPolygon.Points, _boundaryInnerPaint);
            }
        }

        // Closed polygon draw. 2D: build SKPath, single DrawPath (fast).
        // 3D: per-segment DrawLine with near-plane clip — slower but the
        // SKPath route produces W<0 artifacts (orange/white flashing) for
        // segments that span the camera plane.
        private void DrawBoundaryRingSk(SKCanvas canvas, IReadOnlyList<BoundaryPoint> points, SKPaint paint)
        {
            int n = points.Count;
            if (n < 2) return;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                path.Close();
                canvas.DrawPath(path, paint);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % n];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }

        private void DrawHeadlandLine(SKCanvas canvas, MapRenderState s)
        {
            if (s.HeadlandLine == null || s.HeadlandLine.Count < 3) return;
            _headlandPaint.StrokeWidth = 1f * _strokeMult;
            DrawVec3RingSk(canvas, s.HeadlandLine, _headlandPaint);
        }

        private void DrawHeadlandPreview(SKCanvas canvas, MapRenderState s)
        {
            if (s.HeadlandPreview == null || s.HeadlandPreview.Count < 3) return;
            _headlandPreviewPaint.StrokeWidth = 1.5f * _strokeMult;
            DrawVec2RingSk(canvas, s.HeadlandPreview, _headlandPreviewPaint);
        }

        // Closed Vec3 polygon (headland line). Same 2D fast-path / 3D
        // per-segment-clip pattern as DrawBoundaryRingSk.
        private void DrawVec3RingSk(SKCanvas canvas, IReadOnlyList<Vec3> points, SKPaint paint)
        {
            int n = points.Count;
            if (n < 2) return;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                path.Close();
                canvas.DrawPath(path, paint);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % n];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }

        // Open Vec2 polyline (tram parallel/boundary lines).
        private void DrawVec2PolylineSk(SKCanvas canvas, IReadOnlyList<Vec2> points, SKPaint paint)
        {
            int n = points.Count;
            if (n < 2) return;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                canvas.DrawPath(path, paint);
                return;
            }
            for (int i = 0; i < n - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }

        // Closed Vec2 polygon (headland preview).
        private void DrawVec2RingSk(SKCanvas canvas, IReadOnlyList<Vec2> points, SKPaint paint)
        {
            int n = points.Count;
            if (n < 2) return;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                path.Close();
                canvas.DrawPath(path, paint);
                return;
            }
            for (int i = 0; i < n; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % n];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }

        // ----------------- Tool / sections -----------------

        private void DrawToolSk(SKCanvas canvas, MapRenderState s)
        {
            _toolHitchPaint.StrokeWidth = 0.15f * _strokeMult;
            _toolDrawbarPaint.StrokeWidth = 0.3f * _strokeMult;
            _toolCenterPaint.StrokeWidth = 0.1f * _strokeMult;
            _sectionOutlinePaint.StrokeWidth = 0.1f * _strokeMult;

            float tx = (float)s.ToolX, ty = (float)s.ToolY;
            float toolDepth = 2.0f;

            if (s.IsToolTrailing)
            {
                canvas.DrawLine((float)s.ToolDrawbarBaseX, (float)s.ToolDrawbarBaseY,
                    (float)s.HitchX, (float)s.HitchY, _toolDrawbarPaint);
                canvas.DrawLine((float)s.HitchX, (float)s.HitchY, tx, ty, _toolHitchPaint);
            }
            else
            {
                float cosV = (float)Math.Cos(-s.VehicleHeading);
                float sinV = (float)Math.Sin(-s.VehicleHeading);
                float baseX = (float)s.ToolArmBaseX;
                float baseY = (float)s.ToolArmBaseY;
                float armSpread = (float)s.ToolArmHalfSpread;
                canvas.DrawLine(baseX + (-armSpread) * cosV, baseY + (-armSpread) * sinV, tx, ty, _toolHitchPaint);
                canvas.DrawLine(baseX + armSpread * cosV, baseY + armSpread * sinV, tx, ty, _toolHitchPaint);
            }

            canvas.Save();
            canvas.Translate(tx, ty);
            canvas.RotateRadians(-(float)s.ToolHeading);

            if (s.NumSections > 0)
            {
                float sectionGap = 0.05f;
                for (int i = 0; i < s.NumSections; i++)
                {
                    float left = (float)s.SectionLeft[i] + sectionGap / 2;
                    float right = (float)s.SectionRight[i] - sectionGap / 2;
                    float width = right - left;
                    if (width < 0.01f) continue;
                    int state = s.SectionButtonState[i];
                    var secPaint = (uint)state < (uint)_sectionFillPaints.Length
                        ? _sectionFillPaints[state]
                        : _sectionFillPaints[2];
                    canvas.DrawRect(left, -toolDepth / 2, width, toolDepth, secPaint);
                    canvas.DrawRect(left, -toolDepth / 2, width, toolDepth, _sectionOutlinePaint);
                }
            }
            else
            {
                float halfWidth = (float)(s.ToolWidth / 2);
                canvas.DrawRect(-halfWidth, -toolDepth / 2, (float)s.ToolWidth, toolDepth, _toolFullBarPaint);
            }

            canvas.DrawLine(0, -toolDepth / 2, 0, toolDepth / 2, _toolCenterPaint);

            if (s.IsDisplayTramControl && s.TramDisplayMode != TramDisplayMode.Off)
            {
                float dotRadius = 0.3f;
                float halfTrack = (float)s.HalfWheelTrack;
                bool rightOn = (s.TramControlByte & 1) != 0;
                bool leftOn  = (s.TramControlByte & 2) != 0;
                canvas.DrawCircle(halfTrack, 0, dotRadius, rightOn ? _tramOnPaint : _tramOffPaint);
                canvas.DrawCircle(-halfTrack, 0, dotRadius, leftOn  ? _tramOnPaint : _tramOffPaint);
            }

            canvas.Restore();
        }

        // ----------------- Tracks -----------------

        private void DrawTrackSk(SKCanvas canvas, MapRenderState s)
        {
            _trackActivePaint.StrokeWidth = 0.5f * _strokeMult;
            _trackBaseDashPaint.StrokeWidth = 0.3f * _strokeMult;
            _trackNextPaint.StrokeWidth = 0.4f * _strokeMult;
            _abMarkerOutlinePaint.StrokeWidth = 0.15f * _strokeMult;

            if (s.ActiveTrack != null && s.ActiveTrack.Points.Count >= 2)
            {
                if (s.ActiveTrack.Points.Count == 2)
                    DrawExtendedABLineSk(canvas, s.ActiveTrack.Points[0], s.ActiveTrack.Points[1], _trackActivePaint);
                else
                    DrawTrackPointsSk(canvas, s.ActiveTrack.Points, _trackActivePaint);
            }

            var sourceAb = ResolveSourceAbTrack(s);
            if (sourceAb != null && sourceAb.Points.Count == 2)
            {
                var pA = sourceAb.Points[0];
                var pB = sourceAb.Points[1];
                // Dashed A→B base line — clip to near plane in perspective.
                {
                    float dx1 = (float)pA.Easting, dy1 = (float)pA.Northing;
                    float dx2 = (float)pB.Easting, dy2 = (float)pB.Northing;
                    if (!_perspective || ClipSegmentToNearPlane(ref dx1, ref dy1, ref dx2, ref dy2))
                        canvas.DrawLine(dx1, dy1, dx2, dy2, _trackBaseDashPaint);
                }

                float markerHalf = 1.8f;
                // Skip marker draw if center is behind the camera — DrawRect's
                // 4 corner verts would all have W < 0 and Skia rasterizes them
                // as wild geometry from the screen corner (the "white lines
                // from top-left" artifact).
                bool drawA = !_perspective || IsPointInFrontOfNearPlane(pA.Easting, pA.Northing);
                bool drawB = !_perspective || IsPointInFrontOfNearPlane(pB.Easting, pB.Northing);

                if (drawA)
                {
                    var aRect = new SKRect((float)pA.Easting - markerHalf, (float)pA.Northing - markerHalf,
                        (float)pA.Easting + markerHalf, (float)pA.Northing + markerHalf);
                    canvas.DrawRect(aRect, _abMarkerAPaint);
                    canvas.DrawRect(aRect, _abMarkerOutlinePaint);
                    DrawAbLabelSk(canvas, "A", pA.Easting + markerHalf + 0.4, pA.Northing + markerHalf + 0.4);
                }

                if (drawB)
                {
                    var bRect = new SKRect((float)pB.Easting - markerHalf, (float)pB.Northing - markerHalf,
                        (float)pB.Easting + markerHalf, (float)pB.Northing + markerHalf);
                    canvas.DrawRect(bRect, _abMarkerBPaint);
                    canvas.DrawRect(bRect, _abMarkerOutlinePaint);
                    DrawAbLabelSk(canvas, "B", pB.Easting + markerHalf + 0.4, pB.Northing + markerHalf + 0.4);
                }
            }
            else if (sourceAb != null && sourceAb.Points.Count > 2)
            {
                DrawTrackPointsSk(canvas, sourceAb.Points, _trackBaseDashPaint);
            }

            if (s.IsInYouTurn && s.NextTrack != null && s.NextTrack.Points.Count >= 2)
            {
                if (s.NextTrack.Points.Count == 2)
                    DrawExtendedABLineSk(canvas, s.NextTrack.Points[0], s.NextTrack.Points[1], _trackNextPaint);
                else
                    DrawTrackPointsSk(canvas, s.NextTrack.Points, _trackNextPaint);
            }

            if (s.PendingPointA != null)
                canvas.DrawCircle((float)s.PendingPointA.Easting, (float)s.PendingPointA.Northing, 3.0f, _pendingPointPaint);
        }

        private static Track? ResolveSourceAbTrack(MapRenderState s)
        {
            if (s.BaseTrack != null && s.BaseTrack.Points.Count >= 2 && s.BaseTrack != s.ActiveTrack)
                return s.BaseTrack;
            if (s.ActiveTrack != null && s.ActiveTrack.Points.Count >= 2)
                return s.ActiveTrack;
            return null;
        }

        private void DrawExtendedABLineSk(SKCanvas canvas, Vec3 a, Vec3 b, SKPaint paint)
        {
            double dx = b.Easting - a.Easting;
            double dy = b.Northing - a.Northing;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.01)
            {
                canvas.DrawPoint((float)a.Easting, (float)a.Northing, paint);
                return;
            }
            double nx = dx / len, ny = dy / len;
            const double ext = 5000.0;
            float x1 = (float)(a.Easting - nx * ext);
            float y1 = (float)(a.Northing - ny * ext);
            float x2 = (float)(b.Easting + nx * ext);
            float y2 = (float)(b.Northing + ny * ext);

            // In 3D the 5000m extension almost certainly puts one endpoint
            // behind the camera; Skia would draw wild artifacts. CPU-clip
            // to the view near plane before submitting.
            if (_perspective && !ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                return;
            canvas.DrawLine(x1, y1, x2, y2, paint);
        }

        // Returns true if the segment is at least partly in front of the
        // view near plane. Mutates endpoints in place to clip the segment
        // when one endpoint is behind. Standard NDC-style near-plane clip
        // in view space — far simpler than full frustum culling, and the
        // near plane is where projection actually breaks.
        private bool ClipSegmentToNearPlane(ref float x1, ref float y1, ref float x2, ref float y2)
        {
            var v1 = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(x1, y1, 0f), _perspectiveView);
            var v2 = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3(x2, y2, 0f), _perspectiveView);
            bool vis1 = v1.Z <= NearPlaneClipZ;
            bool vis2 = v2.Z <= NearPlaneClipZ;
            if (vis1 && vis2) return true;
            if (!vis1 && !vis2) return false;

            float t = (NearPlaneClipZ - v1.Z) / (v2.Z - v1.Z);
            float clipX = x1 + t * (x2 - x1);
            float clipY = y1 + t * (y2 - y1);
            if (vis1) { x2 = clipX; y2 = clipY; }
            else      { x1 = clipX; y1 = clipY; }
            return true;
        }

        private bool IsPointInFrontOfNearPlane(double worldX, double worldY)
        {
            var v = System.Numerics.Vector3.Transform(
                new System.Numerics.Vector3((float)worldX, (float)worldY, 0f),
                _perspectiveView);
            return v.Z <= NearPlaneClipZ;
        }

        private void DrawTrackPointsSk(SKCanvas canvas, IReadOnlyList<Vec3> points, SKPaint paint)
        {
            int n = points.Count;
            if (n < 2) return;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)points[0].Easting, (float)points[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)points[i].Easting, (float)points[i].Northing);
                canvas.DrawPath(path, paint);
                return;
            }
            // Open polyline with near-plane clipping. Multi-point curves can
            // dip behind the camera mid-stretch (e.g. recorded paths that
            // loop around the vehicle); clip each segment independently.
            for (int i = 0; i < n - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, paint);
            }
        }

        private void DrawAbLabelSk(SKCanvas canvas, string text, double worldX, double worldY)
        {
            float fontSize = _abLabelFont.Size;
            canvas.Save();
            canvas.Scale(1, -1, (float)worldX, (float)worldY);
            canvas.DrawText(text, (float)worldX, (float)worldY + fontSize / 2, _abLabelFont, _abLabelHaloPaint);
            canvas.DrawText(text, (float)worldX, (float)worldY + fontSize / 2, _abLabelFont, _abLabelTextPaint);
            canvas.Restore();
        }

        private void DrawYouTurnPathSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.YouTurnPath == null || s.YouTurnPath.Count < 2) return;
            _youTurnPaint.StrokeWidth = 1f * _strokeMult;
            int n = s.YouTurnPath.Count;
            if (!_perspective)
            {
                using var path = new SKPath();
                path.MoveTo((float)s.YouTurnPath[0].Easting, (float)s.YouTurnPath[0].Northing);
                for (int i = 1; i < n; i++)
                    path.LineTo((float)s.YouTurnPath[i].Easting, (float)s.YouTurnPath[i].Northing);
                canvas.DrawPath(path, _youTurnPaint);
                return;
            }
            for (int i = 0; i < n - 1; i++)
            {
                var a = s.YouTurnPath[i];
                var b = s.YouTurnPath[i + 1];
                float x1 = (float)a.Easting, y1 = (float)a.Northing;
                float x2 = (float)b.Easting, y2 = (float)b.Northing;
                if (ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, _youTurnPaint);
            }
        }

        private void DrawTramLinesSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.TramDisplayMode == TramDisplayMode.Off) return;
            bool hasBoundary = (s.TramOuterTrack?.Count > 1 || s.TramInnerTrack?.Count > 1);
            bool hasLines = s.TramParallelLines?.Count > 0;
            if (!hasBoundary && !hasLines) return;

            byte alpha = (byte)(s.TramAlpha * 160);
            using var tramPaint = new SKPaint
            {
                Color = new SKColor(237, 140, 150, alpha),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 0.6f * _strokeMult,
                IsAntialias = true
            };

            bool hasBoundaryExtra = s.TramBoundaryExtraLines?.Count > 0;

            if ((s.TramDisplayMode == TramDisplayMode.All
                 || s.TramDisplayMode == TramDisplayMode.LinesOnly) && hasLines)
            {
                foreach (var line in s.TramParallelLines!)
                    DrawVec2PolylineSk(canvas, line, tramPaint);
            }

            if ((s.TramDisplayMode == TramDisplayMode.All
                 || s.TramDisplayMode == TramDisplayMode.OuterOnly) && hasBoundary)
            {
                if (s.TramOuterTrack != null) DrawVec2PolylineSk(canvas, s.TramOuterTrack, tramPaint);
                if (s.TramInnerTrack != null) DrawVec2PolylineSk(canvas, s.TramInnerTrack, tramPaint);
                if (hasBoundaryExtra)
                    foreach (var line in s.TramBoundaryExtraLines!)
                        DrawVec2PolylineSk(canvas, line, tramPaint);
            }
        }

        private void DrawRecordingPointsSk(SKCanvas canvas, MapRenderState s)
        {
            if (s.RecordingPoints == null) return;
            using var linePaint = new SKPaint { Color = new SKColor(0, 255, 255), Style = SKPaintStyle.Stroke, StrokeWidth = 0.5f * _strokeMult, IsAntialias = true };
            using var pointPaint = new SKPaint { Color = new SKColor(255, 128, 0), Style = SKPaintStyle.Fill };
            for (int i = 1; i < s.RecordingPoints.Count; i++)
            {
                var (e1, n1) = s.RecordingPoints[i - 1];
                var (e2, n2) = s.RecordingPoints[i];
                float x1 = (float)e1, y1 = (float)n1, x2 = (float)e2, y2 = (float)n2;
                if (!_perspective || ClipSegmentToNearPlane(ref x1, ref y1, ref x2, ref y2))
                    canvas.DrawLine(x1, y1, x2, y2, linePaint);
            }
            if (s.RecordingPoints.Count > 0)
            {
                var (e, n) = s.RecordingPoints[s.RecordingPoints.Count - 1];
                canvas.DrawCircle((float)e, (float)n, 0.3f, pointPaint);
            }
        }

        private void DrawFlagsSk(SKCanvas canvas, MapRenderState s)
        {
            using var outlinePaint = new SKPaint { Color = SKColors.Black, Style = SKPaintStyle.Stroke, StrokeWidth = 0.1f * _strokeMult };
            foreach (var (easting, northing, color, name) in s.Flags)
            {
                var skColor = color switch
                {
                    "Red" => SKColors.Red,
                    "Green" => SKColors.Green,
                    "Blue" => SKColors.Blue,
                    "Yellow" => SKColors.Yellow,
                    _ => SKColors.White
                };
                using var paint = new SKPaint { Color = skColor, Style = SKPaintStyle.Fill };
                canvas.DrawCircle((float)easting, (float)northing, 0.8f, paint);
                canvas.DrawCircle((float)easting, (float)northing, 0.8f, outlinePaint);
            }
        }

        private void DrawRecordedPathsSk(SKCanvas canvas, MapRenderState s)
        {
            foreach (var p in s.RecordedPaths)
                if (p.Points.Count > 1)
                    DrawTrackPointsSk(canvas, p.Points, _trackBaseDashPaint);
        }

        private void DrawContourStripsSk(SKCanvas canvas, MapRenderState s)
        {
            foreach (var p in s.ContourStrips)
                if (p.Points.Count > 1)
                    DrawTrackPointsSk(canvas, p.Points, _trackBaseDashPaint);
        }

        // ----------------- Vehicle -----------------

        private static void BitmapTractorSize(MapRenderState s, out double widthWorld, out double heightWorld)
        {
            double trackWidth = s.VehicleTrackWidth > 0.01 ? s.VehicleTrackWidth : 1.8;
            double wheelbase = s.VehicleWheelbase > 0.01 ? s.VehicleWheelbase : 2.8;
            widthWorld = trackWidth / (2.0 * BitmapFrontWheelHalfXNorm);
            heightWorld = wheelbase / BitmapAxleSpanYNorm;
        }

        private void DrawVehicleSk(SKCanvas canvas, MapRenderState s)
        {
            BitmapTractorSize(s, out double bitmapWWorld, out double bitmapHWorld);
            float trackWidth = (float)(s.VehicleTrackWidth > 0.01 ? s.VehicleTrackWidth : 1.8);
            float wheelbase = (float)(s.VehicleWheelbase > 0.01 ? s.VehicleWheelbase : 2.8);
            float bodyHalfWidth = (float)(bitmapWWorld / 2.0);
            float rectTopWorldY = (float)((1.0 - BitmapRearAxleYNorm) * bitmapHWorld);
            float rectBottomWorldY = (float)(-BitmapRearAxleYNorm * bitmapHWorld);
            float vx = (float)s.VehicleX, vy = (float)s.VehicleY;

            canvas.Save();
            canvas.Translate(vx, vy);
            canvas.RotateRadians(-(float)s.VehicleHeading);

            if (_vehicleSkBitmap == null && s.VehicleImage is Bitmap avBitmap)
            {
                try
                {
                    using var ms = new System.IO.MemoryStream();
                    avBitmap.Save(ms);
                    ms.Position = 0;
                    _vehicleSkBitmap = SKBitmap.Decode(ms);
                }
                catch { /* fall through to triangle */ }
            }

            if (_vehicleSkBitmap != null)
            {
                canvas.Scale(1, -1);
                var dst = new SKRect(-bodyHalfWidth, -rectTopWorldY, bodyHalfWidth, -rectBottomWorldY);
                canvas.DrawBitmap(_vehicleSkBitmap, dst);
                canvas.Scale(1, -1);
            }
            else
            {
                using var path = new SKPath();
                path.MoveTo(0, wheelbase);
                path.LineTo(-bodyHalfWidth, 0);
                path.LineTo(bodyHalfWidth, 0);
                path.Close();
                canvas.DrawPath(path, _vehicleFallbackPaint);
            }

            canvas.DrawCircle((float)s.AntennaOffset, (float)s.AntennaPivot, 0.25f, _antennaPaint);

            if (_frontWheelSkBitmap == null && s.FrontWheelImage is Bitmap wheelAvBitmap)
            {
                try
                {
                    using var ms = new System.IO.MemoryStream();
                    wheelAvBitmap.Save(ms);
                    ms.Position = 0;
                    _frontWheelSkBitmap = SKBitmap.Decode(ms);
                }
                catch { /* leave null */ }
            }

            if (_frontWheelSkBitmap != null && s.VehicleWheelbase > 0.01 && s.VehicleTrackWidth > 0.01)
            {
                float wheelOffsetX = trackWidth / 2.0f;
                float wheelOffsetY = wheelbase + (float)FrontWheelSpriteForwardOffsetM;
                float wheelW = (float)(FrontTireWidthM / WheelBitmapContentWFraction);
                float wheelH = (float)(FrontTireDiameterM / WheelBitmapContentHFraction);
                var wheelDst = new SKRect(-wheelW / 2, -wheelH / 2, wheelW / 2, wheelH / 2);
                float steerDeg = -(float)(s.VehicleSteerAngle * 180.0 / Math.PI);

                canvas.Save();
                canvas.Translate(wheelOffsetX, wheelOffsetY);
                canvas.RotateDegrees(steerDeg);
                canvas.Scale(1, -1);
                canvas.DrawBitmap(_frontWheelSkBitmap, wheelDst);
                canvas.Restore();

                canvas.Save();
                canvas.Translate(-wheelOffsetX, wheelOffsetY);
                canvas.RotateDegrees(steerDeg);
                canvas.Scale(1, -1);
                canvas.DrawBitmap(_frontWheelSkBitmap, wheelDst);
                canvas.Restore();
            }

            if (!s.HasValidHeading)
            {
                float qx = bodyHalfWidth + 1;
                canvas.DrawLine(qx, wheelbase, qx, wheelbase * 0.4f, _headingUnknownLinePaint);
                canvas.DrawCircle(qx, 0, 0.2f, _headingUnknownDotPaint);
            }

            if (s.IsReversing)
            {
                using var revPath = new SKPath();
                revPath.MoveTo(0, -1);
                revPath.LineTo(-bodyHalfWidth * 0.4f, -2.5f);
                revPath.LineTo(bodyHalfWidth * 0.4f, -2.5f);
                revPath.Close();
                canvas.DrawPath(revPath, _reverseIndicatorPaint);
            }

            canvas.Restore();
        }
    }
}
