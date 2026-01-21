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

namespace AgValoniaGPS.ViewModels.Wizards.SteerWizard;

/// <summary>
/// Welcome/Introduction step for the Steer Wizard.
/// </summary>
public class WelcomeStepViewModel : WizardStepViewModel
{
    public override string Title => "Welcome to AutoSteer Setup";

    public override string Description =>
        "This wizard will guide you through configuring your AutoSteer system. " +
        "We'll set up your vehicle dimensions, antenna position, and steering parameters.\n\n" +
        "Make sure your vehicle is on level ground and the GPS receiver is powered on before continuing.";

    public override bool CanGoBack => false; // First step
}
