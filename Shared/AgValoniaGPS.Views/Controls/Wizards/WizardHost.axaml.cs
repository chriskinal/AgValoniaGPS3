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

using Avalonia.Controls;
using AgValoniaGPS.ViewModels.Wizards;
using AgValoniaGPS.ViewModels.Wizards.SteerWizard;
using AgValoniaGPS.Views.Controls.Wizards.SteerWizard;

namespace AgValoniaGPS.Views.Controls.Wizards;

public partial class WizardHost : UserControl
{
    private ContentControl? _stepContentControl;

    public WizardHost()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _stepContentControl = this.FindControl<ContentControl>("StepContentControl");

        // Subscribe to DataContext changes to handle CurrentStep changes
        if (DataContext is WizardViewModel wizard)
        {
            wizard.PropertyChanged += Wizard_PropertyChanged;
            UpdateStepView(wizard.CurrentStep);
        }

        this.DataContextChanged += WizardHost_DataContextChanged;
    }

    private void WizardHost_DataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is WizardViewModel wizard)
        {
            wizard.PropertyChanged += Wizard_PropertyChanged;
            UpdateStepView(wizard.CurrentStep);
        }
    }

    private void Wizard_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WizardViewModel.CurrentStep) && sender is WizardViewModel wizard)
        {
            UpdateStepView(wizard.CurrentStep);
        }
    }

    private void UpdateStepView(WizardStepViewModel? step)
    {
        if (_stepContentControl == null || step == null)
            return;

        // Manually create the correct view based on the ViewModel type
        // This bypasses DataTemplate resolution which had caching issues
        Control? view = step switch
        {
            // Group A: Introduction
            WelcomeStepViewModel => new WelcomeStepView(),

            // Group B: Vehicle Dimensions
            WheelbaseStepViewModel => new WheelbaseStepView(),
            TrackWidthStepViewModel => new TrackWidthStepView(),
            AntennaPivotStepViewModel => new AntennaPivotStepView(),
            AntennaHeightStepViewModel => new AntennaHeightStepView(),
            AntennaOffsetStepViewModel => new AntennaOffsetStepView(),

            // Group C: Hardware Configuration
            SteerEnableStepViewModel => new SteerEnableStepView(),
            MotorDriverStepViewModel => new MotorDriverStepView(),
            ADConverterStepViewModel => new ADConverterStepView(),
            InvertSettingsStepViewModel => new InvertSettingsStepView(),
            DanfossStepViewModel => new DanfossStepView(),

            // Group G: Completion
            FinishStepViewModel => new FinishStepView(),
            _ => null
        };

        if (view != null)
        {
            view.DataContext = step;
            _stepContentControl.Content = view;
        }
        else
        {
            _stepContentControl.Content = new TextBlock { Text = $"Unknown step: {step.GetType().Name}" };
        }
    }
}
