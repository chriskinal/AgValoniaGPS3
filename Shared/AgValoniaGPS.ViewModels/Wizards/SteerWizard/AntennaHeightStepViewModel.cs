using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring antenna height.
/// </summary>
public partial class AntennaHeightStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Height";

    public override string Description =>
        "Enter the height of the GPS antenna above ground level. " +
        "This is used for terrain compensation calculations.";

    public override bool CanSkip => true; // Optional setting

    [ObservableProperty]
    private double _antennaHeight;

    [ObservableProperty]
    private string _unit = "m";

    public AntennaHeightStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaHeight = _configService.Store.Vehicle.AntennaHeight;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaHeight = AntennaHeight;
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaHeight < 0)
        {
            SetValidationError("Antenna height cannot be negative");
            return Task.FromResult(false);
        }
        if (AntennaHeight > 10)
        {
            SetValidationError("Antenna height seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
