using Avalonia.Controls;
using Avalonia.Input;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class FieldSelectionDialogPanel : UserControl
{
    public FieldSelectionDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Cancel the dialog when clicking/tapping the backdrop
        if (DataContext is AgValoniaGPS.ViewModels.MainViewModel vm)
        {
            vm.CancelFieldSelectionDialogCommand?.Execute(null);
        }
        e.Handled = true;
    }
}
