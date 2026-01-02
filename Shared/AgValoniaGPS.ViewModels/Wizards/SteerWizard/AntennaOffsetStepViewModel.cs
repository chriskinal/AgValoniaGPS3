using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring antenna lateral offset.
/// </summary>
public partial class AntennaOffsetStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Antenna Offset";

    public override string Description =>
        "Enter the lateral offset of the GPS antenna from the vehicle centerline. " +
        "Positive values = right of center, negative values = left of center. " +
        "If your antenna is mounted on the centerline, enter 0.";

    public override bool CanSkip => true; // Optional setting

    [ObservableProperty]
    private double _antennaOffset;

    [ObservableProperty]
    private string _unit = "m";

    /// <summary>
    /// Whether the antenna is on the left side.
    /// </summary>
    public bool IsLeft => AntennaOffset < 0;

    /// <summary>
    /// Whether the antenna is centered.
    /// </summary>
    public bool IsCenter => Math.Abs(AntennaOffset) < 0.01;

    /// <summary>
    /// Whether the antenna is on the right side.
    /// </summary>
    public bool IsRight => AntennaOffset > 0;

    public AntennaOffsetStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        AntennaOffset = _configService.Store.Vehicle.AntennaOffset;
    }

    protected override void OnLeaving()
    {
        _configService.Store.Vehicle.AntennaOffset = AntennaOffset;
    }

    partial void OnAntennaOffsetChanged(double value)
    {
        OnPropertyChanged(nameof(IsLeft));
        OnPropertyChanged(nameof(IsCenter));
        OnPropertyChanged(nameof(IsRight));
    }

    public override Task<bool> ValidateAsync()
    {
        if (AntennaOffset < -5 || AntennaOffset > 5)
        {
            SetValidationError("Antenna offset should be between -5 and 5 meters");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
