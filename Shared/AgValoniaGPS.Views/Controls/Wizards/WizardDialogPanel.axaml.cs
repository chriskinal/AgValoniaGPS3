using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels.Wizards;

namespace AgValoniaGPS.Views.Controls.Wizards;

public partial class WizardDialogPanel : UserControl
{
    public WizardDialogPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the wizard ViewModel to display.
    /// </summary>
    public void SetWizard(WizardViewModel wizard)
    {
        WizardHostControl.DataContext = wizard;
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Optional: clicking backdrop could cancel the wizard
        // For now, do nothing - require explicit Cancel button
    }
}
