using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring antenna pivot distance.
/// </summary>
public partial class AntennaPivotStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Pivot Distance";

    public override string Description =>
        "Enter the distance from the GPS antenna to the rear axle (pivot point). " +
        "Positive values mean the antenna is ahead of the rear axle, " +
        "negative values mean it's behind.";

    [ObservableProperty]
    private double _antennaPivot;

    [ObservableProperty]
    private string _unit = "m";

    public AntennaPivotStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaPivot = _configService.Store.Vehicle.AntennaPivot;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaPivot = AntennaPivot;
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaPivot < -10 || AntennaPivot > 15)
        {
            SetValidationError("Antenna pivot distance should be between -10 and 15 meters");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
