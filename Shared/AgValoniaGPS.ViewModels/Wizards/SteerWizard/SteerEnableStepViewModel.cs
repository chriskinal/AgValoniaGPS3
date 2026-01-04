using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for configuring how AutoSteer is enabled.
/// Options: None (software only), Switch (physical toggle), Button (momentary)
/// </summary>
public partial class SteerEnableStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Steer Enable Method";

    public override string Description =>
        "Select how AutoSteer will be enabled. A physical switch or button provides " +
        "a hardware safety override. 'None' uses software-only control.";

    public override bool CanSkip => false;

    [ObservableProperty]
    private int _externalEnable;

    /// <summary>
    /// True when no external enable is selected (software only).
    /// </summary>
    public bool IsNoneSelected => ExternalEnable == 0;

    /// <summary>
    /// True when switch enable is selected.
    /// </summary>
    public bool IsSwitchSelected => ExternalEnable == 1;

    /// <summary>
    /// True when button enable is selected.
    /// </summary>
    public bool IsButtonSelected => ExternalEnable == 2;

    public SteerEnableStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        ExternalEnable = _configService.Store.AutoSteer.ExternalEnable;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.ExternalEnable = ExternalEnable;
    }

    partial void OnExternalEnableChanged(int value)
    {
        OnPropertyChanged(nameof(IsNoneSelected));
        OnPropertyChanged(nameof(IsSwitchSelected));
        OnPropertyChanged(nameof(IsButtonSelected));
    }

    public void SelectNone() => ExternalEnable = 0;
    public void SelectSwitch() => ExternalEnable = 1;
    public void SelectButton() => ExternalEnable = 2;

    public override Task<bool> ValidateAsync()
    {
        if (ExternalEnable < 0 || ExternalEnable > 2)
        {
            SetValidationError("Please select a valid enable method");
            return Task.FromResult(false);
        }

        ClearValidation();
        return Task.FromResult(true);
    }
}
