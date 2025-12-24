using Avalonia.Controls;
using Avalonia.Input;
using AgValoniaGPS.ViewModels;

namespace AgValoniaGPS.Views.Controls.Dialogs.Configuration.SourcesSubTabs;

public partial class GpsSubTab : UserControl
{
    public GpsSubTab()
    {
        InitializeComponent();
    }

    private void OnSingleGpsCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ConfigurationViewModel vm)
        {
            vm.SetSingleGpsCommand.Execute(null);
        }
    }

    private void OnDualGpsCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ConfigurationViewModel vm)
        {
            vm.SetDualGpsCommand.Execute(null);
        }
    }
}
