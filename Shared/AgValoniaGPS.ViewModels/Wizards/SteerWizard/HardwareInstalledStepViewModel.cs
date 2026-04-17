// AgValoniaGPS
// Copyright (C) 2024-2025 AgValoniaGPS Contributors
//
// Licensed under GNU GPL v3. See LICENSE.md.

using System.Threading.Tasks;
using System.Windows.Input;

using AgValoniaGPS.Services.Interfaces;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// What hardware does the user have installed?
/// Determines which later steps to show/skip.
/// </summary>
public class HardwareInstalledStepViewModel : WizardStepViewModel
{
    public override string Title => "Hardware Installed";

    public override string Description =>
        "Select what hardware you have installed. This determines which setup steps are needed.";

    private int _hardwareLevel = 1; // 0=GPS only, 1=GPS+AutoSteer, 2=GPS+AutoSteer+Sections
    /// <summary>0=GPS only, 1=GPS+AutoSteer, 2=GPS+AutoSteer+Sections</summary>
    public int HardwareLevel
    {
        get => _hardwareLevel;
        set
        {
            SetProperty(ref _hardwareLevel, value);
            OnPropertyChanged(nameof(IsGpsOnly));
            OnPropertyChanged(nameof(IsAutoSteer));
            OnPropertyChanged(nameof(IsFullSetup));
        }
    }

    public bool IsGpsOnly => HardwareLevel == 0;
    public bool IsAutoSteer => HardwareLevel == 1;
    public bool IsFullSetup => HardwareLevel == 2;

    /// <summary>True if autosteer hardware is installed (level 1 or 2).</summary>
    public bool HasAutoSteer => HardwareLevel >= 1;

    public ICommand SelectGpsOnlyCommand { get; }
    public ICommand SelectAutoSteerCommand { get; }
    public ICommand SelectFullSetupCommand { get; }

    public HardwareInstalledStepViewModel()
    {
        SelectGpsOnlyCommand = new RelayCommand(() => HardwareLevel = 0);
        SelectAutoSteerCommand = new RelayCommand(() => HardwareLevel = 1);
        SelectFullSetupCommand = new RelayCommand(() => HardwareLevel = 2);
    }

    public override Task<bool> ValidateAsync()
    {
        ClearValidation();
        return Task.FromResult(true);
    }
}
