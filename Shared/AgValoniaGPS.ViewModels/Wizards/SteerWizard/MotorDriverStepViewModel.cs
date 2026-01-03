using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Step for selecting the motor driver type.
/// Options: IBT2 (dual H-bridge) or Cytron (single direction PWM)
/// </summary>
public partial class MotorDriverStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;

    public override string Title => "Motor Driver";

    public override string Description =>
        "Select the type of motor driver used for steering. " +
        "IBT2 is a dual H-bridge driver (most common). " +
        "Cytron uses a single direction PWM signal.";

    public override bool CanSkip => false;

    [ObservableProperty]
    private int _motorDriver;

    /// <summary>
    /// True when IBT2 driver is selected.
    /// </summary>
    public bool IsIBT2Selected => MotorDriver == 0;

    /// <summary>
    /// True when Cytron driver is selected.
    /// </summary>
    public bool IsCytronSelected => MotorDriver == 1;

    public MotorDriverStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        MotorDriver = _configService.Store.AutoSteer.MotorDriver;
    }

    protected override void OnLeaving()
    {
        _configService.Store.AutoSteer.MotorDriver = MotorDriver;
    }

    partial void OnMotorDriverChanged(int value)
    {
        OnPropertyChanged(nameof(IsIBT2Selected));
        OnPropertyChanged(nameof(IsCytronSelected));
    }

    public void SelectIBT2() => MotorDriver = 0;
    public void SelectCytron() => MotorDriver = 1;

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
