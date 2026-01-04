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

        // Group A: Introduction
        AddStep(new WelcomeStepViewModel());

        // Group B: Vehicle Dimensions
        AddStep(new WheelbaseStepViewModel(configService));
        AddStep(new TrackWidthStepViewModel(configService));
        AddStep(new AntennaPivotStepViewModel(configService));
        AddStep(new AntennaHeightStepViewModel(configService));
        AddStep(new AntennaOffsetStepViewModel(configService));

        // Group C: Hardware Configuration
        AddStep(new SteerEnableStepViewModel(configService));
        AddStep(new MotorDriverStepViewModel(configService));
        AddStep(new ADConverterStepViewModel(configService));
        AddStep(new InvertSettingsStepViewModel(configService));
        AddStep(new DanfossStepViewModel(configService));

        // Group G: Completion
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
