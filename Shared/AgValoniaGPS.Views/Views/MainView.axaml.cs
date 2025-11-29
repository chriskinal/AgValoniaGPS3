using Avalonia.Controls;

namespace AgValoniaGPS.Views;

/// <summary>
/// Shared MainView UserControl that contains the core UI structure.
/// Platform-specific content (like map controls) can be injected via MapContent binding.
/// </summary>
public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }
}
