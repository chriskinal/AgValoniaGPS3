using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring signal inversions.
/// Allows inverting WAS, motor direction, and relay outputs.
/// </summary>
public partial class InvertSettingsStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Signal Inversions";

    public override string Description =>
        "Configure signal inversions if your hardware requires it. " +
        "If the steering moves the wrong direction or sensors read backwards, " +
        "enable the appropriate inversion.";

    public override bool CanSkip => true;

    [ObservableProperty]
    private bool _invertWas;

    [ObservableProperty]
    private bool _invertMotor;

    [ObservableProperty]
    private bool _invertRelays;

    public InvertSettingsStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        InvertWas = autoSteer.InvertWas;
        InvertMotor = autoSteer.InvertMotor;
        InvertRelays = autoSteer.InvertRelays;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.InvertWas = InvertWas;
        autoSteer.InvertMotor = InvertMotor;
        autoSteer.InvertRelays = InvertRelays;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
