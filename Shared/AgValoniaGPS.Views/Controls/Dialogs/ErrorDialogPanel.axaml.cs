using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs;

public partial class ErrorDialogPanel : UserControl
{
    public ErrorDialogPanel()
    {
        InitializeComponent();
    }

    private void Backdrop_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Clicking backdrop dismisses the error dialog
        if (DataContext is MainViewModel vm)
        {
            vm.DismissErrorDialogCommand?.Execute(null);
        }
    }
}
