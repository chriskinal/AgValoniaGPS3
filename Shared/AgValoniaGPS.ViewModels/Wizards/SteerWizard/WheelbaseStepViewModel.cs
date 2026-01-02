using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring vehicle wheelbase.
/// </summary>
public partial class WheelbaseStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Vehicle Wheelbase";

    public override string Description =>
        "Enter the wheelbase of your vehicle - the distance from the center of the front axle " +
        "to the center of the rear axle.";

    [ObservableProperty]
    private double _wheelbase;

    [ObservableProperty]
    private string _unit = "m";

    public WheelbaseStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        // Load current value
        Wheelbase = _configService.Store.Vehicle.Wheelbase;
    }

    protected override void OnLeaving()
    {
        // Save value
        _configService.Store.Vehicle.Wheelbase = Wheelbase;
    }

    public override Task<bool> ValidateAsync()
    {
        if (Wheelbase < 0.5)
        {
            SetValidationError("Wheelbase must be at least 0.5 meters");
            return Task.FromResult(false);
        }
        if (Wheelbase > 15)
        {
            SetValidationError("Wheelbase seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
