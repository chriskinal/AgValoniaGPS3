using System.Threading.Tasks;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// ViewModel for the Steer Configuration Wizard.
/// Guides users through AutoSteer setup step by step.
/// </summary>
public class SteerWizardViewModel : WizardViewModel
{
    private readonly IConfigurationService _configService;

    public override string WizardTitle => "AutoSteer Configuration Wizard";

    public SteerWizardViewModel(IConfigurationService configService)
    {
        _configService = configService;

        // Add all wizard steps
        AddStep(new WelcomeStepViewModel());
        AddStep(new WheelbaseStepViewModel(configService));
        AddStep(new TrackWidthStepViewModel(configService));
        AddStep(new AntennaPivotStepViewModel(configService));
        AddStep(new AntennaHeightStepViewModel(configService));
        AddStep(new AntennaOffsetStepViewModel(configService));
        AddStep(new FinishStepViewModel());

        // Initialize navigation
        Initialize();
    }

    protected override Task OnCompletingAsync()
    {
        // Save all configuration changes
        _configService.SaveProfile(_configService.Store.ActiveProfileName);
        return Task.CompletedTask;
    }
}
