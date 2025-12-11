using Avalonia.Controls;

namespace AgValoniaGPS.Views.Controls;

/// <summary>
/// Shared host for all modal dialog overlays.
/// Used by both Desktop and iOS platforms to avoid duplication.
/// </summary>
public partial class DialogOverlayHost : UserControl
{
    public DialogOverlayHost()
    {
        InitializeComponent();
    }
}
