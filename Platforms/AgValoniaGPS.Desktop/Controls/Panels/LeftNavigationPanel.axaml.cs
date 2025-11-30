using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AgValoniaGPS.Desktop.Controls.Panels;

public partial class LeftNavigationPanel : UserControl
{
    private bool _isDragging = false;
    private Point _dragStartPoint;
    private DateTime _pressTime;
    private const int TapTimeThresholdMs = 300;
    private const double TapDistanceThreshold = 5.0;

    /// <summary>
    /// Event raised when the user starts dragging the panel.
    /// The parent view can subscribe to this to handle repositioning.
    /// </summary>
    public event EventHandler<PointerPressedEventArgs>? DragStarted;

    /// <summary>
    /// Event raised when the user is dragging the panel.
    /// Provides the delta movement since drag started.
    /// </summary>
    public event EventHandler<Vector>? DragMoved;

    /// <summary>
    /// Event raised when the user stops dragging the panel.
    /// </summary>
    public event EventHandler<PointerReleasedEventArgs>? DragEnded;

    public LeftNavigationPanel()
    {
        InitializeComponent();

        // Wire up drag handle events
        var dragHandle = this.FindControl<Grid>("DragHandle");
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += DragHandle_PointerPressed;
            dragHandle.PointerMoved += DragHandle_PointerMoved;
            dragHandle.PointerReleased += DragHandle_PointerReleased;
        }

        // Wire up sub-panel drag events
        WireUpSubPanelDrag<SimulatorPanel>("SimulatorPanelControl");
        WireUpSubPanelDrag<ViewSettingsPanel>("ViewSettingsPanelControl");
        WireUpSubPanelDrag<FileMenuPanel>("FileMenuPanelControl");
        WireUpSubPanelDrag<ToolsPanel>("ToolsPanelControl");
        WireUpSubPanelDrag<ConfigurationPanel>("ConfigurationPanelControl");
        WireUpSubPanelDrag<JobMenuPanel>("JobMenuPanelControl");
        WireUpSubPanelDrag<FieldToolsPanel>("FieldToolsPanelControl");
        WireUpSubPanelDrag<BoundaryRecordingPanel>("BoundaryRecordingPanelControl");
        WireUpSubPanelDrag<BoundaryPlayerPanel>("BoundaryPlayerPanelControl");
    }

    private void WireUpSubPanelDrag<T>(string controlName) where T : UserControl
    {
        var panel = this.FindControl<T>(controlName);
        if (panel == null) return;

        // Use reflection to check for DragMoved event
        var dragMovedEvent = typeof(T).GetEvent("DragMoved");
        if (dragMovedEvent != null)
        {
            dragMovedEvent.AddEventHandler(panel, new EventHandler<Vector>((sender, delta) =>
            {
                if (sender is Control control)
                {
                    var left = Canvas.GetLeft(control);
                    var top = Canvas.GetTop(control);
                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;
                    Canvas.SetLeft(control, left + delta.X);
                    Canvas.SetTop(control, top + delta.Y);
                }
            }));
        }
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid handle)
        {
            _pressTime = DateTime.Now;
            _dragStartPoint = e.GetPosition(this);
            e.Pointer.Capture(handle);

            // Close any tooltips
            ToolTip.SetIsOpen(handle, false);
        }
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            // Start dragging if moved beyond threshold
            if (!_isDragging && distance > TapDistanceThreshold)
            {
                _isDragging = true;
                ToolTip.SetIsOpen(handle, false);
                DragStarted?.Invoke(this, null!);
            }

            if (_isDragging)
            {
                var delta = currentPoint - _dragStartPoint;
                DragMoved?.Invoke(this, delta);
                _dragStartPoint = currentPoint; // Update for continuous dragging
            }

            e.Handled = true;
        }
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            var currentPoint = e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));
            var elapsed = (DateTime.Now - _pressTime).TotalMilliseconds;

            // Detect tap: quick release with minimal movement
            bool isTap = !_isDragging &&
                        elapsed < TapTimeThresholdMs &&
                        distance < TapDistanceThreshold;

            if (isTap)
            {
                // Tap detected - rotate the panel
                RotatePanel();
            }
            else if (_isDragging)
            {
                DragEnded?.Invoke(this, e);
            }

            // Reset state
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void RotatePanel()
    {
        var buttonStack = this.FindControl<StackPanel>("ButtonStack");
        if (buttonStack != null)
        {
            buttonStack.Orientation = buttonStack.Orientation == Orientation.Vertical
                ? Orientation.Horizontal
                : Orientation.Vertical;
        }
    }
}
