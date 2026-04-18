// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Threading.Tasks;

using AgValoniaGPS.Services.Interfaces;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Combined step for all hardware configuration: steer enable method,
/// motor driver, A/D converter, signal inversions, and Danfoss valve.
/// </summary>
public class HardwareConfigStepViewModel : WizardStepViewModel
{
    private readonly IConfigurationService _configService;
    private HardwareInstalledStepViewModel? _hardwareStep;

    public override string Title => "Hardware Configuration";

    public override bool ShouldSkip => _hardwareStep?.HardwareLevel == 0;

    public void SetHardwareStep(HardwareInstalledStepViewModel step) => _hardwareStep = step;

    public override string Description =>
        "Configure your steering hardware settings: enable method, motor driver type, " +
        "A/D converter, signal inversions, and Danfoss valve support.";

    // --- Steer Enable ---

    private int _externalEnable;
    public int ExternalEnable
    {
        get => _externalEnable;
        set
        {
            SetProperty(ref _externalEnable, value);
            OnPropertyChanged(nameof(IsNoneSelected));
            OnPropertyChanged(nameof(IsSwitchSelected));
            OnPropertyChanged(nameof(IsButtonSelected));
            OnPropertyChanged(nameof(SteerEnableDescription));
        }
    }

    public bool IsNoneSelected => ExternalEnable == 0;
    public bool IsSwitchSelected => ExternalEnable == 1;
    public bool IsButtonSelected => ExternalEnable == 2;

    public string SteerEnableDescription => ExternalEnable switch
    {
        0 => "No external enable - autosteer always available",
        1 => "Toggle switch enables/disables autosteer",
        2 => "Momentary button to engage autosteer",
        _ => string.Empty
    };

    public void SelectNone() => ExternalEnable = 0;
    public void SelectSwitch() => ExternalEnable = 1;
    public void SelectButton() => ExternalEnable = 2;

    // --- Motor Driver ---

    private int _motorDriver = 1;
    public int MotorDriver
    {
        get => _motorDriver;
        set
        {
            SetProperty(ref _motorDriver, value);
            OnPropertyChanged(nameof(IsIBT2Selected));
            OnPropertyChanged(nameof(IsCytronSelected));
        }
    }

    public bool IsIBT2Selected => MotorDriver == 0;
    public bool IsCytronSelected => MotorDriver == 1;

    public void SelectIBT2() => MotorDriver = 0;
    public void SelectCytron() => MotorDriver = 1;

    // --- A/D Converter ---

    private int _adConverter = 1;
    public int AdConverter
    {
        get => _adConverter;
        set
        {
            SetProperty(ref _adConverter, value);
            OnPropertyChanged(nameof(IsDifferentialSelected));
            OnPropertyChanged(nameof(IsSingleSelected));
        }
    }

    public bool IsDifferentialSelected => AdConverter == 0;
    public bool IsSingleSelected => AdConverter == 1;

    public void SelectDifferential() => AdConverter = 0;
    public void SelectSingle() => AdConverter = 1;

    // --- Invert Settings ---

    private bool _invertRelays;
    public bool InvertRelays
    {
        get => _invertRelays;
        set => SetProperty(ref _invertRelays, value);
    }

    // --- Danfoss ---

    private bool _danfossEnabled;
    public bool DanfossEnabled
    {
        get => _danfossEnabled;
        set => SetProperty(ref _danfossEnabled, value);
    }

    public HardwareConfigStepViewModel(IConfigurationService configService)
    {
        _configService = configService;
    }

    protected override void OnEntering()
    {
        var autoSteer = _configService.Store.AutoSteer;
        ExternalEnable = autoSteer.ExternalEnable;
        MotorDriver = autoSteer.MotorDriver;
        AdConverter = autoSteer.AdConverter;
        InvertRelays = autoSteer.InvertRelays;
        DanfossEnabled = autoSteer.DanfossEnabled;
    }

    protected override void OnLeaving()
    {
        var autoSteer = _configService.Store.AutoSteer;
        autoSteer.ExternalEnable = ExternalEnable;
        autoSteer.MotorDriver = MotorDriver;
        autoSteer.AdConverter = AdConverter;
        autoSteer.InvertRelays = InvertRelays;
        autoSteer.DanfossEnabled = DanfossEnabled;
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
