using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring vehicle track width.
/// </summary>
public partial class TrackWidthStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Vehicle Track Width";

    public override string Description =>
        "Enter the track width of your vehicle - the distance between the centers of the " +
        "left and right wheels (or tracks).";

    [ObservableProperty]
    private double _trackWidth;

    [ObservableProperty]
    private string _unit = "m";

    public TrackWidthStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        TrackWidth = _configService.Store.Vehicle.TrackWidth;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.TrackWidth = TrackWidth;
    }

    public override Task<bool> ValidateAsync()
    {
        if (TrackWidth < 0.5)
        {
            SetValidationError("Track width must be at least 0.5 meters");
            return Task.FromResult(false);
        }
        if (TrackWidth > 10)
        {
            SetValidationError("Track width seems too large. Please check the value.");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
