using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;

namespace AgValoniaGPS.ViewModels;

/// <summary>
/// Wizard commands - AutoSteer wizard, etc.
/// </summary>
public partial class MainViewModel
{
    // SteerWizard ViewModel
    private SteerWizardViewModel? _steerWizardViewModel;
    public SteerWizardViewModel? SteerWizardViewModel
    {
        get => _steerWizardViewModel;
        set => SetProperty(ref _steerWizardViewModel, value);
    }

    // Wizard commands
    public ICommand? ShowSteerWizardCommand { get; private set; }

    private void InitializeWizardCommands()
    {
        ShowSteerWizardCommand = new RelayCommand(ShowSteerWizard);
    }

    private void ShowSteerWizard()
    {
        // Create a new instance of the wizard
        SteerWizardViewModel = new SteerWizardViewModel(_configurationService);

        // Handle wizard close
        SteerWizardViewModel.CloseRequested += (s, e) =>
        {
            if (SteerWizardViewModel != null)
                SteerWizardViewModel.IsDialogVisible = false;
        };

        // Show the wizard
        SteerWizardViewModel.IsDialogVisible = true;
    }
}
