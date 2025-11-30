using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Desktop.Controls.Panels;

public partial class FileMenuPanel : UserControl
{
    private bool _isDragging = false;
    private Point _lastScreenPoint;

    public event EventHandler<PointerPressedEventArgs>? DragStarted;
    public event EventHandler<Vector>? DragMoved;
    public event EventHandler<PointerReleasedEventArgs>? DragEnded;

    public FileMenuPanel()
    {
        InitializeComponent();
        var dragHandle = this.FindControl<Grid>("DragHandle");
        if (dragHandle != null)
        {
            dragHandle.PointerPressed += DragHandle_PointerPressed;
            dragHandle.PointerMoved += DragHandle_PointerMoved;
            dragHandle.PointerReleased += DragHandle_PointerReleased;
        }
    }

    private void DragHandle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid handle)
        {
            var root = this.VisualRoot as Visual;
            _lastScreenPoint = root != null ? e.GetPosition(root) : e.GetPosition(this);
            e.Pointer.Capture(handle);
        }
    }

    private void DragHandle_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            var root = this.VisualRoot as Visual;
            var currentPoint = root != null ? e.GetPosition(root) : e.GetPosition(this);
            var distance = Math.Sqrt(Math.Pow(currentPoint.X - _lastScreenPoint.X, 2) +
                                    Math.Pow(currentPoint.Y - _lastScreenPoint.Y, 2));
            if (!_isDragging && distance > 5.0)
            {
                _isDragging = true;
                DragStarted?.Invoke(this, null!);
            }
            if (_isDragging)
            {
                var delta = currentPoint - _lastScreenPoint;
                DragMoved?.Invoke(this, delta);
                _lastScreenPoint = currentPoint;
            }
            e.Handled = true;
        }
    }

    private void DragHandle_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Grid handle && e.Pointer.Captured == handle)
        {
            if (_isDragging) DragEnded?.Invoke(this, e);
            _isDragging = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }
}
