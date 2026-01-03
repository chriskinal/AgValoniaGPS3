using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring Danfoss hydraulic valve support.
/// </summary>
public partial class DanfossStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Danfoss Valve";

    public override string Description =>
        "Enable this if you're using a Danfoss hydraulic steering valve. " +
        "Danfoss valves require special PWM signal timing. " +
        "Leave disabled for standard motor or hydraulic setups.";

    public override bool CanSkip => true;

    [ObservableProperty]
    private bool _danfossEnabled;

    public DanfossStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        DanfossEnabled = _configService.Store.AutoSteer.DanfossEnabled;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.DanfossEnabled = DanfossEnabled;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
